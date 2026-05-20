using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Utils;
using Microsoft.Extensions.Options;
using static GammaRay.Core.Services.Probing.ProbeResult;

namespace GammaRay.Core.Services.Probing;

public sealed class ProbingManager(
	IDriverRegistry<IProbeDriver> _probeDriverRegistry,
	IIAPChannelPicker _channelPicker,
	IChannelDriverRegistry _channelDriverRegistry,

	INetworkIdentifier _networkIdentifier,
	INetworkProfileMappingRepository _networkProfileRepository,

	IMonitoringSystem _monitoringSystem,
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
		using var context = new MonitoringContext("Probing", start, _monitoringSystem);
		using var report = context.NewReport<ProbingTaskReport>();
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
				var channelStatus = GetAvailableChannel(IAP);
				if (channelStatus is null)
					continue;
				// If failed to connect to IAP, there is no result (positive or negative)
				var channel = channelStatus.Channel;
				var channelDriver = _channelDriverRegistry.ProvideDriver(channel.DriverName);

				var rawStatus = await PerformProbeAsync(driver, channelDriver, IAP, channel, endPoint, materializedParameters, context);
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
		MonitoringContext monitoringContext
	)
	{
		using var report = monitoringContext.NewReport<IAPProbingReport>();
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
				var args = new ProbingArgs(openingResult.OpenChannel.GetFlow(), endPoint, materializedParameters, _options.ProbeOptions, _time, monitoringContext);
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

	private IAPChannelStatus? GetAvailableChannel(InternetAccessPoint IAP)
	{
		var currentIdentity = _networkIdentifier.CurrentIdentity;
		var profile = _networkProfileRepository.GetProfileFor(currentIdentity);

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

	private class ProbingTaskReport() : SystemReport($"{nameof(ProbingManager)}/ProbingTask")
	{
		public ReportProperty<Service> Service { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<string> DriverName { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<IReadOnlyCollection<InternetAccessPoint>> InternetAccessPoints { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<IReadOnlyDictionary<string, string>> ProbingParameters { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<ServiceStatusTable> Result { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<Exception> Exception { get; set => SetProperty(ref field, value.Value); }
	}

	private class IAPProbingReport() : SystemReport($"{nameof(ProbingManager)}/IAPProbing")
	{
		public ReportProperty<InternetAccessPoint> InternetAccessPoint { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<string> ChannelName { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<ServiceIAPStatus> Result { get; set => SetProperty(ref field, value.Value); }
	}
}
