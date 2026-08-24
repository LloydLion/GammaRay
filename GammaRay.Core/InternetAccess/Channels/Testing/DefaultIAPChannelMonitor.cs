using GammaRay.Core.Monitoring;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Network.Profiles;
using GammaRay.Core.Utils;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.InternetAccess.Channels.Testing;

public sealed class DefaultIAPChannelMonitor : IIAPChannelMonitor, IDisposable
{
	private static readonly ArrayPool<TimeSpan> Pool = ArrayPool<TimeSpan>.Create();
	private readonly Dictionary<NetworkProfile, Dictionary<(InternetAccessPoint IAP, IAPChannel Channel), Worker>> _channelWorkers;
	private readonly IIAPChannelObservedDataRepository _dataRepository;
	private readonly TimeProvider _timeProvider;
	private readonly INetworkIdentifier _networkIdentifier;
	private readonly INetworkProfileMappingRepository _networkProfileMapping;
	private readonly IIAPChannelSimpleTester _simpleChannelTester;
	private readonly IIAPChannelHardTester _hardChannelTester;
	private readonly MonitoringSystem _monitoringSystem;
	private readonly Options _options;
	private ActivationContext? _act;
	private long _lastRepositorySave = 0;


	public DefaultIAPChannelMonitor(
		IIAPChannelObservedDataRepository dataRepository,
		TimeProvider timeProvider,

		NetworkProfileProvider networkProfiles,
		INetworkIdentifier networkIdentifier,
		INetworkProfileMappingRepository networkProfileMapping,

		IIAPChannelSimpleTester simpleChannelTester,
		IIAPChannelHardTester hardChannelTester,
		InternetAccessPointProvider internetAccessPointProvider,

		IOptions<Options> options,
		MonitoringSystem monitoringSystem
	)
	{
		_dataRepository = dataRepository;
		_timeProvider = timeProvider;
		_networkIdentifier = networkIdentifier;
		_networkProfileMapping = networkProfileMapping;
		_simpleChannelTester = simpleChannelTester;
		_hardChannelTester = hardChannelTester;
		_monitoringSystem = monitoringSystem;
		_options = options.Value;
		_channelWorkers =
			networkProfiles.PlainProfiles.ToDictionary(p => p, profile => internetAccessPointProvider
				.PlainRemoteInternetAccessPoints
				.SelectMany(IAP => IAP.Channels.Values.Select(channel => (IAP, channel)))
				.ToDictionary(s => s, s => new Worker(s, profile, this))
			);
	}


	private ActivationContext Activation => _act ?? throw new InvalidOperationException($"Start monitoring first using {nameof(StartMonitoring)}() method");


	public void StartMonitoring()
	{
		var updateTimer = _timeProvider.CreateTimer(
			(_) => Activation.CapturedSynchronizationContext.Post((_) => Update(), null), null,
			Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan
		);

		var sync = SynchronizationContext.Current ?? new();

		_act = new ActivationContext(updateTimer, sync);

		_lastRepositorySave = _timeProvider.GetTimestamp();
		foreach (var (network, perNetWorkers) in _channelWorkers)
			foreach (var ((IAP, channel), worker) in perNetWorkers)
			{
				var data = _dataRepository.TryGetObservedData(IAP, channel, network);
				if (data is not null)
					worker.ImportObservedData(data);
			}

		updateTimer.Change(_options.UpdatePeriod, _options.UpdatePeriod);
	}

	public IAPChannelStatus GetStatus(InternetAccessPoint IAP, IAPChannel channel, NetworkProfile profile)
	{
		return _channelWorkers[profile][(IAP, channel)].Status;
	}

	public void Dispose()
	{
		Activation.UpdateTimer.Dispose();
		foreach (var perNetWorkers in _channelWorkers.Values)
			foreach (var worker in perNetWorkers.Values)
				worker.Dispose();
	}

