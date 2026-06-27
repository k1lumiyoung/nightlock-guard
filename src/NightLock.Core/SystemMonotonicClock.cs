namespace NightLock.Core;

/// <summary>
/// Default monotonic counter backed by the OS tick count.
///
/// @spec spec://modules/core/FEAT-006-trusted-time-source#trusted-clock
/// </summary>
public sealed class SystemMonotonicClock : IMonotonicClock
{
    public long ElapsedMilliseconds => Environment.TickCount64;
}

