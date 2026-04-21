using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScamShow;

/// <summary>
/// Temporary low-level keyboard hook used only during hotkey configuration
/// to capture a single key combination pressed by the user.
/// </summary>
public sealed class CaptureHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN     = 0x0100;
    private const int WM_SYSKEYDOWN  = 0x0104;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern bool   UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("user32.dll")] private static extern short  GetKeyState(int nVirtKey);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int ptX, ptY; }

    [DllImport("user32.dll")] private static extern int    GetMessage(out MSG lpMsg, IntPtr hWnd, uint f, uint t);
    [DllImport("user32.dll")] private static extern bool   TranslateMessage(ref MSG m);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG m);

    // Modifier virtual key codes to ignore as the "main" key
    private static readonly HashSet<uint> ModifierVks = [0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5];

    private readonly LowLevelKeyboardProc _proc;
    private readonly Action<HotkeyBinding> _onCapture;
    private IntPtr _hookId;
    private Thread? _thread;
    private volatile bool _disposed;
    private volatile bool _captured;

    public CaptureHook(Action<HotkeyBinding> onCapture)
    {
        _onCapture = onCapture;
        _proc      = HookCallback;
    }

    public void Start()
    {
        _thread = new Thread(() =>
        {
            using var proc   = Process.GetCurrentProcess();
            using var module = proc.MainModule!;
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName!), 0);

            while (!_disposed && GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            if (_hookId != IntPtr.Zero) UnhookWindowsHookEx(_hookId);
        })
        { IsBackground = true, Name = "CaptureHookThread" };

        _thread.Start();
    }

    public void Stop() => Dispose();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        PostThreadMessage((uint)(_thread?.ManagedThreadId ?? 0), 0x0012, IntPtr.Zero, IntPtr.Zero);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (!_captured && nCode >= 0 &&
            (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            // Ignore pure modifier key presses
            if (!ModifierVks.Contains(kbd.vkCode))
            {
                _captured = true;
                bool ctrl  = (GetKeyState(0xA2) & 0x8000) != 0 || (GetKeyState(0xA3) & 0x8000) != 0;
                bool shift = (GetKeyState(0xA0) & 0x8000) != 0 || (GetKeyState(0xA1) & 0x8000) != 0;
                bool alt   = (GetKeyState(0xA4) & 0x8000) != 0 || (GetKeyState(0xA5) & 0x8000) != 0;

                var binding = new HotkeyBinding
                {
                    VirtualKeyCode = (int)kbd.vkCode,
                    Ctrl  = ctrl,
                    Shift = shift,
                    Alt   = alt
                };

                _onCapture(binding);
                Dispose();
                return (IntPtr)1;
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
}