	private void Update()
	{
		var networkProfile = _networkProfileMapping.GetProfileForOrNull(_networkIdentifier.CurrentIdentity);
		if (networkProfile is null)
			return;

		foreach (var worker in _channelWorkers[networkProfile].Values)
			worker.Update();


		var privilegedChannel = _channelWorkers[networkProfile].Values.MinBy(s => s.Status.CharacteristicAccessTime);
		privilegedChannel?.GrantPrivilege();


		var now = _timeProvider.GetTimestamp();
		if (_timeProvider.GetElapsedTime(_lastRepositorySave, now) >= _options.RepositorySavePeriod)
		{
			_lastRepositorySave = now;
			foreach (var (network, perNetWorkers) in _channelWorkers)
				foreach (var ((IAP, channel), worker) in perNetWorkers)
					_dataRepository.SaveObservedData(IAP, channel, network, worker.ExportObservedData());
		}
	}


	public class Options
	{
		public TimeSpan UpdatePeriod { get; init; } = TimeSpan.FromSeconds(5);

		public int WAQSize { get; init; } = 3;
		
		public int WAQSuccessToAdmitAvailable { get; init; } = 3;
		
		public int WAQSuccessToAdmitUnavailable { get; init; } = 1;

		public TimeSpan TestResultTTL { get; init; } = TimeSpan.FromMinutes(20);

		public TimeSpan RepositorySavePeriod { get; init; } = TimeSpan.FromMinutes(10);

		public int UnavailableChannelUpdatePeriodMultiplier { get; init; } = 10;

		public int UnprivilegedChannelUpdatePeriodMultiplier { get; init; } = 4;

		public TimeSpan TestTimeout { get; init; } = TimeSpan.FromSeconds(5);

		public TimeSpan AvailabilityLogCutOff { get; init; } = TimeSpan.FromHours(1);

		public TimeSpan HardTestMinInterval { get; init; } = TimeSpan.FromMinutes(10);
	}

	private record ActivationContext(ITimer UpdateTimer, SynchronizationContext CapturedSynchronizationContext);

	private class Worker((InternetAccessPoint IAP, IAPChannel Channel) _channel, NetworkProfile _network, DefaultIAPChannelMonitor _owner) : IDisposable
	{
		private readonly WriteAheadQueue _waq = new(_owner._options.WAQSize);
		private readonly ObservationRow _observationRow = new(_owner._options.TestResultTTL, _owner._timeProvider);
		private readonly TimeoutHandle _timeoutHandle = new(_owner._timeProvider);
		private AvailabilityLog _availabilityLog = new(_owner._timeProvider, _owner._options.AvailabilityLogCutOff, false);
		private bool _isUpdateRunning = false;
		private bool _available = false;
		private int _ignoreWAQElements = 0;
		private int _skippedUpdatesCounter = 0;
		private bool _privileged = false;
		private long _hardTestCacheExpireTime = 0;
		private bool _lastHardTestResult = false;


		public IAPChannelStatus Status { get; private set; }

		public WriteAheadQueue WAQ => _waq;

		public ObservationRow ObservationRow => _observationRow;

		public AvailabilityLog AvailabilityLog => _availabilityLog;


		public void Update()
		{
			// Update skip logic:
			// WAQ is not full? -> no skips
			// Not available? -> update once per UnavailableChannelUpdatePeriodMultiplier
			// Available but not privileged by monitor? -> update once per UnprivilegedChannelUpdatePeriodMultiplier
			// Available and privileged -> no skips
			//
			// If previous update is still running update will be skipped without compensation
			//

			_skippedUpdatesCounter++;
			if (WAQ.Buffer.IsFull)
			{
				if (_available == false)
				{
					if (_skippedUpdatesCounter < _owner._options.UnavailableChannelUpdatePeriodMultiplier)
						return;
				}
				if (_privileged == false)
				{
					if (_skippedUpdatesCounter < _owner._options.UnprivilegedChannelUpdatePeriodMultiplier)
						return;
				}
			}

			_skippedUpdatesCounter = 0;
			_privileged = false;

			if (_isUpdateRunning)
				return;

			StartUpdate();
		}

		public IAPChannelObservedData ExportObservedData()
		{
			var observationRowExport = new TimeSpan[_observationRow.ValidityLength];
			for (int i = 0; i < _observationRow.ValidityLength; i++)
				observationRowExport[i] = _observationRow.Buffer[i].AccessTime ?? TimeSpan.MaxValue;
			return new IAPChannelObservedData { ObservationRow = observationRowExport, IsAvailable = _available };
		}

