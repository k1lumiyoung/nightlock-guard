namespace NightLock.Admin;

/// <summary>
/// Small dialog to set a new parent password (with confirmation). The text is only used to
/// build a new verifier in the caller; it is never logged.
///
/// @spec spec://modules/core/FEAT-002-parent-password-override#password-setup
/// </summary>
internal sealed class ChangePasswordForm : Form
{
    private readonly TextBox _password = new();
    private readonly TextBox _confirm = new();

    public ChangePasswordForm()
    {
        Text = "Change parent password";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, 180);

        Controls.Add(new Label { Text = "New parent password (min 4 characters).", Left = 16, Top = 14, Width = 348, Height = 22 });

        Controls.Add(new Label { Text = "Password", Left = 16, Top = 50, Width = 110 });
        _password.UseSystemPasswordChar = true;
        _password.SetBounds(130, 46, 234, 26);
        Controls.Add(_password);

        Controls.Add(new Label { Text = "Confirm", Left = 16, Top = 86, Width = 110 });
        _confirm.UseSystemPasswordChar = true;
        _confirm.SetBounds(130, 82, 234, 26);
        Controls.Add(_confirm);

        var ok = new Button { Text = "OK" };
        ok.SetBounds(196, 130, 80, 30);
        ok.Click += (_, _) => TryAccept();
        Controls.Add(ok);

        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        cancel.SetBounds(284, 130, 80, 30);
        Controls.Add(cancel);

        AcceptButton = ok;
        CancelButton = cancel;
    }

    public string NewPassword => _password.Text;

    private void TryAccept()
    {
        if (_password.Text.Length < 4)
        {
            MessageBox.Show(this, "Use at least 4 characters.", "NightLock Guard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_password.Text != _confirm.Text)
        {
            MessageBox.Show(this, "Passwords do not match.", "NightLock Guard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
