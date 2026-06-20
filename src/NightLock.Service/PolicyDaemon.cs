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
/// </summary>
public sealed class PolicyDaemon : IDisposable
{
    private const string HelperProcessName = "NightLock.Helper";

    private readonly AppPaths _paths = AppPaths.Default();
    private readonly object _gate = new();
    private Timer? _timer;
    private FileLogger? _logger;
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
            var now = DateTimeOffset.Now;
            var config = new ConfigStore(_paths).LoadOrCreateDefault();
            // The service does not track parent overrides; the helper owns override state.
            var state = NightLockPolicy.Evaluate(now, config, overrideUntil: null);

            EnsureHelperRunningIfNeeded(state);

            if (_lastTick is not null && Math.Abs((now - _lastTick.Value).TotalMinutes) > 15)
            {
                _logger?.Warn($"Detected clock jump or service sleep. Previous tick: {_lastTick:O}; current: {now:O}.");
            }

            _lastTick = now;
            _logger?.Info($"Policy state: {state.Phase}; next={state.NextChangeAt:O}; {state.Message}");
            ScheduleNext(NightLockPolicy.NextWakeAt(now, config, overrideUntil: null));
        }
        catch (Exception ex)
        {
            _logger?.Error("Policy tick failed", ex);
            ScheduleNext(DateTimeOffset.Now.AddSeconds(30));
        }
    }

    private void ScheduleNext(DateTimeOffset nextWakeAt)
    {
        var delay = nextWakeAt - DateTimeOffset.Now;
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
