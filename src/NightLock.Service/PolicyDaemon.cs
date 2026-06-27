using System.Diagnostics;
using NightLock.Core;

namespace NightLock.Service;

/// <summary>
/// Lean supervisor: owns schedule evaluation for logging and makes sure the
/// session helper (which performs the actual full-screen lock) is running while
/// the policy needs it. The service never locks the desktop itself because a
/// session-0 service cannot draw on the interactive desktop.
///
/// @spec spec://modules/core/INFRA-001-windows-runtime-baseline#service
/// @spec spec://modules/core/INFRA-001-windows-runtime-baseline#resource-budget
/// @spec spec://modules/core/FEAT-001-night-lock-window#clock-behavior
/// @spec spec://modules/core/FEAT-006-trusted-time-source#trusted-clock
/// @spec spec://modules/core/FEAT-006-trusted-time-source#synchronization
/// </summary>
public sealed class PolicyDaemon : IDisposable
{
    private const string HelperProcessName = "NightLock.Helper";

    private readonly AppPaths _paths = AppPaths.Default();
    private readonly IMonotonicClock _monotonicClock = new SystemMonotonicClock();
    private readonly object _gate = new();
    private Timer? _timer;
    private FileLogger? _logger;
    private TrustedClock? _trustedClock;
    private string? _trustedClockConfigKey;
    private long? _lastSyncAttemptMs;
    private DateTimeOffset? _lastTick;
    private bool _started;

    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _paths.EnsureCreated();
            _logger = new FileLogger(_paths);
            _logger.Info("NightLock service daemon starting.");
            _started = true;
            Tick();
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_started)
            {
                return;
            }

            _logger?.Info("NightLock service daemon stopping.");
            _timer?.Dispose();
            _timer = null;
            _started = false;
        }
    }

    public void Dispose() => Stop();

    /// <summary>
    /// Called by the Windows service on logon/unlock/session-reconnect so the helper
    /// is launched promptly instead of waiting for the next timer tick.
    /// </summary>
    public void OnSessionChanged(string reason)
    {
        lock (_gate)
        {
            if (!_started)
            {
                return;
            }

            _logger?.Info($"Session change: {reason}.");
            Tick();
        }
    }

    private void Tick()
    {
        try
        {
            var config = new ConfigStore(_paths).LoadOrCreateDefault();
            SyncTrustedClockIfDue(config, force: false);

            // @spec spec://modules/core/FEAT-006-trusted-time-source#trusted-clock
            var now = GetPolicyNow(config);
            // The service does not track parent overrides; the helper owns override state.
            var state = NightLockPolicy.Evaluate(now, config, overrideUntil: null);
            if (config.UseTrustedTime)
            {
                LogTrustedClockDriftIfNeeded();
            }

            EnsureHelperRunningIfNeeded(state);

            if (_lastTick is not null && Math.Abs((now - _lastTick.Value).TotalMinutes) > 15)
            {
                _logger?.Warn($"Detected clock jump or service sleep. Previous tick: {_lastTick:O}; current: {now:O}.");
            }

            _lastTick = now;
            _logger?.Info($"Policy state: {state.Phase}; next={state.NextChangeAt:O}; {state.Message}");
            ScheduleNext(Min(
                NightLockPolicy.NextWakeAt(now, config, overrideUntil: null),
                NextTrustedClockMaintenanceWake(now, config)), now);
        }
        catch (Exception ex)
        {
            _logger?.Error("Policy tick failed", ex);
            var fallbackNow = DateTimeOffset.Now;
            ScheduleNext(fallbackNow.AddSeconds(30), fallbackNow);
        }
    }

    private void ScheduleNext(DateTimeOffset nextWakeAt, DateTimeOffset now)
    {
        var delay = nextWakeAt - now;
        if (delay < TimeSpan.FromSeconds(1))
        {
            delay = TimeSpan.FromSeconds(1);
        }

        if (delay > TimeSpan.FromHours(12))
        {
            delay = TimeSpan.FromHours(12);
        }

        _timer?.Dispose();
        _timer = new Timer(_ => Tick(), null, delay, Timeout.InfiniteTimeSpan);
    }

    private DateTimeOffset GetPolicyNow(NightLockSettings settings)
    {
        if (!settings.UseTrustedTime)
        {
            return DateTimeOffset.Now;
        }

        EnsureTrustedClock(settings);
        return _trustedClock?.Now ?? DateTimeOffset.Now;
    }

    private void SyncTrustedClockIfDue(NightLockSettings settings, bool force)
    {
        if (!settings.UseTrustedTime)
        {
            return;
        }

        var clock = EnsureTrustedClock(settings);
        var nowMs = _monotonicClock.ElapsedMilliseconds;
        var retryInterval = clock.IsTrusted ? settings.NtpResyncInterval : TimeSpan.FromMinutes(1);
        if (!force && _lastSyncAttemptMs is not null && TimeSpan.FromMilliseconds(nowMs - _lastSyncAttemptMs.Value) < retryInterval)
        {
            return;
        }

        _lastSyncAttemptMs = nowMs;
        clock.TrySync();
    }

    private TrustedClock EnsureTrustedClock(NightLockSettings settings)
    {
        var key = TrustedClockConfigKey(settings);
        if (_trustedClock is not null && string.Equals(_trustedClockConfigKey, key, StringComparison.Ordinal))
        {
            return _trustedClock;
        }

        _trustedClockConfigKey = key;
        _lastSyncAttemptMs = null;
        _trustedClock = new TrustedClock(
            _monotonicClock,
            () => QueryConfiguredNtpServers(settings),
            systemNow: () => DateTimeOffset.Now,
            driftLogThreshold: TimeSpan.FromMinutes(2));
        SyncTrustedClockIfDue(settings, force: true);
        return _trustedClock;
    }

    private DateTimeOffset NextTrustedClockMaintenanceWake(DateTimeOffset now, NightLockSettings settings)
    {
        if (!settings.UseTrustedTime)
        {
            return DateTimeOffset.MaxValue;
        }

        EnsureTrustedClock(settings);
        if (_lastSyncAttemptMs is null)
        {
            return now;
        }

        var interval = _trustedClock?.IsTrusted == true ? settings.NtpResyncInterval : TimeSpan.FromMinutes(1);
        var elapsed = TimeSpan.FromMilliseconds(_monotonicClock.ElapsedMilliseconds - _lastSyncAttemptMs.Value);
        var remaining = interval - elapsed;
        return remaining <= TimeSpan.Zero ? now.AddSeconds(1) : now.Add(remaining);
    }

    private void LogTrustedClockDriftIfNeeded()
    {
        if (_trustedClock is not null && _trustedClock.TryConsumeDriftWarning(out var drift))
        {
            _logger?.Warn($"Trusted time differs from system clock by {Abs(drift).TotalSeconds:F0} seconds; continuing with trusted policy time.");
        }
    }

    private static DateTimeOffset? QueryConfiguredNtpServers(NightLockSettings settings)
    {
        foreach (var server in settings.NormalizedNtpServers)
        {
            var timestamp = SntpClient.Query(server, settings.NtpTimeout);
            if (timestamp is not null)
            {
                return timestamp;
            }
        }

        return null;
    }

    private static string TrustedClockConfigKey(NightLockSettings settings)
    {
        return $"{settings.NtpTimeout.TotalSeconds}|{string.Join("|", settings.NormalizedNtpServers)}";
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;

    private static TimeSpan Abs(TimeSpan value) => value < TimeSpan.Zero ? value.Negate() : value;

    private void EnsureHelperRunningIfNeeded(PolicyState state)
    {
        // Only supervise while protection can be visible. During the restricted window
        // NextWakeAt keeps ticks at ~5s, so a killed helper is relaunched within seconds.
        if (state.Phase is not (LockPhase.Warning or LockPhase.Restricted or LockPhase.Overridden))
        {
            return;
        }

        if (IsHelperRunning())
        {
            return;
        }

        HelperTaskLauncher.TryRun(_logger);
    }

    private static bool IsHelperRunning()
    {
        try
        {
            return Process.GetProcessesByName(HelperProcessName).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