		public void ImportObservedData(IAPChannelObservedData observedData)
		{
			var now = _owner._timeProvider.GetTimestamp();

			_available = observedData.IsAvailable;
			_availabilityLog = new AvailabilityLog(_owner._timeProvider, _owner._options.AvailabilityLogCutOff, _available);

			var observationRowExport = observedData.ObservationRow;
			for (int i = 0; i < observationRowExport.Length; i++)
			{
				var time = observationRowExport[i];
				_observationRow.Push(new TestResult(time == TimeSpan.MaxValue ? null : time, now));
			}
		}

		public void GrantPrivilege() => _privileged = true;

		private async void StartUpdate()
		{
			_isUpdateRunning = true;

			var procedure = TrackableProcedure.New("Testing", _owner._timeProvider, _owner._monitoringSystem);
			try
			{
				var testResult = await PerformTestAsync(procedure);

				if (!CheckNetwork()) return;

				var displacedTestResult = _waq.Push(testResult);
				if (displacedTestResult is not null && _available)
				{
					if (_ignoreWAQElements > 0)
					{
						_ignoreWAQElements--;
					}
					else
					{
						_observationRow.Push(displacedTestResult.Value);
					}
				}
				
				var successInWAQ = _waq.CountSuccessTests();
				
				var availabilityDecision = MakeAvailabilityDecision(successInWAQ);
				switch (availabilityDecision)
				{
					case AvailabilityDecision.TryMakeAvailable:
						if (await CheckIfCanMakeAvailableAsync(procedure))
						{
							IgnoreAllPendingFailedTests();
							_available = true;
						}
						break;
					case AvailabilityDecision.MakeUnavailable:
						_available = false;
						break;
				}

				AvailabilityLog.Log(_available);

				Status = new IAPChannelStatus(
					_observationRow.CalculateQuantile(95),
					_observationRow.CalculateAverage(),
					_observationRow.CalculateAccessChance(),
					_available,
					_availabilityLog.AverageLifeTime
				);

				procedure.CommitReport(new Report
				{
					InternetAccessPoint = _channel.IAP,
					ChannelName = _channel.IAP.InverseChannels[_channel.Channel],
					NetworkProfile = _network,
					WAQSuccessCount = successInWAQ,
					ObservationRowLength = _observationRow.ValidityLength,
					NewStatus = Status
				});

			}
			catch (Exception ex)
			{
				Debugger.BreakForUserUnhandledException(ex);
				procedure.SetFatalException(ex);
			}
			finally
			{
				procedure.Finish();
				_isUpdateRunning = false;
			}
		}

		public void Dispose()
		{
			_timeoutHandle.Dispose();
		}

		private bool CheckNetwork() => _owner._networkProfileMapping.GetProfileForOrNull(_owner._networkIdentifier.CurrentIdentity) == _network;

		private AvailabilityDecision MakeAvailabilityDecision(int successInWAQ)
		{
			if (_waq.Buffer.IsFull == false)
				return AvailabilityDecision.DoNotChange;
			
			if (successInWAQ <= _owner._options.WAQSuccessToAdmitUnavailable && _available == true)
				return AvailabilityDecision.MakeUnavailable;
			if (successInWAQ >= _owner._options.WAQSuccessToAdmitAvailable && _available == false)
				return AvailabilityDecision.TryMakeAvailable;

			return AvailabilityDecision.DoNotChange;
		}

		private async ValueTask<TestResult> PerformTestAsync(TrackableProcedure monitoring)
		{
			var start = _owner._timeProvider.GetTimestamp();
			bool success = false;
			try
			{
				success = await _timeoutHandle.DoAsyncOperationWithTimeout(
					_owner._options.TestTimeout,
					(tester: _owner._simpleChannelTester, channel: _channel.Channel, monitoring),
					(a, cancel) => a.tester.PerformTestAsync(a.channel, cancel, a.monitoring)
				);
			}
			catch (Exception)
			{ }

			var duration = _owner._timeProvider.GetElapsedTime(start);

			return new TestResult(success ? duration : null, start);
		}

