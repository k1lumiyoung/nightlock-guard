namespace NightLock.Core;

/// <summary>
/// @spec spec://modules/core/FEAT-001-night-lock-window#schedule
/// @spec spec://modules/core/FEAT-001-night-lock-window#behavior.lock-window
/// @spec spec://modules/core/FEAT-001-night-lock-window#clock-behavior
/// @spec spec://modules/core/INFRA-001-windows-runtime-baseline#resource-budget
/// </summary>
public static class NightLockPolicy
{
    public static PolicyState Evaluate(DateTimeOffset now, NightLockSettings settings, DateTimeOffset? overrideUntil)
    {
        var window = GetCurrentWindow(now, settings);
        var warningAt = window.Start - settings.WarningOffset;
        var isOverrideActive = overrideUntil is not null && overrideUntil > now;

        if (isOverrideActive && now >= window.Start && now < window.End)
        {
            return new PolicyState(
                LockPhase.Overridden,
                now,
                Min(overrideUntil!.Value, window.End),
                IsRestrictedWindow: true,
                IsOverrideActive: true,
                Message: $"Parent override is active until {overrideUntil:HH:mm}.");
        }

        if (now >= window.Start && now < window.End)
        {
            return new PolicyState(
                LockPhase.Restricted,
                now,
                window.End,
                IsRestrictedWindow: true,
                IsOverrideActive: false,
                Message: $"Computer is locked by night rule until {window.End:HH:mm}.");
        }

        if (now >= warningAt && now < window.Start)
        {
            return new PolicyState(
                LockPhase.Warning,
                now,
                window.Start,
                IsRestrictedWindow: false,
                IsOverrideActive: false,
                Message: $"Computer will lock at {window.Start:HH:mm}.");
        }

        var nextWarning = now < warningAt ? warningAt : GetWindowForDate(now.Date.AddDays(1), settings).Start - settings.WarningOffset;
        return new PolicyState(
            LockPhase.Unrestricted,
            now,
            nextWarning,
            IsRestrictedWindow: false,
            IsOverrideActive: false,
            Message: $"Computer use is allowed until warning at {nextWarning:HH:mm}.");
    }

    public static DateTimeOffset NextWakeAt(DateTimeOffset now, NightLockSettings settings, DateTimeOffset? overrideUntil)
    {
        var state = Evaluate(now, settings, overrideUntil);
        var next = state.NextChangeAt;

        if (state.Phase == LockPhase.Restricted)
        {
            var retry = now.AddSeconds(5);
            return retry < next ? retry : next;
        }

        return next <= now ? now.AddSeconds(5) : next;
    }

    private static (DateTimeOffset Start, DateTimeOffset End) GetCurrentWindow(DateTimeOffset now, NightLockSettings settings)
    {
        var today = now.Date;
        var todayWindow = GetWindowForDate(today, settings);

        // Only a window that crosses midnight can still be "yesterday's" window in the morning.
        // For a same-day window (end > start) the morning belongs to today's upcoming window.
        var crossesMidnight = settings.LockEnd <= settings.LockStart;
        if (crossesMidnight && now < todayWindow.End && now.TimeOfDay < settings.LockEnd.ToTimeSpan())
        {
            return GetWindowForDate(today.AddDays(-1), settings);
        }

        return todayWindow;
    }

    private static (DateTimeOffset Start, DateTimeOffset End) GetWindowForDate(DateTime date, NightLockSettings settings)
    {
        var startLocal = date.Add(settings.LockStart.ToTimeSpan());
        var endDate = settings.LockEnd <= settings.LockStart ? date.AddDays(1) : date;
        var endLocal = endDate.Add(settings.LockEnd.ToTimeSpan());
        return (
            new DateTimeOffset(startLocal, TimeZoneInfo.Local.GetUtcOffset(startLocal)),
            new DateTimeOffset(endLocal, TimeZoneInfo.Local.GetUtcOffset(endLocal)));
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
}
