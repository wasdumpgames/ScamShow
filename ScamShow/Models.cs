namespace ScamShow;

public enum HotkeyAction
{
    IncrementCount,
    DecrementCount,
    Quit,
    ToggleMouseInvert
}

public class HotkeyBinding
{
    public int VirtualKeyCode { get; set; }
    public bool Ctrl { get; set; }
    public bool Shift { get; set; }
    public bool Alt { get; set; }

    private static readonly Dictionary<int, string> OemNames = new()
    {
        [0xBA] = ";",  [0xBB] = "+",  [0xBC] = ",",  [0xBD] = "-",
        [0xBE] = ".",  [0xBF] = "/",  [0xC0] = "`",  [0xDB] = "[",
        [0xDC] = "\\", [0xDD] = "]",  [0xDE] = "'",
    };

    public override string ToString()
    {
        var parts = new List<string>();
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");

        if (OemNames.TryGetValue(VirtualKeyCode, out var symbol))
            parts.Add(symbol);
        else
            parts.Add(((System.Windows.Forms.Keys)VirtualKeyCode).ToString());

        return string.Join(" + ", parts);
    }
}

public class AppConfig
{
    public Dictionary<HotkeyAction, HotkeyBinding> Hotkeys { get; set; } = new()
    {
        [HotkeyAction.IncrementCount] = new HotkeyBinding { VirtualKeyCode = 0xBB, Ctrl = true }, // Ctrl + =  (OEM_PLUS)
        [HotkeyAction.DecrementCount] = new HotkeyBinding { VirtualKeyCode = 0xBD, Ctrl = true }, // Ctrl + -  (OEM_MINUS)
        [HotkeyAction.Quit]              = new HotkeyBinding { VirtualKeyCode = (int)System.Windows.Forms.Keys.Q, Ctrl = true },
        [HotkeyAction.ToggleMouseInvert] = new HotkeyBinding { VirtualKeyCode = (int)System.Windows.Forms.Keys.M, Ctrl = true }
    };
}

public class AppState
{
    public int JumpScareCount { get; set; } = 0;
    public bool MouseYInverted { get; set; } = false;
    public string TextFilePrefix { get; set; } = "Scam Scares";
}
