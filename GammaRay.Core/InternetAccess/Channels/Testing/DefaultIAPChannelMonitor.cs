using GammaRay.Core.Network.Identity;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Utils;
using Microsoft.Extensions.Options;

namespace GammaRay.Core.InternetAccess.Channels.Testing;

public sealed class DefaultIAPChannelMonitor(
	IIAPChannelStatusRepository _statusRepository,
	TimeProvider _timeProvider,

	INetworkIdentifier _networkIdentifier,
	INetworkProfileMappingRepository _networkProfileMapping,

	IIAPChannelSimpleTester _simpleChannelTester,
	InternetAccessPointProvider _internetAccessPointProvider,

	IOptions<DefaultIAPChannelMonitor.Options> options
) : IIAPChannelMonitor, IDisposable
{
	private readonly Options _options = options.Value;
	private RunningFullTestInformation? _runningTestInformation = null;
	private ActivationContext? _activationContext = null;


	public void StartMonitoring()
	{
		var networkIdentifierSubscription = _networkIdentifier.SubscribeForChanges(NetworkIdentityChangeCallback);
		var timer = _timeProvider.CreateTimer(TimerCallback, this, _options.UpdateTimerPeriod, _options.UpdateTimerPeriod);
		var capturedSynchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();

		_activationContext = new ActivationContext(networkIdentifierSubscription, timer, capturedSynchronizationContext);

		BeginFullChannelTestIfNeed();
	}

	public void Dispose()
	{
		if (_activationContext is null)
			return;

		_activationContext.NetworkIdentifierSubscription.Dispose();
		_activationContext.UpdateTimer.Dispose();
	}

	private void NetworkIdentityChangeCallback(INetworkIdentifier _) =>
		BeginFullChannelTestIfNeed();

	private static void TimerCallback(object? state) =>
		(state as DefaultIAPChannelMonitor)?._activationContext?.CapturedSynchronizationContext.Post(static state =>
			(state as DefaultIAPChannelMonitor)?.BeginFullChannelTestIfNeed(), state);

	private void BeginFullChannelTestIfNeed()
	{
		try
		{
			var currentIdentity = _networkIdentifier.CurrentIdentity;
			var currentProfile = _networkProfileMapping.GetProfileFor(currentIdentity);

			if (_runningTestInformation is not null and { IsCompleted: false })
			{
				// If we are already performing a test and
				// the network profile did not change -> we should do nothing, as the test is still relevant
				// the network profile changed -> we should cancel the test, as it is not relevant anymore

				if (_runningTestInformation.PerformingInNetwork == currentProfile)
				{
					return;
				}
				else
				{
					// Canceling test without awaiting its tracking task
					// it will shutdown gracefully without side effects on shared state
					_runningTestInformation.Cancellation.Cancel();
				}
			}

			// Set information object to null to do not prevent GC from deleting it
			// We cannot do it in 'PerformFullChannelTest' because possible side effects
			if (_runningTestInformation is not null and { IsCompleted: true })
				_runningTestInformation = null;

			var now = _timeProvider.GetUtcNow().Date;
			var lastUpdate = _statusRepository.GetLastStatusUpdateTime(currentProfile);

			if (now - lastUpdate >= _options.StatusDecayTime) // Status too old, retest it
			{
				_runningTestInformation = new RunningFullTestInformation(currentProfile);
				var task = PerformFullChannelTest(_runningTestInformation);
				_runningTestInformation.TrackingTask = task;
			}
		}
		catch (Exception) { }
	}

	private async Task PerformFullChannelTest(RunningFullTestInformation testInformation)
	{
		await Task.Yield();
		try
		{
			var cancellationToken = testInformation.Cancellation.Token;
			var currentProfile = testInformation.PerformingInNetwork;

			var baseLine = await getLocalBaseLineResult(currentProfile, cancellationToken);
			var outputStatusTable = new List<IAPChannelStatus>();

			foreach (var IAP in _internetAccessPointProvider.PlainRemoteInternetAccessPoints)
			{
				foreach (var channel in IAP.Channels.Values)
				{
					if (cancellationToken.IsCancellationRequested)
						return;

					if (channel.AvailableInNetwork.Contains(currentProfile) == false)
						return;

					var result = await PerformTestAsync(channel, cancellationToken);
					result = AdjustResult(result, baseLine);
					outputStatusTable.Add(new IAPChannelStatus(IAP, channel, currentProfile, result));
				}
			}

			await _statusRepository.UpdateStatusesAsync(outputStatusTable);
		}
		catch (Exception) { }
		finally
		{
			testInformation.IsCompleted = true;
		}


		async ValueTask<TimeSpan> getLocalBaseLineResult(
			NetworkProfile currentProfile,
			CancellationToken cancellationToken
		)
		{
			var currentLocalIAP = _internetAccessPointProvider.LocalInternetAccessPointsByProfile[currentProfile];
			var currentLocalChannel = currentLocalIAP.Channels[InternetAccessPointProvider.LocalIAPChannelName];

			return await PerformTestAsync(currentLocalChannel, cancellationToken);
		}

		static TimeSpan AdjustResult(TimeSpan rawResult, TimeSpan baseLine)
		{
			if (rawResult == IAPChannelStatus.UnavailableAccessTime)
				return rawResult;
			return Math.Clamp(rawResult - baseLine, TimeSpan.Zero, TimeSpan.MaxValue);
		}
	}

	private async ValueTask<TimeSpan> PerformTestAsync(IAPChannel channel, CancellationToken cancellationToken)
	{
		var totalDuration = TimeSpan.Zero;

		for (int i = 0; i < _options.SimpleTestCount; i++)
		{
			var testResult = await _simpleChannelTester.PerformTestAsync(channel, cancellationToken);
			if (testResult.Status != IAPChannelSimpleTestResult.TestStatus.Success)
				return IAPChannelStatus.UnavailableAccessTime;
			totalDuration += testResult.TestDuration;

			cancellationToken.ThrowIfCancellationRequested();
			await Task.Delay(_options.SimpleTestInterval, cancellationToken);
		}

		return totalDuration / _options.SimpleTestCount;
	}


	public sealed class Options
	{
		public TimeSpan StatusDecayTime { get; init; } = TimeSpan.FromHours(3);

		public TimeSpan UpdateTimerPeriod { get; init; } = TimeSpan.FromMinutes(10);

		public int SimpleTestCount { get; init; } = 5;

		public TimeSpan SimpleTestInterval { get; init; } = TimeSpan.FromSeconds(3);
	}

	private sealed record ActivationContext(
		IDisposable NetworkIdentifierSubscription,
		ITimer UpdateTimer,
		SynchronizationContext CapturedSynchronizationContext
	);

	private sealed class RunningFullTestInformation(NetworkProfile performingInNetwork)
	{
		public Task? TrackingTask { get; set; }

		public NetworkProfile PerformingInNetwork { get; } = performingInNetwork;

		public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();

		public bool IsCompleted { get; set; } = false;
	}
}