		private async ValueTask<bool> CheckIfCanMakeAvailableAsync(TrackableProcedure monitoring)
		{
			var now = _owner._timeProvider.GetTimestamp();
			bool hardTestResult;
			if (_hardTestCacheExpireTime < now)
			{
				hardTestResult = false;
				try
				{
					hardTestResult = await _owner._hardChannelTester.PerformTestAsync(_channel.Channel, monitoring);
				}
				catch { }

				_lastHardTestResult = hardTestResult;
				_hardTestCacheExpireTime = now + (long)(_owner._timeProvider.TimestampFrequency * _owner._options.HardTestMinInterval.TotalSeconds);
			}
			else hardTestResult = _lastHardTestResult;

			return hardTestResult;
		}

		private void IgnoreAllPendingFailedTests()
		{
			var addToWAQIgnore = 0;
            for (int i = 1; i <= _waq.Buffer.Size; i++)
            {
            	var test = _waq.Buffer[-i];
            	if (test.IsSuccess == false)
            		addToWAQIgnore++;
            	else break;
            }

            _ignoreWAQElements += addToWAQIgnore;
		}

		private enum AvailabilityDecision
		{
			DoNotChange,
			MakeUnavailable,
			TryMakeAvailable
		}
	}

	private class WriteAheadQueue(int size)
	{
		private readonly RingBuffer<TestResult> _buffer = new(size);


		public RingBuffer<TestResult> Buffer => _buffer;


		public TestResult? Push(TestResult value)
		{
			return _buffer.Push(value);
		}

		public int CountSuccessTests()
		{
			var successTests = 0;
			foreach (var item in _buffer.FirstUsedPart)
				if (item.IsSuccess)
					successTests++;
			foreach (var item in _buffer.SecondUsedPart)
				if (item.IsSuccess)
					successTests++;
			return successTests;
		}

		public override string ToString() => string.Create(_buffer.Size, _buffer, (span, _buffer) =>
		{
			span.Fill('-');
			for (int i = 0; i < _buffer.Used; i++)
				span[i] = _buffer[i].IsSuccess ? 'S' : 'F';
		});
	}

	private class ObservationRow(TimeSpan _TTL, TimeProvider _time)
	{
		private RingBuffer<TestResult> _buffer = new(128);
		private int _validityLength = 0;


		public RingBuffer<TestResult> Buffer => _buffer;

		public int ValidityLength => _validityLength;


		public void Push(TestResult value)
		{
			var now = _time.GetTimestamp();

			var nextDisplacementCandidate = _buffer.GetNextDisplacementCandidate();
			if (nextDisplacementCandidate is not null)
				if (_time.GetElapsedTime(nextDisplacementCandidate.Value.Timestamp, now) < _TTL)
					_buffer = _buffer.Resize(_buffer.Size * 2);

			_buffer.Push(value);

			_validityLength = 0;
			for (int i = _buffer.Used - 1; i >= 0; i--)
			{
				var isValid = _time.GetElapsedTime(_buffer[i].Timestamp, now) < _TTL;
				if (isValid)
				{
					_validityLength = i + 1;
					break;
				}
			}
		}

		public TimeSpan CalculateQuantile(int percent)
		{
			if (_validityLength == 0)
				return TimeSpan.MaxValue;
			var orderedBuffer = Pool.Rent(_validityLength);
			try
			{
				var usedInBuffer = 0;
				for (int i = 0; i < _validityLength; i++)
				{
					var item = _buffer[i];
					if (item.IsSuccess)
						orderedBuffer[usedInBuffer++] = item.AccessTime.Value;
				}

				var orderedBufferPart = orderedBuffer.AsSpan(..usedInBuffer);
				orderedBufferPart.Sort();

				if (orderedBufferPart.Length == 0)
					return TimeSpan.MaxValue;

				var index = (int)(percent / 100.0 * orderedBufferPart.Length);
				if (index >= orderedBufferPart.Length)
					return orderedBufferPart[^1];
				return orderedBufferPart[index];
			}
			finally
			{
				Pool.Return(orderedBuffer);
			}
		}

		public double CalculateAccessChance()
		{
			if (_validityLength == 0)
				return 0.0;
			int successNumber = 0;
			for (int i = 0; i < _validityLength; i++)
				if (_buffer[i].IsSuccess)
					successNumber++;

			return successNumber / (double)_validityLength;
		}

