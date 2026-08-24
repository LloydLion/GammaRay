using System.Diagnostics;
using System.Net.Http.Headers;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow;
using GammaRay.Core.Protocols.HTTP;
using GammaRay.Core.Utils;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

namespace GammaRay.Core.InternetAccess.Channels.Testing;

public class IAPChannelHardTester(
	IChannelDriverRegistry _driverRegistry,
	IOptions<IAPChannelHardTester.Options> options
) : IIAPChannelHardTester
{
	private readonly Options _options = options.Value;
	private readonly WeightedRandomElementSelector<RequestKindOptions> _requestOptionsSelector = new(options.Value.RequestKinds, r => r.Weight);
	private readonly ObjectPool<HttpClientWrapper> _clientsPool = new DefaultObjectPoolProvider().Create<HttpClientWrapper>();


	public async ValueTask<bool> PerformTestAsync(IAPChannel channel, TrackableProcedure monitoring)
	{
		var requestKindReports = new Dictionary<string, RequestKindReport>();
		var mainTestReport = new MainTestReport();
		AfterTestCheckReport? afterTestReport = null;
		try
		{
			var random = new Random();
			var interval = TimeSpan.Zero;

			var requests = new HashSet<(Task<RequestResult> Task, RequestKindOptions KindOptions)>(_options.MaxParallelRequests);

			var budget = _options.TrafficBudget;
			
			int requestMade = 0;
			int requestSuccess = 0;
			
			while (requestMade < _options.MaxRequests && budget > 0)
			{
				if (interval != TimeSpan.Zero)
					await Task.Delay(interval);

				foreach (var request in requests)
					if (request.Task.IsCompleted)
					{
						if (requestKindReports.TryGetValue(request.KindOptions.Name, out var kindReport) == false)
							requestKindReports.Add(request.KindOptions.Name, kindReport = new(request.KindOptions.Name));
						
						requests.Remove(request);
						requestMade++;
						kindReport.RequestsMade = kindReport.RequestsMade.Value + 1;
						
						if (request.Task.IsCompletedSuccessfully && request.Task.Result.Success)
						{
							budget -= request.KindOptions.ExpectedBodyLength;
							requestSuccess++;
							kindReport.RequestsSuccess = kindReport.RequestsSuccess.Value + 1;
						}
					}

				var maxRequestsToStart = _options.MaxParallelRequests - requests.Count;
				var requestsToStart = random.Next(maxRequestsToStart);
				for (int i = 0; i < requestsToStart; i++)
				{
					var requestOptions = _requestOptionsSelector.Next(random);
					requests.Add((StartRequest(channel, requestOptions), requestOptions));
				}
				
				interval = new TimeSpan(random.NextInt64(_options.MaxTestInterval.Ticks));
			}

			mainTestReport.RequestsMade = requestMade;
			mainTestReport.RequestsSuccess = requestSuccess;
			mainTestReport.IsTrafficBudgetSpent = budget <= 0;
			
			var successRequestPercent = requestMade == 0 ? 0 : requestSuccess * 100 / requestMade;
			var isMainTestPassed = successRequestPercent < _options.RequiredSuccessRequestPercent;
			mainTestReport.IsPassed = isMainTestPassed;
			if (isMainTestPassed == false)
				return false;

			

			await Task.Delay(_options.AfterTestPause);

			afterTestReport = new();
			
			var successAfterTestCheckRequests = 0;
			for (int i = 0; i < _options.AfterTestCheckRequestCount; i++)
			{
				var result = await StartRequest(channel, _options.RequestKinds[0]);
				if (result.Success)
					successAfterTestCheckRequests++;
				
				var remainingTests = _options.AfterTestCheckRequestCount - i - 1;
				var requiredSuccessTestsToBeSuccess = _options.RequiredSuccessAfterTestCheckRequestCount - successAfterTestCheckRequests;
				if (requiredSuccessTestsToBeSuccess > remainingTests)
					break;

				if (remainingTests != 0)
					await Task.Delay(_options.AfterTestCheckRequestInterval);
			}
			
			var isAfterTestCheckPassed = successAfterTestCheckRequests >= _options.RequiredSuccessAfterTestCheckRequestCount;
			afterTestReport.AfterTestCheckRequestsMade = _options.AfterTestCheckRequestCount;
			afterTestReport.AfterTestCheckRequestsSuccess = successAfterTestCheckRequests;
			afterTestReport.IsPassed = isAfterTestCheckPassed;
			return isAfterTestCheckPassed;
		}
		finally
		{
			foreach (var report in requestKindReports.Values)
				monitoring.CommitReport(report);
			monitoring.CommitReport(mainTestReport);
			if (afterTestReport is not null)
				monitoring.CommitReport(afterTestReport);
		}
	}

	private async Task<RequestResult> StartRequest(IAPChannel channel, RequestKindOptions requestKindOptions)
	{
		HttpClientWrapper? client = null;
		try
		{
			var uri = requestKindOptions.Uri;
			var endPoint = new WebEndPoint(new GenericWebEndPoint(new(uri.Host), uri.Port), TransportType.StreamBased);
			var openingResult = await _driverRegistry
				.ProvideDriver(channel.DriverName)
				.TryOpenChannelAsync(channel, endPoint);
			if (openingResult.Type != ChannelOpeningResult.ResultType.Success)
				return new RequestResult(false, $"Failed to open channel: {openingResult.InternalException}");

			await using (openingResult)
			{
				var dataFlow = (IStreamDataFlow)openingResult.OpenChannel.GetFlow();
				
				using var request = new HttpRequestMessage(HttpMethod.Get, uri);
				request.Headers.Add("User-Agent", "GammaRay/1.0.0");
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
				request.Headers.Connection.Add("close");

				foreach (var header in requestKindOptions.Headers)
				{
					request.Headers.Remove(header.Key);
					request.Headers.Add(header.Key, header.Value);
				}
				
				client = _clientsPool.Get();
				client.Configure(dataFlow, endPoint);
				using var response = await client.AccessClient().SendAsync(request);
				
				await using var responseStream = await response.Content.ReadAsStreamAsync();

				var buffer = new byte[1024];
				while (true)
					if (await responseStream.ReadAsync(buffer) == 0)
						break;
			}

			return new RequestResult(true, "Success");

		}
		catch (Exception ex)
		{
			Debugger.BreakForUserUnhandledException(ex);
			return new RequestResult(false, $"Exception: {ex}");
		}
		finally
		{
			if (client is not null)
				_clientsPool.Return(client);
		}
	}


	public class Options
	{
		public int RequiredSuccessRequestPercent { get; init; } = 95;
		
		public TimeSpan MaxTestInterval { get; init; } = TimeSpan.FromSeconds(7);
		
		public int MaxParallelRequests { get; init; } = 10;

		public int TrafficBudget { get; init; } = 20 * 1024 * 1024;

		public int MaxRequests { get; init; } = 50;

		public RequestKindOptions[] RequestKinds { get; init; } =
		[
			new() { Name = "Ping", Uri = new Uri("http://www.gstatic.com/generate_204"), ExpectedBodyLength = 0, Weight = 70 },
			new() { Name = "BigFile", Uri = new Uri("https://testfile.to/dl/1mb"), ExpectedBodyLength = 1024*1024, Weight = 30 }
		];
		
		public TimeSpan AfterTestCheckRequestInterval { get; init; } = TimeSpan.FromSeconds(2);
		
		public int AfterTestCheckRequestCount { get; init; } = 5;
		
		public int RequiredSuccessAfterTestCheckRequestCount { get; init; } = 4;
		
		public TimeSpan AfterTestPause { get; init; } = TimeSpan.FromSeconds(20);
	}

	public class RequestKindOptions
	{
		public required string Name { get; init; }

		public required Uri Uri { get; init; }

		public Dictionary<string, string> Headers { get; init; } = [];

		public required int ExpectedBodyLength { get; init; }

		public required int Weight { get; init; }
	}

	private record struct RequestResult(bool Success, string Reason);
	
	[SystemReportMetadata(nameof(IIAPChannelHardTester), nameof(IAPChannelHardTester), "HardTest/RequestKind")]
	public class RequestKindReport(string? kindName = null, TrackableProcedure? autoBind = null) : SystemReport(autoBind)
	{
		public ReportProperty<string> KindName { get; set; } = kindName ?? default(ReportProperty<string>);

		public ReportProperty<int> RequestsMade { get; set; } = 0;
		
		public ReportProperty<int> RequestsSuccess { get; set; } = 0;
	}

	[SystemReportMetadata(nameof(IIAPChannelHardTester), nameof(IAPChannelHardTester), "HardTest/MainTest")]
	public class MainTestReport(TrackableProcedure? autoBind = null) : SystemReport(autoBind)
	{
		public ReportProperty<int> RequestsMade { get; set; }

		public ReportProperty<int> RequestsSuccess { get; set; }
		
		public ReportProperty<bool> IsTrafficBudgetSpent { get; set; }
		
		public ReportProperty<bool> IsPassed { get; set; }
	}

	[SystemReportMetadata(nameof(IIAPChannelHardTester), nameof(IAPChannelHardTester), "HardTest/AfterTestCheck")]
	public class AfterTestCheckReport(TrackableProcedure? autoBind = null) : SystemReport(autoBind)
	{
		public ReportProperty<int> AfterTestCheckRequestsMade { get; set; }
		
		public ReportProperty<int> AfterTestCheckRequestsSuccess { get; set; }
		
		public ReportProperty<bool> IsPassed { get; set; }
	}
}
