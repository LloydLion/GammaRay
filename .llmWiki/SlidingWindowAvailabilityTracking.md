# Sliding Window Availability Tracking — Best Practices

## Overview

Sliding window availability tracking is a common pattern in monitoring systems for computing uptime metrics over a rolling time period. This page consolidates industry patterns, formulas, and common pitfalls.

---

## 1. Calculating "Average Uptime Duration" with a Sliding Window

### Pattern Name: **Length-Weighted Average (LWA)**

The most common approach is a **length-weighted average** over completed availability intervals within the cutoff window.

#### Formula

```
result = Σ(duration_i²) / Σ(duration_i)
```

Then typically:
- Clamp the result to `[0, cutoffInterval]`
- Divide by 2 (to produce a conservative estimate)

#### Why length-weighted instead of simple average?

A simple arithmetic mean `Σ(duration_i) / count` treats every interval equally regardless of length. A short 1-second outage and a long 1-hour outage get equal weight. The length-weighted average biases toward longer intervals, which better represents the "typical" experience — a user is more likely to encounter a long interval than a short one.

#### Reference Implementations

| System | Approach |
|--------|----------|
| **GammaRay** | LWA over `_intervals` list + open interval, clamped, /2 |
| **Prometheus** (`up_over_time`) | Proportion of time series where `up == 1` within range |
| **Datadog** | Bucket-based aggregation: time-weighted mean of up/down states |
| **AWS CloudWatch** | Simple percentage: `up_seconds / total_seconds` in window |

### Typical Implementation Steps

```csharp
public class AvailabilityTracker
{
    private readonly List<LifeInterval> _completedIntervals;
    private readonly TimeSpan _cutoffWindow;
    
    public TimeSpan AverageUptimeDuration(DateTime now)
    {
        var openDuration = _isCurrentlyAvailable
            ? now - _availabilityStart
            : TimeSpan.Zero;

        // Step 1: Filter intervals within cutoff
        var cutoffTime = now - _cutoffWindow;
        var validIntervals = _completedIntervals
            .Where(i => i.Start < now)  // Exclude future
            .ToList();

        // Step 2: Truncate intervals to window boundary
        foreach (var interval in validIntervals)
        {
            var clippedStart = Math.Max(interval.Start, cutoffTime);
            if (clippedStart < interval.End)
            {
                // Recalculate with clipped duration
            }
        }

        // Step 3: Compute length-weighted average
        // Σ(duration²) / Σ(duration)
    }
}
```

---

## 2. Common Bugs When State Doesn't Change

### Bug #1: Stale open interval at window edge

**Symptom**: The open interval (`_availabilityStart`) is set very early and never updated when the state stays "available." After the cutoff window passes, the open interval's effective duration gets incorrectly calculated.

**Root cause**: The code only updates `_availabilityStart` on state *transitions* (available→unavailable or vice versa). If the channel stays available for hours, `_availabilityStart` remains at the original timestamp.

**Fix**: On each `Log(true)` call, ensure `_availabilityStart = Math.Max(_availabilityStart, cutoffTime)`. This clips the open interval's start to the window boundary.

### Bug #2: Transition loses cutoff adjustment

**Symptom**: When transitioning available→unavailable (setting `_availabilityStart = cutoffTime`), then immediately transitioning back to available (setting `_availabilityStart = now`), the cutoff adjustment is lost.

**Root cause**: The available-state handler blindly sets `_availabilityStart = now` without considering the cutoff.

**Fix**: Always use `_availabilityStart = Math.Max(now, cutoffTime)` when becoming available.

### Bug #3: Double-counting at the cutoff boundary

**Symptom**: The truncated tail of an interval and the first full interval in the window overlap at the cutoff time, causing double-counted duration.

**Root cause**: An interval like `[t-10m, t-5m]` gets clipped to `[t-5m, t-5m]` (duration 0), but the next interval `[t-5m, t-3m]` also starts at t-5m. Both "exist" at the cutoff point.

**Fix**: When clipping, use strict inequality for the next interval's start, or deduplicate intervals with the same start time.

### Bug #4: Not handling the case where ALL intervals are outside the window

**Symptom**: The sliding window has advanced past all recorded intervals, but the code doesn't reset properly, returning stale metrics.

**Root cause**: The cleanup loop only removes intervals where `Start > End` (invalid), but doesn't remove intervals where `End < cutoffTime` (expired).

**Fix**: Add explicit filtering: remove any interval where `End <= cutoffTime`.

