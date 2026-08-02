# DefaultIAPChannelMonitor

The `DefaultIAPChannelMonitor` class is used for monitoring internet access channel availability.

## File Path
`C:\Users\GGJJ1\Nextcloud\Разработка\С# projects\Other\GammaRay\GammaRay.Core\InternetAccess\Channels\Testing\DefaultIAPChannelMonitor.cs`

## Availability Logging Logic

The monitoring is handled by a nested `Worker` class.

### Mechanism
Uses a `WriteAheadQueue` (`_waq`) to store test results. When the `_waq` is full, the oldest result is displaced; if the channel is currently available, this displaced result is pushed into the `_observationRow` (long-term buffer). The `AvailabilityLog` is used to track the history of the channel's availability state.

### Trigger Mechanism
1. **Trigger**: The `Update()` method (called by the monitor's timer) calls `StartUpdate()`. `StartUpdate` performs an asynchronous network test via `PerformTestAsync`.
2. **State Determination (`_available`)**:
    - The `_available` boolean state is updated when the `WriteAheadQueue` (`_waq`) is full (`_waq.Buffer.IsFull`).
    - **Becoming Unavailable**: If successful tests in the full queue $\le 2$ and the state was previously `true`, `_available` is set to `false`.
    - **Becoming Available**: If successful tests in the full queue $\ge 4$ and the state was previously `false`, `_available` is set to `true`. To prevent immediate oscillation (flapping) caused by old failed tests still in the queue, `_ignoreWAQElements` is incremented by the number of consecutive failed tests found at the tail of the `_waq`.
3. **Logging**: `AvailabilityLog.Log(_available)` is called immediately after the state transition logic to record the timestamped change.

### Code Snippet (Worker.StartUpdate)

```csharp
private async void StartUpdate()
{
    _isUpdateRunning = true;

    var procedure = TrackableProcedure.New("Testing", _owner._timeProvider, _owner._monitoringSystem);
    try
    {
        var testResult = await PerformTestAsync(procedure);

        if (!CheckNetwork()) return;

        var displacedTestResult = _waq.Push(testResult);

        var successInWAQ = _waq.CountSuccessTests();
        if (_waq.Buffer.IsFull)
        {
            // Transition to UNAVAILABLE if success count is low
            if (successInWAQ is <= 2 && _available == true)
            {
                _available = false;
            }
            // Transition to AVAILABLE if success count is high
            else if (successInWAQ is >= 4 && _available == false)
            {
                _available = true;

                // Ignore all pending failed tests to prevent immediate oscillation
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
        }

        // Trigger the log update with the current state
        AvailabilityLog.Log(_available);

        Status = new IAPChannelStatus(
            _observationRow.CalculateQuantile(95),
            _observationRow.CalculateAverage(),
            _observationRow.CalculateAccessChance(),
            _available,
            _availabilityLog.AverageLifeTime
        );
    }
    // ...
}
```
