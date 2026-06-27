namespace NightLock.Core;

/// <summary>
/// Derives policy time from a trusted UTC anchor plus monotonic elapsed time.
///
/// @spec spec://modules/core/FEAT-006-trusted-time-source#trusted-clock
/// @spec spec://modules/core/FEAT-006-trusted-time-source#fallback
/// @spec spec://modules/core/FEAT-006-trusted-time-source#tamper-evidence
/// </summary>
public sealed class TrustedClock
{
    private readonly IMonotonicClock _monotonic;
    private readonly Func<DateTimeOffset?> _syncSource;
    private readonly Func<DateTimeOffset> _systemNow;
    private readonly TimeSpan _driftLogThreshold;
    private DateTimeOffset _anchorUtc;
    private long _anchorMonotonicMs;
    private bool _driftWarningConsumed;

    public TrustedClock(
        IMonotonicClock monotonic,
        Func<DateTimeOffset?> syncSource,
        Func<DateTimeOffset>? systemNow = null,
        TimeSpan? driftLogThreshold = null)
    {
        _monotonic = monotonic;
        _syncSource = syncSource;
        _systemNow = systemNow ?? (() => DateTimeOffset.Now);
        _driftLogThreshold = driftLogThreshold ?? TimeSpan.FromMinutes(2);
    }

    public bool IsTrusted { get; private set; }

    public DateTimeOffset Now
    {
        get
        {
            if (!IsTrusted)
            {
                return _systemNow();
            }

            var elapsedMs = _monotonic.ElapsedMilliseconds - _anchorMonotonicMs;
            var trustedUtc = _anchorUtc.AddMilliseconds(elapsedMs);
            return TimeZoneInfo.ConvertTime(trustedUtc, TimeZoneInfo.Local);
        }
    }

    public TimeSpan? Drift => IsTrusted ? Now - _systemNow() : null;

    public void TrySync()
    {
        var trustedUtc = _syncSource();
        if (trustedUtc is null)
        {
            return;
        }

        _anchorUtc = trustedUtc.Value.ToUniversalTime();
        _anchorMonotonicMs = _monotonic.ElapsedMilliseconds;
        IsTrusted = true;
    }

    public bool TryConsumeDriftWarning(out TimeSpan drift)
    {
        drift = Drift ?? TimeSpan.Zero;
        if (_driftWarningConsumed || !IsTrusted || Abs(drift) <= _driftLogThreshold)
        {
            return false;
        }

        _driftWarningConsumed = true;
        return true;
    }

    private static TimeSpan Abs(TimeSpan value) => value < TimeSpan.Zero ? value.Negate() : value;
}