---

## 3. Handling the "Open Interval" at the Edge of a Sliding Window

### Pattern: **Clip-and-Include**

The standard approach is:

1. **Clip** the open interval's effective start to `max(_availabilityStart, cutoffTime)`
2. **Include** it in the calculation with its clipped duration
3. **Never store** the open interval in `_intervals` — it's computed on-the-fly

### Sentinel Values

| Value | Usage | Pros | Cons |
|-------|-------|------|------|
| `DateTime.MinValue` | "Not currently available" | Clear semantics | Can leak into calculations if not checked |
| `DateTime.UnixEpoch` | Unix-time systems | Compact | Same issues as MinValue |
| `Nullable<DateTime>` | Modern C# (`DateTime?`) | Type-safe nullability | Requires nullable checks everywhere |

### How Prometheus handles it

Prometheus doesn't use explicit intervals. The `up` metric is a gauge (1 = up, 0 = down) sampled at regular intervals. The sliding window is computed by `range queries`:

```promql
# Fraction of time series that are UP over the last 5m
avg_over_time(up[5m])
```

The "open interval" is implicitly handled because the latest sample is always the current value. If the series is still at 1, the open interval extends to `now`.

### How Kubernetes handles it

Kubernetes probes are point-in-time checks (not sliding windows). The "availability" is binary at each probe interval. State is tracked by:
- `successCount >= failureThreshold` → Ready
- `failureCount >= failureThreshold` → Not Ready

No sliding window — it's a counter with thresholds.

### How Datadog handles it

Datadog's uptime monitors use a bucket-based approach:
- Each probe result is bucketed into a 1-minute interval
- Availability = (minutes with all probes passing) / (total minutes)
- The sliding window is configurable (1m, 6h, 24h, etc.)

---

## 4. How Major Monitoring Systems Handle This

### Prometheus

**Metric**: `up` (gauge: 1.0 = up, 0.0 = down)
**Sliding window**: `up_over_time(up[5m])`
**Open interval**: Implicit — the latest sample is always included
**Pitfall**: If scrape interval is longer than alert window, brief outages are missed

### Datadog

**Approach**: Bucket-based time aggregation
**Sliding window**: Configurable (minutes to days)
**Open interval**: The current bucket is always partial and included
**Pitfall**: Bucket misalignment at window boundaries

### AWS CloudWatch

**Approach**: Simple percentage over window
**Formula**: `up_seconds / window_seconds`
**Open interval**: Always included (current period is partial)
**Pitfall**: Doesn't weight by duration — short uptimes count same as long ones

### UptimeRobot

**Approach**: Probe-based with fixed intervals (5 minutes minimum)
**Sliding window**: 1-minute to 1-year configurable
**Open interval**: Not tracked — each probe is an atomic event
**Pitfall**: Cannot detect outages shorter than probe interval

### InfluxDB / Chronograf

**Approach**: Time-series aggregation with `mean()`, `percentile()` over windows
**Open interval**: Latest bucket is always partial
**Pitfall**: Requires careful window sizing to avoid edge effects

---

## 5. Recommended Implementation Checklist

- [ ] **Clip** all intervals (completed + open) to `[cutoffTime, now]`
- [ ] **Filter** completed intervals: remove those entirely before `cutoffTime`
- [ ] **Never** include the open interval in the stored list — compute it on-the-fly
- [ ] **Handle** the case where no intervals exist AND the channel is currently down
- [ ] **Use** `Math.Max(availableStart, cutoffTime)` when transitioning TO available
- [ ] **Use** `Math.Max(availableStart, cutoffTime)` when closing the interval TO unavailable
- [ ] **Validate** that clipped intervals have `Start < End` (non-zero duration)
- [ ] **Consider** using `DateTimeOffset` instead of `DateTime` to avoid timezone issues
- [ ] **Document** whether the average is length-weighted or simple arithmetic

---

## References

- GammaRay `AvailabilityLog` implementation: `DefaultIAPChannelMonitor.cs`
- Prometheus documentation: https://prometheus.io/docs/prometheus/latest/querying/functions/
- Datadog Uptime Monitors: https://docs.datadoghq.com/monitors/manage/status_monitors/
- Kubernetes Health Checks: https://kubernetes.io/docs/concepts/workloads/pods/pod-lifecycle/#container-probes
- "Sliding Window" pattern in system design: https://docs.microsoft.com/en-us/azure/architecture/patterns/sliding-window