		public TimeSpan CalculateAverage()
		{
			if (_validityLength == 0)
				return TimeSpan.MaxValue;
			var sumTime = TimeSpan.Zero;
			var count = 0;
			for (int i = 0; i < _validityLength; i++)
			{
				var item = _buffer[i];
				if (item.IsSuccess)
					(count, sumTime) = (count + 1, sumTime + item.AccessTime.Value);
			}
			return sumTime / count;
		}
	}

	private class AvailabilityLog
	{
		private readonly TimeProvider _time;
		private readonly TimeSpan _cutoffInterval;
		private readonly List<LifeInterval> _intervals = new();
		/// <summary>
		/// Start of current (open) availability interval. If the channel is currently unavailable, this value is DateTime.MinValue.
		/// </summary>
		private DateTime _availabilityStart;


		public AvailabilityLog(TimeProvider time, TimeSpan cutoffInterval, bool initialAvailability)
		{
			var now = time.GetUtcNow().UtcDateTime;
			_time = time;
			_cutoffInterval = cutoffInterval;

			// If channel is available initially, we assume that it was available for infinite time, but we only work with last 'cutoffInterval' time.
			_availabilityStart = initialAvailability ? (now - cutoffInterval) : DateTime.MinValue;
			AverageLifeTime = CalculateAverageLifeTime(now);
		}


		public List<LifeInterval> Intervals => _intervals;

		public TimeSpan AverageLifeTime { get; private set; }


		public void Log(bool isAvailable)
		{
			var now = _time.GetUtcNow().UtcDateTime;
			var cutoffTime = now - _cutoffInterval;
			for (int i = 0; i < _intervals.Count; i++)
			{
				var interval = _intervals[i];
				if (interval.Start <= cutoffTime)
					_intervals[i] = interval with { Start = cutoffTime };

				if (interval.Start > interval.End)
				{
					_intervals.RemoveAt(i);
					i--;
				}
			}


			if (isAvailable)
			{
				if (_availabilityStart == DateTime.MinValue)
				{
					_availabilityStart = now;
				}
				else
				{
					if (_availabilityStart < cutoffTime)
						_availabilityStart = cutoffTime;
				}
			}
			else
			{
				if (_availabilityStart != DateTime.MinValue)
				{
					if (_availabilityStart <= cutoffTime)
						_availabilityStart = cutoffTime;

					_intervals.Add(new LifeInterval(_availabilityStart, now));
					_availabilityStart = DateTime.MinValue;
				}
			}

			AverageLifeTime = CalculateAverageLifeTime(now);
		}

		private TimeSpan CalculateAverageLifeTime(DateTime now)
		{
			var openIntervalLength = GetOpenIntervalLength(now);

			if (openIntervalLength == TimeSpan.Zero && _intervals.Count == 0)
				return TimeSpan.Zero;

			var sumDuration = openIntervalLength;
			foreach (var interval in _intervals)
				sumDuration += interval.Duration;

			var result = openIntervalLength / sumDuration * openIntervalLength;
			foreach (var interval in _intervals)
				result += interval.Duration / sumDuration * interval.Duration;

			return Math.Clamp(result, TimeSpan.Zero, _cutoffInterval) / 2;
		}

		private TimeSpan GetOpenIntervalLength(DateTime now)
		{
			return _availabilityStart == DateTime.MinValue ? TimeSpan.Zero : Math.Min(_cutoffInterval, now - _availabilityStart);
		}


		public readonly record struct LifeInterval(DateTime Start, DateTime End)
		{
			public TimeSpan Duration => End - Start;
		}
	}

	private readonly record struct TestResult(TimeSpan? AccessTime, long Timestamp)
	{
		[MemberNotNullWhen(true, nameof(AccessTime))]
		public bool IsSuccess => AccessTime is not null;
	}

	[SystemReportMetadata(nameof(IIAPChannelMonitor), nameof(DefaultIAPChannelMonitor), "Update")]
	public class Report() : SystemReport()
	{
		public ReportProperty<InternetAccessPoint> InternetAccessPoint { get; set; }

		public ReportProperty<string> ChannelName { get; set; }

		public ReportProperty<NetworkProfile> NetworkProfile { get; set; }

		public ReportProperty<int> WAQSuccessCount { get; set; }

		public ReportProperty<int> ObservationRowLength { get; set; }

		public ReportProperty<IAPChannelStatus> NewStatus { get; set; }
	}
}
