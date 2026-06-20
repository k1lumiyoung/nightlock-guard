using NightLock.Core;

namespace NightLock.Admin;

/// <summary>
/// Single-screen parent settings panel: lock schedule, override minutes, Windows-key
/// suppression, the emergency stop hotkey, and the parent password. It writes the same
/// machine-wide config the service and helper read; the helper applies changes immediately.
///
/// @spec spec://modules/core/FEAT-003-parent-admin-panel#editable-settings
/// @spec spec://modules/core/FEAT-003-parent-admin-panel#persistence
/// @spec spec://modules/core/FEAT-003-parent-admin-panel#ui-simplicity
/// </summary>
internal sealed class AdminSettingsForm : Form
{
    private readonly ConfigStore _store;
    private readonly FileLogger _logger;
    private readonly NightLockSettings _settings;

    private readonly TextBox _start = new();
    private readonly TextBox _end = new();
    private readonly NumericUpDown _minutes = new();
    private readonly CheckBox _winKey = new();
    private readonly TextBox _hotkeyDisplay = new();
    private List<int> _capturedHotkey;
    private PasswordVerifier? _pendingPassword;

    public AdminSettingsForm(ConfigStore store, FileLogger logger)
    {
        _store = store;
        _logger = logger;
        _settings = store.LoadOrCreateDefault();
        _capturedHotkey = _settings.StopHotkey.ToList();

        Text = "NightLock Guard — Parent Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(440, 410);

        var labelFont = new Font("Segoe UI", 9.5F);
        int y = 18;

        AddLabel("Lock starts at (HH:mm)", y, labelFont);
        _start.SetBounds(240, y - 4, 180, 26);
        _start.Text = _settings.LockWindowStart;
        Controls.Add(_start);
        y += 40;

        AddLabel("Lock ends at (HH:mm)", y, labelFont);
        _end.SetBounds(240, y - 4, 180, 26);
        _end.Text = _settings.LockWindowEnd;
        Controls.Add(_end);
        y += 40;

        AddLabel("Password unlock minutes", y, labelFont);
        _minutes.SetBounds(240, y - 4, 180, 26);
        _minutes.Minimum = 1;
        _minutes.Maximum = 240;
        _minutes.Value = Math.Clamp((int)_settings.OverrideDuration.TotalMinutes, 1, 240);
        Controls.Add(_minutes);
        y += 40;

        _winKey.Text = "Block the Windows key during lock hours";
        _winKey.SetBounds(20, y, 400, 24);
        _winKey.Checked = _settings.SuppressWindowsKey;
        Controls.Add(_winKey);
        y += 38;

        AddLabel("Emergency stop hotkey", y, labelFont);
        _hotkeyDisplay.SetBounds(240, y - 4, 180, 26);
        _hotkeyDisplay.ReadOnly = true;
        Controls.Add(_hotkeyDisplay);
        y += 32;

        var recordButton = new Button { Text = "Record keys…" };
        recordButton.SetBounds(240, y, 180, 28);
        recordButton.Click += (_, _) => RecordHotkey();
        Controls.Add(recordButton);
        y += 36;

        var help = new Label
        {
            Text = "Click \"Record keys…\", then press and hold the keys you want\n"
                 + "(e.g. Left Shift + Right Shift + 6 + 7) and release them together.\n"
                 + "2–6 keys, at least one non-modifier.",
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 8.5F)
        };
        help.SetBounds(20, y, 410, 48);
        Controls.Add(help);
        y += 54;

        var changePassword = new Button { Text = "Change parent password…" };
        changePassword.SetBounds(20, y, 200, 30);
        changePassword.Click += (_, _) => ChangePassword();
        Controls.Add(changePassword);

        var save = new Button { Text = "Save" };
        save.SetBounds(256, y, 80, 30);
        save.Click += (_, _) => Save();
        Controls.Add(save);

        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        cancel.SetBounds(344, y, 80, 30);
        Controls.Add(cancel);

        AcceptButton = save;
        CancelButton = cancel;

        UpdateHotkeyDisplay();
    }

    private void AddLabel(string text, int top, Font font)
    {
        Controls.Add(new Label { Text = text, Font = font, Left = 20, Top = top, Width = 220, Height = 24 });
    }

    private void UpdateHotkeyDisplay()
    {
        _hotkeyDisplay.Text = Hotkey.Describe(_capturedHotkey);
    }

    private void RecordHotkey()
    {
        using var dialog = new HotkeyCaptureForm();
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Captured is not null)
        {
            _capturedHotkey = dialog.Captured.ToList();
            UpdateHotkeyDisplay();
        }
    }

    private void ChangePassword()
    {
        using var dialog = new ChangePasswordForm();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pendingPassword = PasswordHasher.Create(dialog.NewPassword);
            MessageBox.Show(this, "New password will be applied when you click Save.", "NightLock Guard",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void Save()
    {
        if (!TimeOnly.TryParse(_start.Text, out var start))
        {
            Warn("Lock start time is not a valid HH:mm time.");
            return;
        }

        if (!TimeOnly.TryParse(_end.Text, out var end))
        {
            Warn("Lock end time is not a valid HH:mm time.");
            return;
        }

        if (start == end)
        {
            Warn("Lock start and end cannot be the same time.");
            return;
        }

        if (!Hotkey.IsValid(_capturedHotkey))
        {
            Warn("The emergency stop hotkey is not valid. Click \"Record keys…\" and press 2–6 keys including at least one non-modifier.");
            return;
        }

        _settings.LockWindowStart = start.ToString("HH:mm");
        _settings.LockWindowEnd = end.ToString("HH:mm");
        _settings.OverrideMinutes = (int)_minutes.Value;
        _settings.SuppressWindowsKey = _winKey.Checked;
        _settings.StopHotkeyKeys = _capturedHotkey.ToList();
        if (_pendingPassword is not null)
        {
            _settings.ParentPassword = _pendingPassword;
        }

        try
        {
            _store.Save(_settings);
            _logger.Info("Settings saved from admin panel.");
            MessageBox.Show(this, "Settings saved and applied.", "NightLock Guard",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _logger.Error("Admin panel could not save settings", ex);
            MessageBox.Show(this,
                "Could not save settings. Make sure you opened this from an administrator account.",
                "NightLock Guard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Warn(string message)
        => MessageBox.Show(this, message, "NightLock Guard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
