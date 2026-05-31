using GammaRay.Core.Monitoring;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Utils;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GammaRay.Core.InternetAccess.Channels.Testing;

public sealed class DefaultIAPChannelMonitor(
	IIAPChannelStatusRepository _statusRepository,
	TimeProvider _timeProvider,

	INetworkIdentifier _networkIdentifier,
	INetworkProfileMappingRepository _networkProfileMapping,

	IIAPChannelSimpleTester _simpleChannelTester,
	InternetAccessPointProvider _internetAccessPointProvider,

	IOptions<DefaultIAPChannelMonitor.Options> options,
	IMonitoringSystem _monitoringSystem,

	IIAPChannelPicker _picker
) : IIAPChannelMonitor, IDisposable
{
	private readonly Options _options = options.Value;
	private readonly Dictionary<InternetAccessPoint, RunningFullTestInformation?> _runningTests = [];
	private ActivationContext? _activationContext = null;
	private bool _isUpdating = false;


	public void StartMonitoring()
	{
		var networkIdentifierSubscription = _networkIdentifier.SubscribeForChanges(NetworkIdentityChangeCallback);
		var updateTimer = _timeProvider.CreateTimer(UpdateTimerCallback, this, _options.UpdateTimerPeriod, _options.UpdateTimerPeriod);
		var capturedSynchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();

		_activationContext = new ActivationContext(networkIdentifierSubscription, updateTimer, capturedSynchronizationContext);

		Update();
	}

	public void Dispose()
	{
		if (_activationContext is null)
			return;

		_activationContext.NetworkIdentifierSubscription.Dispose();
		_activationContext.UpdateTimer.Dispose();
	}

	private void NetworkIdentityChangeCallback(INetworkIdentifier _) =>
		Update();

	private static void UpdateTimerCallback(object? state) =>
		(state as DefaultIAPChannelMonitor)?._activationContext?.CapturedSynchronizationContext.Post(static state =>
			(state as DefaultIAPChannelMonitor)?.Update(), state);

	private async void Update()
	{
		if (_isUpdating) return;

		try
		{
			_isUpdating = true;

			var currentIdentity = _networkIdentifier.CurrentIdentity;
			var currentProfile = _networkProfileMapping.GetProfileFor(currentIdentity);

			HashSet<Task>? tasks = null;
			foreach (var IAP in _internetAccessPointProvider.PlainRemoteInternetAccessPoints)
			{
				var task = updateForIAP(IAP, currentProfile);
				if (task.IsCompleted == false)
				{
					tasks ??= new();
					tasks.Add(task);
				}
				else await task;
			}

			if (tasks is not null)
				await Task.WhenAll(tasks);
		}
		catch (Exception ex) { Debugger.BreakForUserUnhandledException(ex); }
		finally
		{
			_isUpdating = false;
		}


		async Task updateForIAP(InternetAccessPoint IAP, NetworkProfile currentProfile)
		{
			var runningTest = _runningTests.GetValueOrDefault(IAP);

			if (runningTest is not null and { IsCompleted: false })
			{
				// If we are already performing a test and
				// the network profile did not change -> we should do nothing, as the test is still relevant
				// the network profile changed -> we should cancel the test, as it is not relevant anymore

				if (runningTest.PerformingInNetwork == currentProfile)
				{
					return;
				}
				else
				{
					// Canceling test without awaiting its tracking task
					// it will shutdown gracefully without side effects on shared state
					runningTest.Cancellation.Cancel();
				}
			}

			// Set information object to null to do not prevent GC from deleting it
			// We cannot do it in 'PerformFullChannelTest' because possible side effects
			if (runningTest is not null and { IsCompleted: true })
				runningTest = null;

			// Retest if: 1) result is too old, 2) continuous channel test failed
			bool shouldPerformFullTest = false;

			// Run CCT only when full test is not running
			if (_options.EnableContinuousChannelTesting)
			{
				var CCTResult = await PerformContinuousChannelTestAsync(IAP, currentProfile);
				if (CCTResult is (false, not null))
				{
					IEnumerable<IAPChannelStatus> newStatus = [
						new IAPChannelStatus(IAP, CCTResult.UsedChannelStatus.Channel,
							currentProfile, IAPChannelStatus.UnavailableAccessTime)
					];
					_statusRepository.UpdateStatuses(newStatus);
					shouldPerformFullTest = true;
				}
			}

			if (shouldPerformFullTest == false)
			{
				var now = _timeProvider.GetUtcNow().UtcDateTime;
				var lastUpdate = _statusRepository.GetLastStatusUpdateTime(currentProfile);
				shouldPerformFullTest = now - lastUpdate >= _options.StatusDecayTime;
			}

			if (shouldPerformFullTest)
			{
				runningTest = new RunningFullTestInformation(currentProfile, IAP);
				var task = PerformFullChannelTestAsync(runningTest);
				runningTest.TrackingTask = task;
				_runningTests[IAP] = runningTest;
			}
		}
	}

	private async Task PerformFullChannelTestAsync(RunningFullTestInformation testInformation)
	{
		await Task.Yield();
		var cancellationToken = testInformation.Cancellation.Token;
		using var context = new MonitoringContext("Testing", _timeProvider, _monitoringSystem);
		using var report = context.NewReport<Report>();
		report.PerformingInNetwork = testInformation.PerformingInNetwork;

		try
		{
			var workingProfile = testInformation.PerformingInNetwork;

			var baseLine = await getLocalBaseLineResult(workingProfile, cancellationToken);
			report.LocalBaseLine = baseLine;
			var outputStatusTable = new List<IAPChannelStatus>();

			var IAP = testInformation.IAP;

			foreach (var channel in IAP.Channels.Values)
			{
				var currentProfile = _networkProfileMapping.GetProfileFor(_networkIdentifier.CurrentIdentity);
				if (currentProfile != workingProfile)
				{
					testInformation.Cancellation.Cancel();
					return;
				}

				if (cancellationToken.IsCancellationRequested)
					return;

				if (channel.AvailableInNetwork.Contains(workingProfile) == false)
					return;

				var result = await PerformTestAsync(channel, cancellationToken);
				result = AdjustResult(result, baseLine);
				outputStatusTable.Add(new IAPChannelStatus(IAP, channel, workingProfile, result));
			}

			report.Result = outputStatusTable;

			_statusRepository.UpdateStatuses(outputStatusTable);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			report.Exception = ex;
		}
		finally
		{
			if (cancellationToken.IsCancellationRequested)
				report.WasInterrupted = true;
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

	private async ValueTask<(bool Success, IAPChannelStatus? UsedChannelStatus)> PerformContinuousChannelTestAsync(InternetAccessPoint IAP, NetworkProfile currentProfile)
	{
		var bestChannelStatus = _picker.PickBestChannel(IAP, currentProfile, new IAPChannelRequirements());
		if (bestChannelStatus is null)
			return (true, bestChannelStatus);
		var bestChannel = bestChannelStatus.Channel;

		for (var i = 0; i < 3; i++)
		{
			var testResult = await _simpleChannelTester.PerformTestAsync(bestChannel, default);
			if (testResult.Status == IAPChannelSimpleTestResult.TestStatus.Success)
				return (true, bestChannelStatus);
		}

		return (false, bestChannelStatus);
	}


	public sealed class Options
	{
		public TimeSpan StatusDecayTime { get; init; } = TimeSpan.FromHours(3);

		public TimeSpan UpdateTimerPeriod { get; init; } = TimeSpan.FromSeconds(5);

		public int SimpleTestCount { get; init; } = 5;

		public TimeSpan SimpleTestInterval { get; init; } = TimeSpan.FromSeconds(3);

		public bool EnableContinuousChannelTesting { get; init; } = true;
	}

	private sealed record ActivationContext(
		IDisposable NetworkIdentifierSubscription,
		ITimer UpdateTimer,
		SynchronizationContext CapturedSynchronizationContext
	);

	private sealed class RunningFullTestInformation(NetworkProfile performingInNetwork, InternetAccessPoint IAP)
	{
		public InternetAccessPoint IAP { get; } = IAP;

		public Task? TrackingTask { get; set; }

		public NetworkProfile PerformingInNetwork { get; } = performingInNetwork;

		public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();

		public bool IsCompleted { get; set; } = false;
	}

	public class Report() : SystemReport(nameof(DefaultIAPChannelMonitor))
	{
		public ReportProperty<NetworkProfile> PerformingInNetwork { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<TimeSpan> LocalBaseLine { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<IReadOnlyCollection<IAPChannelStatus>> Result { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<Exception> Exception { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<bool> WasInterrupted { get; set => SetProperty(ref field, value.Value); }
	}
}
