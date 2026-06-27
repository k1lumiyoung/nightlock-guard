namespace NightLock.Core;

/// <summary>
/// Provides elapsed process-independent monotonic time that is not affected by wall-clock edits.
///
/// @spec spec://modules/core/FEAT-006-trusted-time-source#trusted-clock
/// </summary>
public interface IMonotonicClock
{
    long ElapsedMilliseconds { get; }
}

