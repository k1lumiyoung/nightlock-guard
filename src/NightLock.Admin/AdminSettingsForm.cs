using NightLock.Core;

namespace NightLock.Admin;

/// <summary>
/// Single-screen parent settings panel: lock schedule, override minutes, Windows-key
/// suppression, the emergency stop hotkey, and the parent password. It writes the same
/// machine-wide config the service and helper read; changes apply on the next reload.
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
    private readonly TextBox _hotkey = new();
    private readonly Label _hotkeyPreview = new();
    private PasswordVerifier? _pendingPassword;

    public AdminSettingsForm(ConfigStore store, FileLogger logger)
    {
        _store = store;
        _logger = logger;
        _settings = store.LoadOrCreateDefault();

        Text = "NightLock Guard — Parent Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(440, 380);

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
        _hotkey.SetBounds(240, y - 4, 180, 26);
        _hotkey.Text = Hotkey.ToTokenString(_settings.StopHotkey);
        _hotkey.TextChanged += (_, _) => UpdateHotkeyPreview();
        Controls.Add(_hotkey);
        y += 30;

        _hotkeyPreview.ForeColor = Color.DimGray;
        _hotkeyPreview.SetBounds(20, y, 400, 36);
        Controls.Add(_hotkeyPreview);
        y += 42;

        var help = new Label
        {
            Text = "Tokens joined by \"+\": LShift, RShift, LCtrl, Ctrl, Alt, A-Z, 0-9, F1-F12.\nExample: LShift+RShift+6+7. Needs 2-6 keys, at least one non-modifier.",
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 8.5F)
        };
        help.SetBounds(20, y, 400, 40);
        Controls.Add(help);
        y += 46;

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

        UpdateHotkeyPreview();
    }

    private void AddLabel(string text, int top, Font font)
    {
        Controls.Add(new Label { Text = text, Font = font, Left = 20, Top = top, Width = 220, Height = 24 });
    }

    private void UpdateHotkeyPreview()
    {
        var parsed = Hotkey.Parse(_hotkey.Text);
        if (parsed is not null && Hotkey.IsValid(parsed))
        {
            _hotkeyPreview.ForeColor = Color.DimGray;
            _hotkeyPreview.Text = $"Reads as: {Hotkey.Describe(parsed)}";
        }
        else
        {
            _hotkeyPreview.ForeColor = Color.FromArgb(180, 0, 0);
            _hotkeyPreview.Text = "Not a valid combo yet.";
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

        var hotkey = Hotkey.Parse(_hotkey.Text);
        if (hotkey is null || !Hotkey.IsValid(hotkey))
        {
            Warn("The emergency stop hotkey is not valid. See the examples below the field.");
            return;
        }

        _settings.LockWindowStart = start.ToString("HH:mm");
        _settings.LockWindowEnd = end.ToString("HH:mm");
        _settings.OverrideMinutes = (int)_minutes.Value;
        _settings.SuppressWindowsKey = _winKey.Checked;
        _settings.StopHotkeyKeys = hotkey.ToList();
        if (_pendingPassword is not null)
        {
            _settings.ParentPassword = _pendingPassword;
        }

        try
        {
            _store.Save(_settings);
            _logger.Info("Settings saved from admin panel.");
            MessageBox.Show(this, "Settings saved. They apply on the next policy reload.", "NightLock Guard",
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
