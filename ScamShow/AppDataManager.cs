using System.Text.Json;

namespace ScamShow;

public static class AppDataManager
{
    private static readonly string AppFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScamShow");

    private static readonly string ConfigPath = Path.Combine(AppFolder, "config.json");
    private static readonly string StatePath  = Path.Combine(AppFolder, "state.json");
    public  static readonly string CountTxt   = Path.Combine(AppFolder, "JumpScareCount.txt");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    static AppDataManager()
    {
        Directory.CreateDirectory(AppFolder);
    }

    // ── Config ──────────────────────────────────────────────────────────────

    public static AppConfig LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            var def = new AppConfig();
            SaveConfig(def);
            return def;
        }
        try
        {
            var json   = File.ReadAllText(ConfigPath);
            var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts) ?? new AppConfig();

            // Backfill any hotkey actions added after the config was first saved
            var defaults = new AppConfig();
            bool dirty   = false;
            foreach (var (action, binding) in defaults.Hotkeys)
            {
                if (!loaded.Hotkeys.ContainsKey(action))
                {
                    loaded.Hotkeys[action] = binding;
                    dirty = true;
                }
            }
            if (dirty) SaveConfig(loaded);

            return loaded;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public static void SaveConfig(AppConfig config)
    {
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOpts));
    }

    // ── State ────────────────────────────────────────────────────────────────

    public static AppState LoadState()
    {
        if (!File.Exists(StatePath))
        {
            var def = new AppState();
            SaveState(def);
            return def;
        }
        try
        {
            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<AppState>(json, JsonOpts) ?? new AppState();
        }
        catch
        {
            return new AppState();
        }
    }

    public static void SaveState(AppState state)
    {
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, JsonOpts));
        File.WriteAllText(CountTxt, state.JumpScareCount.ToString());
    }
}
