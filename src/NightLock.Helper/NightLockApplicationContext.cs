using System.Diagnostics;
using Microsoft.Win32;
using NightLock.Core;

namespace NightLock.Helper;

/// <summary>
/// Session-side enforcer. Evaluates the night-lock schedule and, during restricted
/// hours, shows the full-screen <see cref="LockOverlayForm"/>. A correct parent
/// password grants an in-memory, time-bounded override; when it expires the lock
/// returns. All enforcement lives in the user session because only a process on the
/// interactive desktop can cover the screen.
///
/// @spec spec://modules/core/FEAT-001-night-lock-window#behavior.warning
/// @spec spec://modules/core/FEAT-001-night-lock-window#behavior.lock-window
/// @spec spec://modules/core/FEAT-002-parent-password-override#override-behavior
/// @spec spec://modules/core/FEAT-004-emergency-stop-hotkey#stop-behavior
/// @spec spec://modules/core/FEAT-005-windows-key-suppression#suppression-window
/// @spec spec://modules/core/INFRA-001-windows-runtime-baseline#resource-budget
/// </summary>
internal sealed class NightLockApplicationContext : ApplicationContext
{
    private readonly AppPaths _paths = AppPaths.Default();
    private readonly ConfigStore _configStore;
    private readonly FileLogger _logger;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly LockOverlayForm _overlay;
    private readonly KeyboardHook _keyboardHook;
    private DateTimeOffset? _overrideUntil;
    private DateTimeOffset? _lastWarningStart;
    private bool _stopped;

    public NightLockApplicationContext()
    {
        _paths.EnsureCreated();
        _configStore = new ConfigStore(_paths);
        _logger = new FileLogger(_paths);

        _overlay = new LockOverlayForm(VerifyParentPassword);
        _overlay.Unlocked += OnOverlayUnlocked;
        // Force the window handle so SystemEvents callbacks can marshal onto the UI thread.
        _ = _overlay.Handle;

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "NightLock Guard",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        // The hook detects the emergency stop combo at all times; Win-key suppression is
        // toggled per policy phase in EvaluateAndSchedule.
        _keyboardHook = new KeyboardHook(
            _configStore.LoadOrCreateDefault().StopHotkey,
            OnStopComboPressed,
            _logger.Info);
        _keyboardHook.Install();

        _timer.Tick += (_, _) => EvaluateAndSchedule();
        SystemEvents.SessionSwitch += OnSessionSwitch;
        _logger.Info("Helper started.");

        EnsureParentPassword();
        EvaluateAndSchedule();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _logger.Info("Helper exiting.");
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            _keyboardHook.Dispose();
            _timer.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _overlay.AllowCloseAndDispose();
            _overlay.Dispose();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Status", null, (_, _) => ShowStatus());
        menu.Items.Add("Re-lock now", null, (_, _) => CancelOverride());
        menu.Items.Add("Settings…", null, (_, _) => LaunchAdminPanel());
        return menu;
    }

