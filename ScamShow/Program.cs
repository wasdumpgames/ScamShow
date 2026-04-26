using ScamShow;
using System.Runtime.InteropServices;

// ── Bootstrap ────────────────────────────────────────────────────────────────
var config = AppDataManager.LoadConfig();
var state  = AppDataManager.LoadState();

// Safeguard: Ensure MouseYInverted defaults to false on startup (prevents stuck state)
if (state.MouseYInverted)
{
    state.MouseYInverted = false;
    AppDataManager.SaveState(state);
}

AppDataManager.SaveState(state); // ensure txt is synced on launch

// Persistent mouse invert hook — started/stopped as state changes
MouseInvertHook? mouseHook = null;
if (state.MouseYInverted)
{
    mouseHook = new MouseInvertHook();
    mouseHook.Start();
}

MainMenu();
return;

// ─────────────────────────────────────────────────────────────────────────────
// MAIN MENU
// ─────────────────────────────────────────────────────────────────────────────
void MainMenu()
{
    while (true)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════╗");
        Console.WriteLine("║         SCAM SHOW  v1.5          ║");
        Console.WriteLine("╚══════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"  Current Jump Scare Count: {state.JumpScareCount}");
        Console.WriteLine();
        Console.WriteLine("  1) Start Jump Scare Counter");
        Console.WriteLine("  2) Reset Jump Scare Counter to 0");
        Console.WriteLine("  3) Configure Hotkeys");
        Console.WriteLine($"  4) Copy .txt file path to clipboard");
        string invertLabel = state.MouseYInverted ? "ON " : "OFF";
        Console.WriteLine($"  5) Invert Mouse Y Axis          [{invertLabel}]");
        Console.WriteLine("  6) Edit Text File Prefix");
        Console.WriteLine("  7) Quit");
        Console.WriteLine();
        Console.Write("  Choice: ");

        var key = Console.ReadKey(intercept: true).KeyChar;
        switch (key)
        {
            case '1': RunCounter(); break;
            case '2': ResetCounter(); break;
            case '3': ConfigureHotkeys(); break;
            case '4': CopyTxtPath(); break;
            case '5': ToggleMouseInvert(); break;
            case '6': EditTextFilePrefix(); break;
            case '7': return;
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────z
// 1) RUN COUNTER  (global hotkeys active)
// ─────────────────────────────────────────────────────────────────────────────
void RunCounter()
{
    Console.Clear();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  Jump Scare Counter is RUNNING");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine($"  [{config.Hotkeys[HotkeyAction.IncrementCount]}]  Increment");
    Console.WriteLine($"  [{config.Hotkeys[HotkeyAction.DecrementCount]}]  Decrement");
    Console.WriteLine($"  [{config.Hotkeys[HotkeyAction.ToggleMouseInvert]}]  Toggle Mouse Y Invert");
    Console.WriteLine($"  [{config.Hotkeys[HotkeyAction.Quit]}]             Stop & return to menu");
    Console.WriteLine();

    int countRow  = Console.CursorTop;
    int invertRow = countRow + 1;
    PrintCount(countRow, state.JumpScareCount);
    PrintInvert(invertRow, state.MouseYInverted);

    var quit  = new ManualResetEventSlim(false);
    var @lock = new object();

    using var hook = new GlobalKeyboardHook(config);
    hook.HotkeyPressed += action =>
    {
        lock (@lock)
        {
            switch (action)
            {
                case HotkeyAction.IncrementCount:
                    state.JumpScareCount++;
                    AppDataManager.SaveState(state);
                    PrintCount(countRow, state.JumpScareCount);
                    break;
                case HotkeyAction.DecrementCount:
                    state.JumpScareCount--;
                    AppDataManager.SaveState(state);
                    PrintCount(countRow, state.JumpScareCount);
                    break;
                case HotkeyAction.ToggleMouseInvert:
                    ApplyMouseInvertToggle();
                    System.Threading.Thread.Sleep(100); // Give hook time to start/stop
                    PrintInvert(invertRow, state.MouseYInverted);
                    break;
                case HotkeyAction.Quit:
                    quit.Set();
                    return;
            }
        }
    };

    hook.Start();
    quit.Wait();
}

void PrintCount(int row, int count)
{
    int origRow = Console.CursorTop;
    int origCol = Console.CursorLeft;
    Console.SetCursorPosition(0, row);
    Console.Write($"  Jump Scare Count: {count}          ");
    Console.SetCursorPosition(origCol, origRow);
}

void PrintInvert(int row, bool inverted)
{
    int origRow = Console.CursorTop;
    int origCol = Console.CursorLeft;
    Console.SetCursorPosition(0, row);
    string label = inverted ? "ON " : "OFF";
    Console.Write($"  Mouse Y Invert:   {label}   ");
    Console.SetCursorPosition(origCol, origRow);
}

// ─────────────────────────────────────────────────────────────────────────────
// 5) TOGGLE MOUSE Y INVERT
// ─────────────────────────────────────────────────────────────────────────────
void ToggleMouseInvert()
{
    ApplyMouseInvertToggle();

    Console.Clear();
    string status = state.MouseYInverted ? "ENABLED" : "DISABLED";
    Console.ForegroundColor = state.MouseYInverted ? ConsoleColor.Green : ConsoleColor.Yellow;
    Console.WriteLine($"  Mouse Y Invert is now {status}.");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("  Press any key to return to the menu...");
    Console.ReadKey(intercept: true);
}

void ApplyMouseInvertToggle()
{
    state.MouseYInverted = !state.MouseYInverted;
    AppDataManager.SaveState(state);

    if (state.MouseYInverted)
    {
        mouseHook ??= new MouseInvertHook();
        mouseHook.Start();
    }
    else
    {
        mouseHook?.Stop();
        mouseHook = null;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 4) COPY TXT FILE PATH
// ─────────────────────────────────────────────────────────────────────────────
void CopyTxtPath()
{
    // Clipboard requires STA; top-level statements run MTA by default
    var sta = new Thread(() => System.Windows.Forms.Clipboard.SetText(AppDataManager.CountTxt));
    sta.SetApartmentState(ApartmentState.STA);
    sta.Start();
    sta.Join();

    Console.Clear();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  Path copied to clipboard!");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine($"  {AppDataManager.CountTxt}");
    Console.WriteLine();
    Console.WriteLine("  Press any key to return to the menu...");
    Console.ReadKey(intercept: true);
}

// ─────────────────────────────────────────────────────────────────────────────
// 2) RESET COUNTER
// ─────────────────────────────────────────────────────────────────────────────
void ResetCounter()
{
    state.JumpScareCount = 0;
    AppDataManager.SaveState(state);

    Console.Clear();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  Jump Scare Counter has been reset to 0.");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("  Press any key to return to the menu...");
    Console.ReadKey(intercept: true);
}

// ─────────────────────────────────────────────────────────────────────────────
// 3) CONFIGURE HOTKEYS
// ─────────────────────────────────────────────────────────────────────────────
void ConfigureHotkeys()
{
    // Work on a copy so we can validate before saving
    var workingConfig = new AppConfig
    {
        Hotkeys = new Dictionary<HotkeyAction, HotkeyBinding>(
            config.Hotkeys.ToDictionary(kvp => kvp.Key, kvp => new HotkeyBinding
            {
                VirtualKeyCode = kvp.Value.VirtualKeyCode,
                Ctrl           = kvp.Value.Ctrl,
                Shift          = kvp.Value.Shift,
                Alt            = kvp.Value.Alt
            }))
    };

    var actions = Enum.GetValues<HotkeyAction>();

    while (true)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  ── Configure Hotkeys ──────────────────");
        Console.ResetColor();
        Console.WriteLine();

        int i = 1;
        foreach (var action in actions)
        {
            var binding = workingConfig.Hotkeys.TryGetValue(action, out var b) ? b : null;
            string label = binding != null ? binding.ToString() : "(unbound)";
            Console.WriteLine($"  {i}) {action,-20} [{label}]");
            i++;
        }

        Console.WriteLine();
        Console.WriteLine($"  {i}) Save & Return");
        Console.WriteLine();
        Console.Write("  Choice (or ESC to cancel): ");

        var key = Console.ReadKey(intercept: true);
        Console.WriteLine();

        if (key.Key == ConsoleKey.Escape) return;

        if (key.KeyChar >= '1' && key.KeyChar < (char)('1' + actions.Length))
        {
            int idx    = key.KeyChar - '1';
            var action = actions[idx];
            BindHotkey(workingConfig, action);
        }
        else if (key.KeyChar == (char)('1' + actions.Length))
        {
            // Validate all are bound
            bool allBound = actions.All(a =>
                workingConfig.Hotkeys.ContainsKey(a) &&
                workingConfig.Hotkeys[a].VirtualKeyCode != 0);

            if (!allBound)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  All hotkeys must be bound before saving.");
                Console.ResetColor();
                Console.WriteLine("  Press any key to continue...");
                Console.ReadKey(intercept: true);
                continue;
            }

            config = workingConfig;
            AppDataManager.SaveConfig(config);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  Hotkeys saved.");
            Console.ResetColor();
            Console.WriteLine("  Press any key to return to the menu...");
            Console.ReadKey(intercept: true);
            return;
        }
    }
}

void BindHotkey(AppConfig cfg, HotkeyAction action)
{
    Console.Clear();
    Console.WriteLine($"  Binding: {action}");
    Console.WriteLine();
    Console.WriteLine("  Press the desired key combination now...");
    Console.WriteLine("  (Hold Ctrl/Shift/Alt then press the main key)");
    Console.WriteLine();

    // Wait until all modifier keys are released, then capture the next combo
    // We capture via a temporary low-level hook on the current thread.
    HotkeyBinding? captured = null;
    var done = new ManualResetEventSlim(false);

    using var captureHook = new CaptureHook(binding =>
    {
        captured = binding;
        done.Set();
    });
    captureHook.Start();
    done.Wait();
    captureHook.Stop();

    if (captured != null)
    {
        cfg.Hotkeys[action] = captured;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Bound to: [{captured}]");
        Console.ResetColor();
    }
    else
    {
        Console.WriteLine("  Capture cancelled.");
    }

    Console.WriteLine("  Press any key to continue...");
    Console.ReadKey(intercept: true);
}

// ─────────────────────────────────────────────────────────────────────────────
// 6) EDIT TEXT FILE PREFIX
// ─────────────────────────────────────────────────────────────────────────────
void EditTextFilePrefix()
{
    while (true)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  ── Edit Text File Prefix ──────────────────");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"  Current Prefix: \"{state.TextFilePrefix}\"");
        Console.WriteLine();
        Console.WriteLine("  Enter Text to Display Before Count:");
        Console.Write("  > ");

        string? input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Input cannot be empty. Press any key to try again...");
            Console.ResetColor();
            Console.ReadKey(intercept: true);
            continue;
        }

        // Show example
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  Example:");
        Console.WriteLine($"  {input} {state.JumpScareCount}");
        Console.ResetColor();
        Console.WriteLine();
        Console.Write("  Is this ok? (y/n): ");

        var confirmation = Console.ReadKey(intercept: true).KeyChar;
        Console.WriteLine();

        if (char.ToLower(confirmation) == 'y')
        {
            state.TextFilePrefix = input;
            AppDataManager.SaveState(state);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  Text File Prefix saved successfully.");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("  Press any key to return to the menu...");
            Console.ReadKey(intercept: true);
            return;
        }
        else if (char.ToLower(confirmation) == 'n')
        {
            Console.WriteLine("  Cancelled. Try again...");
            Console.WriteLine();
            continue;
        }
        else
        {
            Console.WriteLine("  Invalid choice. Press any key to try again...");
            Console.ReadKey(intercept: true);
        }
    }
}

