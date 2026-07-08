using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Network.Profiles;
using GammaRay.Core.Utils;
using Microsoft.Extensions.Options;
using System.Threading.Channels;
using static GammaRay.Core.Services.Probing.ProbeResult;

namespace GammaRay.Core.Services.Probing;

public sealed class ProbingManager(
	IDriverRegistry<IProbeDriver> _probeDriverRegistry,
	IIAPChannelPicker _channelPicker,
	IChannelDriverRegistry _channelDriverRegistry,

	INetworkIdentifier _networkIdentifier,
	INetworkProfileMappingRepository _networkProfileRepository,

	MonitoringSystem _monitoringSystem,
	TimeProvider _time,

	IOptions<ProbingManager.Options> options
) : IProbingManager
{
	private readonly HashSet<ProbingTask> _tasks = [];
	private readonly Options _options = options.Value;


	public void StartProbing(Service service, IReadOnlyCollection<InternetAccessPoint> pointsToProbeVia, IServiceStatusTableRepository routeOutput)
	{
		if (_tasks.Any(s => s.Service == service) == false)
		{
			var probingTask = new ProbingTask(service);

			var task = StartProbingTask(service, pointsToProbeVia, routeOutput, () => _tasks.Remove(probingTask));
			probingTask.SetTask(task);

			_tasks.Add(probingTask);
		}
	}

	private async Task StartProbingTask(Service service, IReadOnlyCollection<InternetAccessPoint> pointsToProbeVia, IServiceStatusTableRepository routeOutput, Action callback)
	{
		await Task.Yield();

		var start = _time.GetUtcNow().UtcDateTime;
		using var procedure = TrackableProcedure.New("Probing", start, _monitoringSystem);
		using var report = new ProbingTaskReport(procedure);
		report.Service = service;
		report.InternetAccessPoints = ReportProperty.Create(pointsToProbeVia);

		try
		{
			var probingMethod = service.Capability.Class.ProbingMethod;
			var endPoint = service.EndPoint;

			var driver = _probeDriverRegistry.ProvideDriver(probingMethod.Driver);
			report.DriverName = probingMethod.Driver;

			var materializedParameters = service.Capability.MaterializedProbingParameters;
			report.ProbingParameters = ReportProperty.Create(materializedParameters);


			var rawStatusTable = new Dictionary<InternetAccessPoint, ServiceIAPStatus>();
			foreach (var IAP in pointsToProbeVia)
			{
				var searchResult = GetAvailableChannel(IAP);
				if (searchResult is null)
					continue;
				// If failed to connect to IAP, there is no result (positive or negative)
				var (channelStatus, channel) = searchResult.Value;
				var channelDriver = _channelDriverRegistry.ProvideDriver(channel.DriverName);

				var rawStatus = await PerformProbeAsync(driver, channelDriver, IAP, channel, endPoint, materializedParameters, procedure);
				// Consider null in rawStatus as failed probe with no result (positive or negative)
				if (rawStatus is null)
					continue;

				// Subtract channel access time
				var correctedTime = rawStatus.Value.AverageProbeTime - channelStatus.AverageAccessTime;
				if (correctedTime < TimeSpan.Zero) correctedTime = TimeSpan.Zero;
				var status = rawStatus.Value with { AverageProbeTime = correctedTime };

				rawStatusTable.Add(IAP, status);
			}


			var statusTable = new ServiceStatusTable(service, rawStatusTable);
			report.Result = statusTable;

			routeOutput.UpdateTable(statusTable);
		}
		catch (Exception ex)
		{
			report.Exception = ex;
		}
		finally
		{
			callback();
		}
	}

	private async ValueTask<ServiceIAPStatus?> PerformProbeAsync(
		IProbeDriver driver,
		IChannelDriver channelDriver,
		InternetAccessPoint IAP,
		IAPChannel channel,
		WebEndPoint endPoint,
		IReadOnlyDictionary<string, string> materializedParameters,
		TrackableProcedure procedure
	)
	{
		using var report = new IAPProbingReport(procedure);
		report.InternetAccessPoint = IAP;
		report.ChannelName = IAP.InverseChannels[channel];
		var accMetric = TimeSpan.Zero;

		bool wasBanned = false, wasSuccess = false;

		int successInRow = 0;
		for (int index = 0; index < _options.MaxProbeCount; index++)
		{
			bool threatAsSuccessProbe;
			await using var openingResult = await channelDriver.TryOpenChannelAsync(channel, endPoint);
			if (openingResult.Type is ChannelOpeningResult.ResultType.Exception)
				return null;
			else if (openingResult.Type is ChannelOpeningResult.ResultType.ConnectionError)
				threatAsSuccessProbe = false;
			else // ChannelOpeningResult.ResultType.Success
			{
				var args = new ProbingArgs(openingResult.OpenChannel.GetFlow(), endPoint, materializedParameters, _options.ProbeOptions, _time, procedure);
				var probeResult = await driver.ProbeAsync(args);
				accMetric += probeResult.ProbeDuration;

				switch ((probeResult.L6Status, probeResult.L7Status))
				{
					case (CommunicationStatus.RemoteServerBan, CommunicationStatus.Skipped) or
						 (CommunicationStatus.Success, CommunicationStatus.UnexceptedData or CommunicationStatus.RemoteServerBan):
						wasBanned = true;
						threatAsSuccessProbe = true;
						break;

					case (CommunicationStatus.Success or CommunicationStatus.Skipped, CommunicationStatus.Success):
						wasSuccess = true;
						threatAsSuccessProbe = true;
						break;

					default:
						threatAsSuccessProbe = false;
						break;
				}
			}

			if (threatAsSuccessProbe)
				successInRow++;
			else successInRow = 0;
			if (successInRow == _options.RequiredSuccessProbeCount)
				goto success;
				
			var ableBecomeSucceed = (_options.MaxProbeCount - index - 1) >= _options.RequiredSuccessProbeCount - successInRow;
			if (ableBecomeSucceed == false)
				break;

			await Task.Delay(_options.ProbeInterval);
		}

		report.Result = ServiceIAPStatus.Blocked;
		return ServiceIAPStatus.Blocked;

	success:

		var type = (wasBanned, wasSuccess) switch
		{
			(true, false) => ServiceIAPStatus.StatusType.ServerSideBan,
			_ => ServiceIAPStatus.StatusType.Available,
		};
		var result = new ServiceIAPStatus(type, accMetric / _options.RequiredSuccessProbeCount);
		report.Result = result;
		return result;
	}

	private (IAPChannelStatus Status, IAPChannel Channel)? GetAvailableChannel(InternetAccessPoint IAP)
	{
		var currentIdentity = _networkIdentifier.CurrentIdentity;
		var profile = _networkProfileRepository.GetProfileForOrNull(currentIdentity);
		if (profile is null)
			return null;

		var bestChannelStatus = _channelPicker.PickBestChannel(IAP, profile, new IAPChannelRequirements());

		return bestChannelStatus;
	}


	public class Options
	{
		public TimeSpan ProbeInterval { get; init; } = TimeSpan.FromSeconds(3);

		public CommonProbeDriverOptions ProbeOptions { get; init; } = new();

		public int MaxProbeCount { get; init; } = 5;

		public int RequiredSuccessProbeCount { get; init; } = 3;
	}

	private class ProbingTask(Service service)
	{
		private Task? _task;


		public Service Service { get; } = service;

		public Task Task => _task ?? throw new InvalidOperationException("Task has not been set");


		public void SetTask(Task task)
		{
			_task = task;
		}
	}

	[SystemReportMetadata(nameof(IProbingManager), nameof(ProbingManager), "ProbingTask")]
	private class ProbingTaskReport(TrackableProcedure? autoBind = null) : SystemReport(autoBind)
	{
		public ReportProperty<Service> Service { get; set; }

		public ReportProperty<string> DriverName { get; set; }

		public ReportProperty<IReadOnlyCollection<InternetAccessPoint>> InternetAccessPoints { get; set; }

		public ReportProperty<IReadOnlyDictionary<string, string>> ProbingParameters { get; set; }

		public ReportProperty<ServiceStatusTable> Result { get; set; }

		public ReportProperty<Exception> Exception { get; set; }
	}

	[SystemReportMetadata(nameof(IProbingManager), nameof(ProbingManager), "IAPProbing")]
	private class IAPProbingReport(TrackableProcedure? autoBind = null) : SystemReport(autoBind)
	{
		public ReportProperty<InternetAccessPoint> InternetAccessPoint { get; set; }

		public ReportProperty<string> ChannelName { get; set; }

		public ReportProperty<ServiceIAPStatus> Result { get; set; }
	}
}
