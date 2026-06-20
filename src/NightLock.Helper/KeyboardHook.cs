using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NightLock.Helper;

/// <summary>
/// Single low-level keyboard hook (WH_KEYBOARD_LL) owned by the session helper. It does
/// exactly two narrow things and nothing else:
///   1. blocks the Windows key while <see cref="SuppressWindowsKey"/> is true (FEAT-005);
///   2. detects the configured emergency stop combo and raises <see cref="StopComboPressed"/>
///      (FEAT-004).
///
/// It NEVER records, stores, buffers, or transmits typed characters — it only inspects key
/// state to decide block-vs-pass and to match the configured combo, then forgets it. It does
/// not (and a user-mode hook cannot) intercept Ctrl+Alt+Del or emergency shutdown.
///
/// @spec spec://modules/core/INFRA-001-windows-runtime-baseline#input-suppression-hook
/// @spec spec://modules/core/FEAT-004-emergency-stop-hotkey#stop-behavior
/// @spec spec://modules/core/FEAT-005-windows-key-suppression#suppressed-keys
/// @spec spec://modules/core/PROP-001-product-boundaries#safety-boundaries
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int HcAction = 0;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    private readonly LowLevelKeyboardProc _proc;
    private readonly HashSet<int> _pressed = new();
    private readonly HashSet<int> _combo = new();
    private readonly Action _onStop;
    private readonly Action<string> _log;
    private readonly SynchronizationContext? _syncContext;

    private IntPtr _hookHandle = IntPtr.Zero;
    private volatile bool _suppressWindowsKey;
    private bool _comboLatched;

    public KeyboardHook(IReadOnlyList<int> combo, Action onStop, Action<string> log)
    {
        _onStop = onStop;
        _log = log;
        _proc = HookCallback;
        _syncContext = SynchronizationContext.Current;
        SetCombo(combo);
    }

    /// <summary>When true, the Windows key is swallowed (used only during restricted hours).</summary>
    public bool SuppressWindowsKey
    {
        get => _suppressWindowsKey;
        set => _suppressWindowsKey = value;
    }

    public bool IsInstalled => _hookHandle != IntPtr.Zero;

    public void SetCombo(IReadOnlyList<int> combo)
    {
        _combo.Clear();
        foreach (var vk in combo)
        {
            _combo.Add(vk);
        }
    }

    /// <summary>Installs the hook. Failure is logged and returns false; the rest of the helper keeps working.</summary>
    public bool Install()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return true;
        }

        try
        {
            using var process = Process.GetCurrentProcess();
            using var module = process.MainModule;
            var moduleHandle = module is null ? IntPtr.Zero : GetModuleHandle(module.ModuleName);
            _hookHandle = SetWindowsHookEx(WhKeyboardLl, _proc, moduleHandle, 0);

            if (_hookHandle == IntPtr.Zero)
            {
                _log($"Keyboard hook install failed (error {Marshal.GetLastWin32Error()}). Lock still applies; Win-key/stop-combo disabled.");
                return false;
            }

            _log("Keyboard hook installed (Win-key suppression + stop combo).");
            return true;
        }
        catch (Exception ex)
        {
            _log($"Keyboard hook install threw: {ex.Message}");
            _hookHandle = IntPtr.Zero;
            return false;
        }
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
            _log("Keyboard hook removed.");
        }

        _pressed.Clear();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != HcAction)
        {
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        var vk = (int)data.vkCode;
        var isDown = message is WmKeyDown or WmSysKeyDown;
        var isUp = message is WmKeyUp or WmSysKeyUp;

        // 1) Windows-key suppression. Swallow LWin/RWin (down and up) while active.
        if (_suppressWindowsKey && vk is VkLWin or VkRWin)
        {
            return (IntPtr)1;
        }

        // 2) Stop-combo detection. Track which keys are currently held and fire once when
        //    every combo key is down together. Nothing about the keystroke is stored.
        if (isDown)
        {
            _pressed.Add(vk);
            if (!_comboLatched && _combo.Count > 0 && _combo.IsSubsetOf(_pressed))
            {
                _comboLatched = true;
                RaiseStop();
            }
        }
        else if (isUp)
        {
            _pressed.Remove(vk);
            if (_comboLatched && !_combo.IsSubsetOf(_pressed))
            {
                _comboLatched = false;
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void RaiseStop()
    {
        // Post off the hook callback so showing/hiding UI and removing the hook never run
        // re-entrantly inside the hook chain (which must return promptly).
        if (_syncContext is not null)
        {
            _syncContext.Post(_ => _onStop(), null);
        }
        else
        {
            _onStop();
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
