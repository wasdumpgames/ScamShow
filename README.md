# ScamShow

A lightweight Windows console utility for tracking **jump scare counts** during live streams, recordings, or gaming sessions.  
Counts are updated in real-time via global hotkeys, persisted to your AppData folder, and exported to a plain `.txt` file that can be dropped straight into OBS or any overlay tool.

---

## Features

- **Global hotkeys** — increment, decrement, or toggle settings from any application
- **Live `.txt` export** — `JumpScareCount.txt` updates instantly, ready for OBS text sources
- **Persistent state** — count and settings survive restarts (stored as JSON in AppData)
- **Mouse Y-axis inversion** — toggle inverted mouse Y globally at any time
- **Configurable hotkeys** — rebind every action from inside the app; all actions must be bound before saving
- **Clipboard helper** — copy the `.txt` file path straight to your clipboard for easy OBS setup

---

## Requirements

| Requirement | Version |
|---|---|
| OS | Windows 10 / 11 |
| Runtime | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |

> **Note:** This is a Windows-only application. It uses low-level Win32 keyboard and mouse hooks (`WH_KEYBOARD_LL` / `WH_MOUSE_LL`).

---

## Installation

1. Download the latest release from the [Releases](../../releases) page.
2. Extract and run `ScamShow.exe` — no installer required.

**Or build from source:**

```bash
git clone https://github.com/wasdumpgames/ScamShow.git
cd ScamShow
dotnet build -c Release
```

---

## Usage

### Main Menu

```
╔══════════════════════════════════╗
║         SCAM SHOW  v1.0          ║
╚══════════════════════════════════╝

  Current Jump Scare Count: 0

  1) Start Jump Scare Counter
  2) Reset Jump Scare Counter to 0
  3) Configure Hotkeys
  4) Copy .txt file path to clipboard
  5) Invert Mouse Y Axis          [OFF]
  6) Quit
```

| Option | Description |
|---|---|
| **1** | Activates global hotkeys and starts listening for jump scare inputs |
| **2** | Resets the counter to `0` and saves immediately |
| **3** | Opens the hotkey configuration screen |
| **4** | Copies the full path of `JumpScareCount.txt` to your clipboard |
| **5** | Toggles global mouse Y-axis inversion on or off |
| **6** | Exits the application |

### Default Hotkeys

| Action | Default |
|---|---|
| Increment count | `Ctrl + +` |
| Decrement count | `Ctrl + -` |
| Toggle mouse Y invert | `Ctrl + M` |
| Stop counter / quit to menu | `Ctrl + Q` |

All hotkeys are **global** — they work regardless of which application has focus.

---

## OBS / Overlay Setup

1. Launch ScamShow and press **4** to copy the `.txt` file path.
2. In OBS, add a **Text (GDI+)** source.
3. Check **"Read from file"** and paste the copied path.
4. The text source will update automatically whenever the count changes.

The file is located at:

```
%APPDATA%\ScamShow\JumpScareCount.txt
```

---

## Data Files

All files are stored in `%APPDATA%\ScamShow\`:

| File | Contents |
|---|---|
| `JumpScareCount.txt` | Current count as plain text (for overlays) |
| `state.json` | Persisted counter value and mouse invert setting |
| `config.json` | Hotkey bindings |

---

## Configuring Hotkeys

Select **3) Configure Hotkeys** from the main menu.  
Press the number next to any action, then press your desired key combination.  
All actions must have a binding before you can save — the app will warn you if any are unbound.

---

## License

MIT — see [LICENSE](LICENSE) for details.
