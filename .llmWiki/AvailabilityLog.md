# AvailabilityLog

`AvailabilityLog` is a private nested class within `DefaultIAPChannelMonitor.cs` used to track and analyze the history of a channel's availability.

## Data Structures

- **`_intervals` (`List<LifeInterval>`)**: A list of completed availability periods. Each `LifeInterval` is a `readonly record struct` containing `Start` and `End` timestamps and a `Duration` property.
- **`_availabilityStart` (`DateTime`)**: Stores the start time of the currently active (open) availability period. If the channel is currently unavailable, this is set to `DateTime.MinValue`.
- **`_cutoffInterval` (`TimeSpan`)**: The duration of the historical window used for calculations.
- **`_time` (`TimeProvider`)**: Used to retrieve the current UTC time.

## The `Log(bool isAvailable)` Method

The `Log` method manages state transitions and maintains the interval list within a sliding window.

### Logic Flow

1.  **Cleanup**:
    *   Iterates through `_intervals` to remove intervals that have become invalid (where `Start > End`).
    *   Clips the `Start` of any interval that began before the `cutoffTime` to the `cutoffTime` to maintain the sliding window.
2.  **State Transition (Available)**:
    *   If moving from unavailable to available (`_availabilityStart == DateTime.MinValue`), it starts a new interval at `now`.
    *   If already available, it ensures `_availabilityStart` is at least at the `cutoffTime`.
3.  **State Transition (Unavailable)**:
    *   If moving from available to unavailable, it closes the current interval by adding a new `LifeInterval(_availabilityStart, now)` to the `_intervals` list and resets `_availabilityStart` to `DateTime.MinValue`.
4.  **Recalculation**:
    *   Triggers a recalculation of the `AverageLifeTime` property.

## Metrics: `AverageLifeTime`

The `AverageLifeTime` is calculated using a length-weighted average approach over the available intervals within the `_cutoffInterval`.

### Calculation Formula

The metric calculates the sum of the squares of the durations divided by the total duration of all intervals (including the currently open one):

$$\text{result} = \frac{\sum (\text{duration}_i^2)}{\sum \text{duration}_i}$$

The final result is clamped between `TimeSpan.Zero` and `_cutoffInterval`, and then divided by 2.

### Implementation Snippet

```csharp
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
```

## File Location

`C:\Users\GGJJ1\Nextcloud\Разработка\С# projects\Other\GammaRay\GammaRay.Core\InternetAccess\Channels\Testing\DefaultIAPChannelMonitor.cs`

## Known Issues & Bug Fixes

- **Bug**: When transitioning available→unavailable then back to available, the cutoffTime adjustment on `_availabilityStart` was lost on the second transition. **Fix**: Always use `Math.Max(now, cutoffTime)` when becoming available.

## Related Pages

- **[Sliding Window Availability Tracking](SlidingWindowAvailabilityTracking.md)** — Industry best practices, common bugs, and how major monitoring systems handle this.
