using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScamShow;

/// <summary>
/// Installs a WH_MOUSE_LL hook that intercepts every WM_MOUSEMOVE event,
/// inverts the Y delta, and re-injects a corrected absolute move via SendInput.
/// A sentinel dwExtraInfo value prevents the hook from looping on its own events.
/// </summary>
public sealed class MouseInvertHook : IDisposable
{
    // ── Win32 constants ──────────────────────────────────────────────────────
    private const int    WH_MOUSE_LL   = 14;
    private const int    WM_MOUSEMOVE  = 0x0200;
    private const uint   MOUSEEVENTF_MOVE      = 0x0001;
    private const uint   MOUSEEVENTF_ABSOLUTE  = 0x8000;
    private const uint   MOUSEEVENTF_VIRTUALDESK = 0x4000;
    private const IntPtr INJECTED_SENTINEL = 0x5343414D; // "SCAM" as magic value

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern bool   UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("user32.dll")] private static extern uint   SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] private static extern bool   GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] private static extern int    GetSystemMetrics(int nIndex);
    [DllImport("user32.dll")] private static extern bool   PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int    GetMessage(out MSG lpMsg, IntPtr hWnd, uint f, uint t);
    [DllImport("user32.dll")] private static extern bool   PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
    [DllImport("user32.dll")] private static extern bool   TranslateMessage(ref MSG m);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG m);

    // ── Structs ──────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT   pt;
        public uint    mouseData;
        public uint    flags;
        public uint    time;
        public IntPtr  dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int    dx, dy;
        public uint   mouseData;
        public uint   dwFlags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint       type;   // 0 = INPUT_MOUSE
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int ptX, ptY; }

    // SM_CXVIRTUALSCREEN / SM_CYVIRTUALSCREEN
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;
    private const int SM_XVIRTUALSCREEN  = 76;
    private const int SM_YVIRTUALSCREEN  = 77;

    // ── Fields ───────────────────────────────────────────────────────────────

    private readonly LowLevelMouseProc _proc;
    private IntPtr  _hookId;
    private Thread? _thread;
    private volatile bool _disposed;
    private POINT   _lastPt;
    private bool    _hasPrev;
    private readonly ManualResetEventSlim _threadStarted = new(false);
    private readonly ManualResetEventSlim _exitEvent = new(false);

    public MouseInvertHook()
    {
        _proc = HookCallback;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void Start()
    {
        if (_thread != null && _thread.IsAlive) return; // Already started

        _hasPrev = false;
        _disposed = false;
        _threadStarted.Reset();
        _exitEvent.Reset();
        _thread  = new Thread(() =>
        {
            using var proc   = Process.GetCurrentProcess();
            using var module = proc.MainModule!;
            _hookId = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(module.ModuleName!), 0);

            if (_hookId == IntPtr.Zero)
            {
                _threadStarted.Set();
                return; // Failed to set hook
            }

            // Signal that hook is set up
            _threadStarted.Set();

            // Keep hook responsive with a non-blocking message pump
            while (!_disposed)
            {
                if (PeekMessage(out var msg, IntPtr.Zero, 0, 0, 0x0001)) // PM_REMOVE = 0x0001
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
                else
                {
                    // Yield to prevent 100% CPU usage
                    System.Threading.Thread.Yield();
                }
            }

            // Unhook before exiting
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        })
        { IsBackground = true, Name = "MouseInvertHookThread" };

        _thread.Start();
        // Wait for hook to actually be set up before returning
        _threadStarted.Wait(1000);
    }

    public void Stop() => Dispose();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Signal the thread to exit
        _exitEvent.Set();

        if (_thread != null && _thread.IsAlive)
        {
            // Wait for thread to exit cleanly
            if (!_thread.Join(2000))
            {
                // Force abort if thread doesn't exit
                try { _thread.Abort(); } catch { }
            }
        }

        _threadStarted?.Dispose();
        _exitEvent?.Dispose();
    }

    // ── Hook callback ────────────────────────────────────────────────────────

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEMOVE)
        {
            var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            // Ignore events we injected ourselves
            if (ms.dwExtraInfo == INJECTED_SENTINEL)
            {
                _hasPrev = true;
                _lastPt  = ms.pt;
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            if (_hasPrev)
            {
                int deltaY = ms.pt.Y - _lastPt.Y;

                if (deltaY != 0)
                {
                    int newY = ms.pt.Y - 2 * deltaY;

                    // Convert to normalised absolute coords (0–65535)
                    int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
                    int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
                    int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                    int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);

                    int absX = (int)(((long)(ms.pt.X - vx) * 65535) / (vw - 1));
                    int absY = (int)(((long)(newY      - vy) * 65535) / (vh - 1));

                    absX = Math.Clamp(absX, 0, 65535);
                    absY = Math.Clamp(absY, 0, 65535);

                    var inputs = new INPUT[]
                    {
                        new INPUT
                        {
                            type = 0,
                            mi   = new MOUSEINPUT
                            {
                                dx          = absX,
                                dy          = absY,
                                dwFlags     = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK,
                                dwExtraInfo = INJECTED_SENTINEL
                            }
                        }
                    };

                    SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());

                    // Suppress the original event
                    _lastPt  = new POINT { X = ms.pt.X, Y = newY };
                    return (IntPtr)1;
                }
            }

            _hasPrev = true;
            _lastPt  = ms.pt;
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
}
