using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Utils;
using Microsoft.Extensions.Options;

namespace GammaRay.Core.Services.Probing;

public sealed class ProbingManager(
	IDriverRegistry<IProbeDriver> _probeDriverRegistry,
	IIAPChannelPicker _channelPicker,
	IChannelDriverRegistry _channelDriverRegistry,

	INetworkIdentifier _networkIdentifier,
	INetworkProfileMappingRepository _networkProfileRepository,

	IOptions<ProbingManager.Options> options
) : IProbingManager
{
	private readonly HashSet<ProbingTask> _tasks = [];
	private readonly Options _options = options.Value;


	public void StartProbing(Service service, IReadOnlyCollection<InternetAccessPoint> pointsToProbeVia, IServiceStatusTableRepository routeOutput)
	{
		if (_tasks.Any(s => s.Service == service) == false)
		{
			Console.WriteLine($"New probing for service on {service.EndPoint} via: {string.Join(", ", pointsToProbeVia.Select(s => s.Name))}");
			var probingTask = new ProbingTask(service);

			var task = StartProbingTask(service, pointsToProbeVia, routeOutput, () => _tasks.Remove(probingTask));
			probingTask.SetTask(task);

			_tasks.Add(probingTask);
		}
		else
		{
			Console.WriteLine($"Probing for service on {service.EndPoint} already running");
		}
	}

	private async Task StartProbingTask(Service service, IReadOnlyCollection<InternetAccessPoint> pointsToProbeVia, IServiceStatusTableRepository routeOutput, Action callback)
	{
		await Task.Yield();

		try
		{
			var probingMethod = service.Capability.Class.ProbingMethod;
			var endPoint = service.EndPoint;

			var driver = _probeDriverRegistry.ProvideDriver(probingMethod.Driver);

			var materializedParameters = service.Capability.MaterializedProbingParameters;


			var rawStatusTable = new Dictionary<InternetAccessPoint, ServiceIAPStatus>();
			foreach (var IAP in pointsToProbeVia)
			{
				var channelStatus = GetAvailableChannel(IAP);
				if (channelStatus is null)
					continue;
				// If failed to connect to IAP, there is no result (positive or negative)
				var channel = channelStatus.Channel;
				var channelDriver = _channelDriverRegistry.ProvideDriver(channel.DriverName);

				var rawStatus = await PerformProbeAsync(driver, channelDriver, channel, endPoint, materializedParameters);
				// Consider null in rawStatus as failed probe with no result (positive or negative)
				if (rawStatus is null)
					continue;

				// Subtract channel access time
				var status = rawStatus.Value.Match(channelStatus.AverageAccessTime, (AAT, raw) =>
					new ServiceIAPStatus(Math.Clamp(raw.AverageProbeTime - AAT, TimeSpan.Zero, TimeSpan.MaxValue)));

				rawStatusTable.Add(IAP, status);
			}


			var statusTable = new ServiceStatusTable(service, rawStatusTable);

			Console.WriteLine($"Probing for service on {service.EndPoint} completed with results:\n" +
				$"\t{string.Join(", ", rawStatusTable.Select(kv => $"{kv.Key.Name}={(kv.Value.IsAvailable ? kv.Value.AverageProbeTime.TotalMilliseconds.ToString() : "INF")}ms"))}");

			routeOutput.UpdateTable(statusTable);
		}
		catch (Exception)
		{
		
		}
		finally
		{
			callback();
		}
	}

	private async ValueTask<ServiceIAPStatus?> PerformProbeAsync(
		IProbeDriver driver,
		IChannelDriver channelDriver,
		IAPChannel channel,
		WebEndPoint endPoint,
		IReadOnlyDictionary<string, string> materializedParameters
	)
	{
		var accMetric = TimeSpan.Zero;

		int successInRow = 0;
		for (int index = 0; index < _options.MaxProbeCount; index++)
		{
			await using var openChannel = await channelDriver.TryOpenChannelAsync(channel, endPoint);
			if (openChannel is null)
				return null;

			var probeResult = await driver.ProbeAsync(openChannel.GetFlow(), endPoint, materializedParameters, _options.ProbeOptions);
			accMetric += probeResult.ProbeDuration;

			if (probeResult.Status is ProbeResult.ProbeStatus.Success)
			{
				successInRow++;
				if (successInRow == _options.RequiredSuccessProbeCount)
					goto success;
			}
			else
			{
				successInRow = 0;
			}

			var ableBecomeSucceed = (_options.MaxProbeCount - index - 1) >= _options.RequiredSuccessProbeCount - successInRow;
			if (ableBecomeSucceed == false)
				break;

			await Task.Delay(_options.ProbeInterval);
		}

		return ServiceIAPStatus.Unavailable;

	success:

		return new ServiceIAPStatus(accMetric / _options.RequiredSuccessProbeCount);
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
}
