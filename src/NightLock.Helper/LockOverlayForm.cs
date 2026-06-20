namespace NightLock.Helper;

/// <summary>
/// Full-screen, always-on-top lock window shown during restricted hours. While it
/// is visible the only available action is typing the NightLock parent password.
/// It cannot be closed by the user and re-asserts itself on top of other windows.
///
/// This is best-effort enforcement against casual misuse, not a hard security
/// boundary (an administrator can still kill the process or open Task Manager).
///
/// @spec spec://modules/core/FEAT-001-night-lock-window#behavior.lock-window
/// @spec spec://modules/core/FEAT-002-parent-password-override#override-prompt
/// </summary>
internal sealed class LockOverlayForm : Form
{
    private readonly Func<string, bool> _verifyPassword;
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Label _error = new();
    private readonly TextBox _password = new();
    private readonly Button _unlock = new();
    private readonly Panel _card = new();
    private readonly System.Windows.Forms.Timer _topMostTimer = new();
    private readonly System.Windows.Forms.Timer _lockoutTimer = new();
    private int _failedAttempts;
    private bool _allowClose;

    /// <summary>Raised after the correct parent password is entered.</summary>
    public event Action? Unlocked;

    public LockOverlayForm(Func<string, bool> verifyPassword)
    {
        _verifyPassword = verifyPassword;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        ControlBox = false;
        KeyPreview = true;
        BackColor = Color.FromArgb(15, 17, 26);
        Bounds = SystemInformation.VirtualScreen;

        _card.Width = 460;
        _card.Height = 250;
        _card.BackColor = Color.FromArgb(28, 31, 46);

        _title.Text = "NightLock Guard";
        _title.ForeColor = Color.White;
        _title.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        _title.AutoSize = false;
        _title.TextAlign = ContentAlignment.MiddleCenter;
        _title.SetBounds(20, 20, 420, 36);

        _subtitle.ForeColor = Color.Gainsboro;
        _subtitle.Font = new Font("Segoe UI", 10F);
        _subtitle.AutoSize = false;
        _subtitle.TextAlign = ContentAlignment.MiddleCenter;
        _subtitle.SetBounds(20, 62, 420, 48);

        _password.UseSystemPasswordChar = true;
        _password.Font = new Font("Segoe UI", 12F);
        _password.SetBounds(60, 120, 340, 30);

        _unlock.Text = "Unlock";
        _unlock.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _unlock.SetBounds(60, 162, 340, 36);
        _unlock.Click += (_, _) => TrySubmit();

        _error.ForeColor = Color.FromArgb(255, 120, 120);
        _error.Font = new Font("Segoe UI", 9F);
        _error.AutoSize = false;
        _error.TextAlign = ContentAlignment.MiddleCenter;
        _error.SetBounds(20, 206, 420, 24);

        _card.Controls.Add(_title);
        _card.Controls.Add(_subtitle);
        _card.Controls.Add(_password);
        _card.Controls.Add(_unlock);
        _card.Controls.Add(_error);
        Controls.Add(_card);

        AcceptButton = _unlock;

        _topMostTimer.Interval = 1000;
        _topMostTimer.Tick += (_, _) => AssertOnTop();

        _lockoutTimer.Interval = 1000;
        _lockoutTimer.Tick += (_, _) => CountdownLockout();
    }

    /// <summary>Message shown above the password box (e.g. when the lock ends).</summary>
    public string SubtitleText
    {
        get => _subtitle.Text;
        set => _subtitle.Text = value;
    }

    public void PrepareForDisplay()
    {
        _password.Clear();
        _error.Text = string.Empty;
        _failedAttempts = 0;
        SetInputEnabled(true);
        CenterCard();
    }

    public void AllowCloseAndDispose()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AssertOnTop();
        _topMostTimer.Start();
        _password.Focus();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            _topMostTimer.Start();
        }
        else
        {
            _topMostTimer.Stop();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        CenterCard();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // The user must never be able to close the lock; only code paths that set
        // _allowClose (correct password / app shutdown) may proceed.
        if (!_allowClose && e.CloseReason is CloseReason.UserClosing or CloseReason.None)
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _topMostTimer.Dispose();
            _lockoutTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void CenterCard()
    {
        // Center the card on the primary monitor, accounting for the virtual-screen origin.
        var primary = Screen.PrimaryScreen?.Bounds ?? Bounds;
        var x = primary.X - Bounds.X + (primary.Width - _card.Width) / 2;
        var y = primary.Y - Bounds.Y + (primary.Height - _card.Height) / 2;
        _card.Location = new Point(Math.Max(0, x), Math.Max(0, y));
    }

    private void AssertOnTop()
    {
        if (!Visible)
        {
            return;
        }

        TopMost = true;
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        BringToFront();
        Activate();
    }

    private void TrySubmit()
    {
        if (!_password.Enabled)
        {
            return;
        }

        var entered = _password.Text;
        _password.Clear();

        if (_verifyPassword(entered))
        {
            _failedAttempts = 0;
            _error.Text = string.Empty;
            Unlocked?.Invoke();
            return;
        }

        _failedAttempts++;
        _error.Text = "Wrong parent password.";

        // Slow down repeated guessing without locking the parent out for long.
        if (_failedAttempts % 3 == 0)
        {
            StartLockout(seconds: 10);
        }
        else
        {
            _password.Focus();
        }
    }

    private int _lockoutRemaining;

    private void StartLockout(int seconds)
    {
        _lockoutRemaining = seconds;
        SetInputEnabled(false);
        _error.Text = $"Too many attempts. Wait {_lockoutRemaining}s.";
        _lockoutTimer.Start();
    }

    private void CountdownLockout()
    {
        _lockoutRemaining--;
        if (_lockoutRemaining <= 0)
        {
            _lockoutTimer.Stop();
            SetInputEnabled(true);
            _error.Text = string.Empty;
            _password.Focus();
            return;
        }

        _error.Text = $"Too many attempts. Wait {_lockoutRemaining}s.";
    }

    private void SetInputEnabled(bool enabled)
    {
        _password.Enabled = enabled;
        _unlock.Enabled = enabled;
    }
}
