namespace NightLock.Helper;

internal sealed class SetupPasswordForm : Form
{
    private readonly TextBox _password = new();
    private readonly TextBox _confirm = new();

    public SetupPasswordForm()
    {
        Text = "Set NightLock parent password";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        Width = 430;
        Height = 230;

        Controls.Add(new Label
        {
            Text = "Create a separate NightLock parent password.",
            Left = 16,
            Top = 16,
            Width = 380,
            Height = 24
        });

        Controls.Add(new Label { Text = "Password", Left = 16, Top = 54, Width = 120 });
        _password.Left = 140;
        _password.Top = 50;
        _password.Width = 240;
        _password.UseSystemPasswordChar = true;
        Controls.Add(_password);

        Controls.Add(new Label { Text = "Confirm", Left = 16, Top = 88, Width = 120 });
        _confirm.Left = 140;
        _confirm.Top = 84;
        _confirm.Width = 240;
        _confirm.UseSystemPasswordChar = true;
        Controls.Add(_confirm);

        var ok = new Button { Text = "Save", Left = 214, Top = 130, Width = 80 };
        ok.Click += (_, _) => TryAccept();
        var cancel = new Button { Text = "Cancel", Left = 300, Top = 130, Width = 80, DialogResult = DialogResult.Cancel };

        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public string Password => _password.Text;

    private void TryAccept()
    {
        if (_password.Text.Length < 4)
        {
            MessageBox.Show("Use at least 4 characters.", "NightLock Guard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_password.Text != _confirm.Text)
        {
            MessageBox.Show("Passwords do not match.", "NightLock Guard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