    /// <summary>
    /// Launches the hidden, password-gated parent admin panel. It is a separate elevated
    /// process (it must write the ACL-hardened config), so this just starts the exe and lets
    /// Windows show the UAC prompt.
    ///
    /// @spec spec://modules/core/FEAT-003-parent-admin-panel#discoverability
    /// </summary>
    private void LaunchAdminPanel()
    {
        var adminExe = Path.Combine(AppContext.BaseDirectory, "NightLock.Admin.exe");
        if (!File.Exists(adminExe))
        {
            MessageBox.Show(
                "The settings app was not found next to the helper.",
                "NightLock Guard",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(adminExe) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            // A cancelled UAC prompt lands here; that is a normal user choice, not an error.
            _logger.Info($"Admin panel launch cancelled or failed: {ex.Message}");
        }
    }

    private void EnsureParentPassword()
    {
        var settings = _configStore.LoadOrCreateDefault();
        if (settings.ParentPassword is not null)
        {
            return;
        }

        using var dialog = new SetupPasswordForm();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            settings.ParentPassword = PasswordHasher.Create(dialog.Password);
            try
            {
                _configStore.Save(settings);
                _logger.Info("Parent password configured from helper.");
            }
            catch (Exception ex)
            {
                _logger.Error("Could not save parent password from helper (config may be admin-only)", ex);
                MessageBox.Show(
                    "Could not save the parent password. Ask an administrator to set it during install.",
                    "NightLock Guard",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        else
        {
            _logger.Warn("Parent password is not configured; the lock cannot be unlocked until it is set.");
        }
    }

    private void EvaluateAndSchedule()
    {
        _timer.Stop();

        // After an emergency stop the helper stays alive but enforces nothing until it is
        // restarted (logoff/reboot), per FEAT-004#re-enable.
        if (_stopped)
        {
            HideOverlay();
            return;
        }

        var settings = _configStore.LoadOrCreateDefault();
        var now = DateTimeOffset.Now;
        var overrideUntil = ActiveOverride(now);
        var state = NightLockPolicy.Evaluate(now, settings, overrideUntil);

        // Pick up a hotkey change made in the admin panel on the next reload.
        _keyboardHook.SetCombo(settings.StopHotkey);

        switch (state.Phase)
        {
            case LockPhase.Warning:
                ShowWarningOnce(state);
                HideOverlay();
                break;

            case LockPhase.Restricted:
                ShowOverlay(settings, state);
                break;

            case LockPhase.Overridden:
                HideOverlay();
                break;

            default:
                _overrideUntil = null;
                HideOverlay();
                break;
        }

        // Suppress the Windows key only while the lock is actually showing (restricted, no
        // override) and the setting is enabled (FEAT-005#suppression-window).
        _keyboardHook.SuppressWindowsKey =
            state.Phase == LockPhase.Restricted && settings.SuppressWindowsKey;

        ScheduleNext(NightLockPolicy.NextWakeAt(now, settings, overrideUntil));
    }

    /// <summary>
    /// Handles the emergency stop combo: removes the lock and the input hook and parks the
    /// helper until it is restarted. The combo is a convenience secret, not a credential.
    ///
    /// @spec spec://modules/core/FEAT-004-emergency-stop-hotkey#stop-behavior
    /// </summary>
    private void OnStopComboPressed()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        _logger.Info("Emergency stop combo pressed; enforcement stopped until helper restart.");
        _timer.Stop();
        _keyboardHook.SuppressWindowsKey = false;
        _keyboardHook.Dispose();
        HideOverlay();
    }

    private DateTimeOffset? ActiveOverride(DateTimeOffset now)
    {
        if (_overrideUntil is not null && _overrideUntil <= now)
        {
            _overrideUntil = null;
        }

        return _overrideUntil;
    }

    private void ShowWarningOnce(PolicyState state)
    {
        var lockStart = state.NextChangeAt;
        if (_lastWarningStart == lockStart)
        {
            return;
        }

        _lastWarningStart = lockStart;
        _notifyIcon.ShowBalloonTip(
            timeout: 10_000,
            tipTitle: "NightLock Guard",
            tipText: state.Message,
            tipIcon: ToolTipIcon.Warning);
        _logger.Info("Warning shown.");
    }

    private void ShowOverlay(NightLockSettings settings, PolicyState state)
    {
        var minutes = (int)settings.OverrideDuration.TotalMinutes;
        _overlay.SubtitleText =
            $"{state.Message}\nEnter the NightLock parent password for {minutes} minutes of access.";

        if (!_overlay.Visible)
        {
            _logger.Info("Lock shown (restricted window).");
            _overlay.PrepareForDisplay();
            _overlay.Show();
        }

        _overlay.WindowState = FormWindowState.Normal;
        _overlay.BringToFront();
        _overlay.Activate();
    }

    private void HideOverlay()
    {
        if (_overlay.Visible)
        {
            _overlay.Hide();
        }
    }

    private bool VerifyParentPassword(string password)
    {
        var settings = _configStore.LoadOrCreateDefault();
        if (settings.ParentPassword is null)
        {
            return false;
        }

        var ok = PasswordHasher.Verify(password, settings.ParentPassword);
        _logger.Info(ok ? "Parent override accepted." : "Parent override attempt failed.");
        return ok;
    }

    private void OnOverlayUnlocked()
    {
        var settings = _configStore.LoadOrCreateDefault();
        _overrideUntil = DateTimeOffset.Now.Add(settings.OverrideDuration);
        _logger.Info($"Parent override active until {_overrideUntil:O}.");
        HideOverlay();
        EvaluateAndSchedule();
    }

    private void CancelOverride()
    {
        _overrideUntil = null;
        _logger.Info("Override cancelled from helper menu.");
        EvaluateAndSchedule();
    }

    private void ShowStatus()
    {
        var settings = _configStore.LoadOrCreateDefault();
        var now = DateTimeOffset.Now;
        var state = NightLockPolicy.Evaluate(now, settings, ActiveOverride(now));
        MessageBox.Show(state.Message, "NightLock Guard", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is not (SessionSwitchReason.SessionUnlock
            or SessionSwitchReason.SessionLogon
            or SessionSwitchReason.ConsoleConnect))
        {
            return;
        }

        // Re-evaluate immediately so the lock reappears within the spec's relock target
        // after an unlock, without waiting for the next timer tick.
        if (_overlay.IsHandleCreated)
        {
            _overlay.BeginInvoke(new Action(EvaluateAndSchedule));
        }
        else
        {
            EvaluateAndSchedule();
        }
    }

    private void ScheduleNext(DateTimeOffset nextWakeAt)
    {
        var delay = nextWakeAt - DateTimeOffset.Now;
        if (delay < TimeSpan.FromSeconds(1))
        {
            delay = TimeSpan.FromSeconds(1);
        }

        if (delay > TimeSpan.FromHours(1))
        {
            delay = TimeSpan.FromHours(1);
        }

        _timer.Interval = (int)Math.Clamp(delay.TotalMilliseconds, 1_000, int.MaxValue);
        _timer.Start();
    }
}
