namespace NightLock.Admin;

/// <summary>
/// Password gate for the admin panel. The entered text is verified against the stored
/// parent-password verifier and is never logged or persisted.
///
/// @spec spec://modules/core/FEAT-003-parent-admin-panel#access-control
/// </summary>
internal sealed class AdminAuthForm : Form
{
    private readonly Func<string, bool> _verify;
    private readonly TextBox _password = new();
    private readonly Label _error = new();
    private readonly Button _ok = new();
    private int _failedAttempts;

    public AdminAuthForm(Func<string, bool> verify)
    {
        _verify = verify;

        Text = "NightLock Guard settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ClientSize = new Size(380, 170);

        Controls.Add(new Label
        {
            Text = "Enter the NightLock parent password to open settings.\nThis is not your Windows password.",
            Left = 16,
            Top = 14,
            Width = 348,
            Height = 40
        });

        _password.UseSystemPasswordChar = true;
        _password.SetBounds(16, 62, 348, 26);
        _password.Font = new Font("Segoe UI", 11F);
        Controls.Add(_password);

        _error.ForeColor = Color.FromArgb(180, 0, 0);
        _error.SetBounds(16, 94, 348, 22);
        Controls.Add(_error);

        _ok.Text = "Open";
        _ok.SetBounds(196, 124, 80, 30);
        _ok.Click += (_, _) => TrySubmit();
        Controls.Add(_ok);

        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        cancel.SetBounds(284, 124, 80, 30);
        Controls.Add(cancel);

        AcceptButton = _ok;
        CancelButton = cancel;
    }

    private void TrySubmit()
    {
        var entered = _password.Text;
        _password.Clear();

        if (_verify(entered))
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        // Generic message — do not reveal anything about the stored password.
        _failedAttempts++;
        _error.Text = "Wrong parent password.";

        if (_failedAttempts % 3 == 0)
        {
            _ok.Enabled = false;
            var release = new System.Windows.Forms.Timer { Interval = 3000 };
            release.Tick += (_, _) =>
            {
                _ok.Enabled = true;
                release.Stop();
                release.Dispose();
                _password.Focus();
            };
            release.Start();
        }
        else
        {
            _password.Focus();
        }
    }
}
