using GammaRay.Core.Monitoring;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow;
using GammaRay.Core.Protocols.TLS;
using GammaRay.Core.Utils;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Authentication;

namespace GammaRay.Core.Services.Probing.Drivers;

[RecommendedDriverName("HTTP")]
public sealed class HTTPProbingDriver : IProbeDriver, IDisposable
{
	private readonly ConnectCallbackSource _connectCallbackSource = new();
	private readonly SemaphoreSlim _sync = new(1);
	private HttpClient? _client;


	public async Task<ProbeResult> ProbeAsync(ProbingArgs args)
	{
		var (targetOutcomingFlow, endPoint, parameters, options, time, trackingProcedure) =
			(args.TargetOutcomingFlow, args.EndPoint, args.Parameters, args.Options, args.TimeProvider, args.MonitoringContext);

		if (targetOutcomingFlow is not IStreamDataFlow streamDataFlow)
			throw new ArgumentException("Only stream based data flows supported", nameof(args));

		using var report = new Report(trackingProcedure);

		var strongParameters = ParseParameters(parameters);
		report.Parameters = strongParameters;

		var readingOptions = new DataFlowReadingOptions() { Timeout = options.RTTTimeout };
		var writingOptions = new DataFlowWritingOptions() { Timeout = options.RTTTimeout };

		var startTimestamp = time.GetTimestamp();
		var result = new ResultHelper(time, startTimestamp, report, strongParameters);

		try
		{
			streamDataFlow = await ApplyTLSOptionsAsync(streamDataFlow, strongParameters, endPoint.Host, readingOptions.Timeout);
		}
		catch (TimeoutException) { return result.L6Failure(ProbeResult.CommunicationStatus.Timeout); }
		catch (AuthenticationException ex) { return result.L6Failure(ProbeResult.CommunicationStatus.UnexceptedData, ex.Message); }
		catch (Exception ex) { return result.L6Failure(ProbeResult.CommunicationStatus.FlowFailure, ex.ToString()); }


		await _sync.WaitAsync();
		try
		{
			CreateClientIfNeed();
			using var connectCallbackConfiguration = _connectCallbackSource.Configure(streamDataFlow, endPoint);
			_connectCallbackSource.FlowWrapper.WritingOptions = writingOptions;
			_connectCallbackSource.FlowWrapper.ReadingOptions = readingOptions;

			var baseUri = new Uri($"http://{endPoint.Host.Domain}:{endPoint.Port}/");
			var uri = new Uri(baseUri, $"{strongParameters.Path}");
			for (int redirections = 0; redirections != strongParameters.MaxRedirectCount + 1; redirections++)
			{
				using var request = new HttpRequestMessage(new HttpMethod(strongParameters.Method), uri);
				request.Headers.Add("User-Agent", strongParameters.UserAgent);
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
				request.Headers.Connection.Add("keep-alive");

				using var response = await _client.SendAsync(request);

				if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.UnavailableForLegalReasons)
					return result.L7Failure(ProbeResult.CommunicationStatus.RemoteServerBan, "Server returned 403 or 451 status code");

				var isStatusCodeSatisfied = strongParameters.RequireNonErrorStatusCode is false || ((int)response.StatusCode / 100) is 1 or 2 or 3;
				if (isStatusCodeSatisfied == false)
					return result.L7Failure(ProbeResult.CommunicationStatus.UnexceptedData, $"Server returned invalid status code: {(int)response.StatusCode}");

				// If 'Location' header is present, we consider redirection
				var redirectLocation = response.Headers.Location;
				if (
					redirectLocation is not null &&
					redirectLocation.Host == endPoint.Host.Domain && redirectLocation.Port == endPoint.Port &&
					(redirectLocation.Scheme, strongParameters.UseTLS) is ("https", true) or ("http", false)
				)
				{
					if (response.Headers.Connection.Any(s => s.Equals("Close", StringComparison.OrdinalIgnoreCase)) == false)
					{
						var pathAndQuery = redirectLocation.PathAndQuery;
						uri = new Uri(baseUri, pathAndQuery);
						continue;
	 				}
				}

				await using var responseStream = await response.Content.ReadAsStreamAsync();

				_connectCallbackSource.FlowWrapper.ReadingOptions = readingOptions with { Timeout = options.ContinuousDataTimeout };

				var buffer = new byte[1024];
				while (true)
				{
					var read = await responseStream.ReadAsync(buffer);
					if (read == 0)
						break;
				}

				return result.Success();
			}

			return result.L7Failure(ProbeResult.CommunicationStatus.UnexceptedData, "Too much redirects");
		}
		catch (TimeoutException) { return result.L7Failure(ProbeResult.CommunicationStatus.Timeout); }
		catch (HttpRequestException ex) { return result.L7Failure(ProbeResult.CommunicationStatus.UnexceptedData, ex.Message); }
		catch (Exception ex) { return result.L7Failure(ProbeResult.CommunicationStatus.FlowFailure, ex.ToString()); }
		finally
		{
			_sync.Release();
		}
	}

	private async static ValueTask<IStreamDataFlow> ApplyTLSOptionsAsync(IStreamDataFlow originalDataFlow, StrongParameters parameters, WebHost targetHost, TimeSpan timeout)
	{
		var dataFlow = originalDataFlow;
		if (parameters.UseTLS)
		{
			var tlsFlow = new TLSDataFlowWrapper(originalDataFlow);
			await tlsFlow.BeginConnectionAsync(targetHost.Domain, timeout);
			dataFlow = tlsFlow;
		}

		return dataFlow; 
	}

	private static StrongParameters ParseParameters(IReadOnlyDictionary<string, string> parameters)
	{
		var useTLS = bool.Parse(parameters.GetValueOrDefault("useTLS") ?? "true");
		var path = parameters.GetValueOrDefault("path") ?? "";
		var method = parameters.GetValueOrDefault("method") ?? "GET";
		var userAgent = parameters.GetValueOrDefault("userAgent") ??
			"GammaRay/" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0");
		var requireNonErrorStatusCode = bool.Parse(parameters.GetValueOrDefault("requireNonErrorStatusCode") ?? "false");
		var maxRedirectCount = int.Parse(parameters.GetValueOrDefault("maxRedirectCount") ?? "2");

		return new StrongParameters(useTLS, path, method, userAgent, requireNonErrorStatusCode, maxRedirectCount);
	}

	[MemberNotNull(nameof(_client))]
	private void CreateClientIfNeed()
	{
		_client = new HttpClient(new SocketsHttpHandler()
		{
			ConnectCallback = _connectCallbackSource.Callback,
			AllowAutoRedirect = false,
			EnableMultipleHttp2Connections = false,
			EnableMultipleHttp3Connections = false,
			UseCookies = false,
			UseProxy = false,
			PooledConnectionLifetime = TimeSpan.Zero,
			PooledConnectionIdleTimeout = TimeSpan.Zero,
		}, disposeHandler: true);
	}

	public void Dispose()
	{
		_client?.Dispose();
	}


	public record StrongParameters(bool UseTLS, string Path, string Method, string UserAgent, bool RequireNonErrorStatusCode, int MaxRedirectCount);

	[SystemReportMetadata(nameof(IProbeDriver), nameof(HTTPProbingDriver), "Probe")]
	public class Report(TrackableProcedure? autoBind = null) : SystemReport(autoBind)
	{
		public ReportProperty<StrongParameters> Parameters { get; set; }

		public ReportProperty<int> ResponseStatusCode { get; set; }

		public ReportProperty<long> TotalResponseBodyLength { get; set; }

		public ReportProperty<ProbeResult> Result { get; set; }
	}

	private readonly struct ResultHelper(TimeProvider _time, long _startTime, Report _report, StrongParameters _parameters)
	{
		public ProbeResult Success()
		{
			var l6Status = _parameters.UseTLS ? ProbeResult.CommunicationStatus.Success : ProbeResult.CommunicationStatus.Skipped;
			var result = new ProbeResult(ProbeResult.CommunicationStatus.Success, l6Status, _time.GetElapsedTime(_startTime));
			_report.Result = result;
			return result;
		}

		public ProbeResult L6Failure(ProbeResult.CommunicationStatus status, string? comment = null)
		{
			var result = new ProbeResult(ProbeResult.CommunicationStatus.Skipped, status, _time.GetElapsedTime(_startTime))
				{ FailureComment = comment };
			_report.Result = result;
			return result;
		}

		public ProbeResult L7Failure(ProbeResult.CommunicationStatus status, string? comment = null)
		{
			var l6Status = _parameters.UseTLS ? ProbeResult.CommunicationStatus.Success : ProbeResult.CommunicationStatus.Skipped;
			var result = new ProbeResult(status, l6Status, _time.GetElapsedTime(_startTime))
				{ FailureComment = comment };
			_report.Result = result;
			return result;
		}
	}

	private class ConnectCallbackSource
	{
		private DataFlowStreamWrapper _wrapper = new();
#if DEBUG
		private WebEndPoint? _targetEndPoint = null;
#endif


		public ValueTask<Stream> Callback(SocketsHttpConnectionContext ctx, CancellationToken _)
		{
			if (_wrapper is null)
				throw new InvalidOperationException("Set data flow first");
#if DEBUG
			if (_targetEndPoint is not null)
			{
				Debug.Assert(ctx.DnsEndPoint.Port == _targetEndPoint.Value.Port);
				Debug.Assert(ctx.DnsEndPoint.Host == _targetEndPoint.Value.Host);
			}
#endif
			return ValueTask.FromResult<Stream>(_wrapper);
		}

		public ConfigurationHandle Configure(IStreamDataFlow dataFlow, WebEndPoint? targetEndPoint)
		{
			if (_wrapper is not null)
				_wrapper.ReInit(dataFlow);
			else _wrapper = new DataFlowStreamWrapper(dataFlow);
#if DEBUG
			_targetEndPoint = targetEndPoint;
#endif
			return new(this);
		}


		public DataFlowStreamWrapper FlowWrapper => _wrapper;


		public readonly struct ConfigurationHandle(ConnectCallbackSource source) : IDisposable
		{
			public void Dispose()
			{
				source._wrapper.ReInit(newDataFlow: null);
#if DEBUG
				source._targetEndPoint = null;
#endif
			}
		}
	}
}
