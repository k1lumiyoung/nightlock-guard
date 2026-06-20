using System.Runtime.InteropServices;
using NightLock.Core;

namespace NightLock.Admin;

/// <summary>
/// Captures an emergency stop combo by letting the parent literally press and hold the keys,
/// then release. Left/right modifiers are distinguished via the live key state, so the stored
/// combo matches what the helper's keyboard hook sees.
///
/// @spec spec://modules/core/FEAT-004-emergency-stop-hotkey#hotkey-config
/// @spec spec://modules/core/FEAT-003-parent-admin-panel#editable-settings
/// </summary>
internal sealed class HotkeyCaptureForm : Form
{
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;

    private readonly Label _prompt = new();
    private readonly List<int> _captured = new();
    private bool _capturing = true;

    public HotkeyCaptureForm()
    {
        Text = "Record stop hotkey";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, 150);
        KeyPreview = true;

        _prompt.SetBounds(16, 16, 348, 70);
        _prompt.TextAlign = ContentAlignment.MiddleCenter;
        _prompt.Font = new Font("Segoe UI", 10F);
        _prompt.Text = "Press and hold the keys you want,\nthen release them together.";
        Controls.Add(_prompt);

        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        cancel.SetBounds(150, 104, 80, 30);
        Controls.Add(cancel);
        CancelButton = cancel;
    }

    /// <summary>The captured combo (virtual-key codes), or null if cancelled.</summary>
    public IReadOnlyList<int>? Captured { get; private set; }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;
        if (!_capturing)
        {
            return;
        }

        foreach (var vk in ResolveKeys(e.KeyCode, e.KeyValue))
        {
            if (!_captured.Contains(vk))
            {
                _captured.Add(vk);
            }
        }

        _prompt.Text = _captured.Count == 0
            ? "Press and hold the keys you want,\nthen release them together."
            : $"{Hotkey.Describe(_captured)}\n(release the keys to confirm)";
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;
        if (!_capturing || _captured.Count == 0)
        {
            return;
        }

        // Finalize once every captured key has been released.
        if (_captured.Any(vk => (GetAsyncKeyState(vk) & 0x8000) != 0))
        {
            return;
        }

        if (Hotkey.IsValid(_captured))
        {
            _capturing = false;
            Captured = _captured.ToList();
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _prompt.ForeColor = Color.FromArgb(180, 0, 0);
            _prompt.Text = "Need 2–6 keys incl. at least one\nnon-modifier. Try again.";
            _captured.Clear();
        }
    }

    private static IEnumerable<int> ResolveKeys(Keys keyCode, int keyValue)
    {
        switch (keyCode)
        {
            case Keys.ShiftKey:
                if (IsDown(VkLShift)) yield return VkLShift;
                if (IsDown(VkRShift)) yield return VkRShift;
                break;
            case Keys.ControlKey:
                if (IsDown(VkLControl)) yield return VkLControl;
                if (IsDown(VkRControl)) yield return VkRControl;
                break;
            case Keys.Menu:
                if (IsDown(VkLMenu)) yield return VkLMenu;
                if (IsDown(VkRMenu)) yield return VkRMenu;
                break;
            default:
                yield return keyValue;
                break;
        }
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
