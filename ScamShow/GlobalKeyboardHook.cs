using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScamShow;

public sealed class GlobalKeyboardHook : IDisposable
{
    // ── Win32 ────────────────────────────────────────────────────────────────

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN     = 0x0100;
    private const int WM_SYSKEYDOWN  = 0x0104;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern bool   UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("user32.dll")] private static extern short  GetKeyState(int nVirtKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint   vkCode;
        public uint   scanCode;
        public uint   flags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    // ── Fields ───────────────────────────────────────────────────────────────

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private Thread? _hookThread;
    private volatile bool _disposed;

    public event Action<HotkeyAction>? HotkeyPressed;
    private AppConfig _config;

    public GlobalKeyboardHook(AppConfig config)
    {
        _config = config;
        _proc   = HookCallback;
    }

    public void UpdateConfig(AppConfig config) => _config = config;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void Start()
    {
        _hookThread = new Thread(() =>
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule  = curProcess.MainModule!;
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName!), 0);

            // Message pump — required to receive low-level hook callbacks
            while (!_disposed && GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            if (_hookId != IntPtr.Zero)
                UnhookWindowsHookEx(_hookId);
        })
        { IsBackground = true, Name = "KeyboardHookThread" };

        _hookThread.Start();
    }

    public void Stop() => Dispose();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Post WM_QUIT to the hook thread's message queue so it exits cleanly
        PostThreadMessage((uint)(_hookThread?.ManagedThreadId ?? 0), 0x0012 /*WM_QUIT*/, IntPtr.Zero, IntPtr.Zero);
    }

    // ── Hook callback ────────────────────────────────────────────────────────

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool ctrl  = (GetKeyState(0xA2) & 0x8000) != 0 || (GetKeyState(0xA3) & 0x8000) != 0;
            bool shift = (GetKeyState(0xA0) & 0x8000) != 0 || (GetKeyState(0xA1) & 0x8000) != 0;
            bool alt   = (GetKeyState(0xA4) & 0x8000) != 0 || (GetKeyState(0xA5) & 0x8000) != 0;

            foreach (var (action, binding) in _config.Hotkeys)
            {
                if ((int)kbd.vkCode == binding.VirtualKeyCode &&
                    ctrl  == binding.Ctrl  &&
                    shift == binding.Shift &&
                    alt   == binding.Alt)
                {
                    HotkeyPressed?.Invoke(action);
                    return (IntPtr)1; // suppress the key
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    // ── Extra Win32 for message pump ─────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint   message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint   time;
        public int    ptX, ptY;
    }

    [DllImport("user32.dll")] private static extern int  GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG lpmsg);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);
}
