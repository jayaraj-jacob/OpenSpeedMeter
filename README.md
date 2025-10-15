# OpenSpeedMeter

A tiny **.NET 8 WinForms** utility that shows your **current network speed** — **download (↓)** and **upload (↑)** — in two places:

* A **system tray** (notification area) icon that updates every second (hover shows full values).
* A **text-only “SpeedBand” overlay** that docks near the Windows taskbar (looks native; no background rectangle).

Tested on Windows 10/11.

---

## ✨ Features

* Live bandwidth (aggregate of active NICs) using `NetworkInterface` IPv4 stats
* Compact tray icon with arrows and numbers
* **Taskbar-docked text overlay** (transparent; just the text)
* Auto-repositions for taskbar edge, DPI, and display changes
* Handles tray overflow (chevron) opening/closing
* Minimal CPU footprint

---

## 🧱 Project layout

```
NetSpeedTray/
├─ Program.cs                    // app entry; runs NetworkSpeedContext
├─ NetworkSpeedContext.cs        // headless context: tray icon, timer, speed calc
├─ SpeedBandForm.cs              // borderless transparent text-only band
└─ TaskbarUtil.cs                // taskbar/tray/overflow detection + docking
```

> If you used the earlier single-file sample, just add `SpeedBandForm.cs` and `TaskbarUtil.cs` and wire them into `NetworkSpeedContext`.

---

## 🛠️ Requirements

* Windows 10/11 (x64 or ARM64)
* .NET 8 SDK (to build)
* No admin rights required to run

---

## 🚀 Build & Run (dev)

```bash
dotnet build
dotnet run
```

You’ll see:

* A tray icon with up/down arrows; hover for full speeds.
* A small **text-only** readout docked by the taskbar (moves with the taskbar).

Right-click the tray icon → **Exit**.

---

## 📦 Publish

### Option A — Portable (single EXE)

```bash
dotnet publish -c Release -r win-x64 ^
  -p:PublishSingleFile=true ^
  -p:SelfContained=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:PublishTrimmed=true
```

Output: `bin\Release\net8.0\win-x64\publish\NetSpeedTray.exe`

* Copy this folder to any PC and run.
* **Run at login** (per-user): put a shortcut to the EXE in `shell:startup`

  * Win+R → `shell:startup` → paste shortcut

### Option B — ClickOnce (auto-updates)

1. In Visual Studio: **Project → Publish…**
2. Create a **Folder** (or **Web**) profile
3. Enable **Updates** and set **Update location** (file share or website/Azure Blob)
4. **Publish** and distribute `setup.exe`
5. For auto-run at login, copy the Start Menu shortcut to `shell:startup`

> MSIX works too, but for tray apps that must auto-run and self-update easily, **ClickOnce** or a **portable EXE + Startup shortcut** is usually smoother.

---

## ⚙️ Configuration & tweaks

* **Update interval**: `_timer = new Timer { Interval = 1000 };` (ms)
* **Units**: uses **bits/sec** with SI suffix (K/M/G); change in `FormatBitsPerSec`
* **NIC filtering**: skip loopback/tunnel; add more rules if virtual adapters skew totals
* **Text look** (SpeedBand): edit fonts/positions in `SpeedBandForm.OnPaint`
* **Transparency**: `BackColor = Color.Magenta; TransparencyKey = BackColor;`
* **Positioning**: adjust `margin` in `TaskbarUtil.GetDockPoint`

---

## 🧩 How it works (short)

Every second:

1. Enumerate active NICs (`NetworkInterface.GetAllNetworkInterfaces()`).
2. Read cumulative IPv4 byte counters → compute deltas since last tick.
3. Sum deltas across NICs → convert **Bytes/sec → bits/sec**.
4. Update:

   * Tray icon (drawn to a 32×32 icon).
   * SpeedBand text (`↑ up/s` and `↓ down/s`) positioned by taskbar/tray detection.

`TaskbarUtil` uses `SHAppBarMessage` + `FindWindow(NotifyIconOverflowWindow)` to detect taskbar edge, tray rectangle, and overflow flyout, so the band stays visible even when the tray expands upward.

---

## 🧯 Troubleshooting

* **Band disappears when tray opens**
  We detect the overflow and re-dock **above** the taskbar if the flyout opens upward. If it still hides:

  * Ensure you’re using `TaskbarUtil.IsOverflowVisible()` and calling `ReassertTopmost()` after state changes.
  * Lower `_repositionTimer.Interval` to `250ms`.

* **Text looks blurry**

  * Windows display scaling can blur GDI text. Use 100–125% scaling if possible.
  * Try a slightly larger font size in `SpeedBandForm.OnPaint`.

* **Wrong totals / too high traffic**

  * Add filtering for specific virtual/adapters (by `nic.Name` or `nic.Speed`).
  * If you use VPNs frequently, the aggregate is expected to rise.

* **Multiple instances**
  Add a process-wide mutex in `Program.cs` if needed.

---

## 📄 License

MIT (or your preferred license)

---

## 🙌 Credits

Built with ❤️ on WinForms + .NET 8 GDI drawing, using `System.Net.NetworkInformation` for counters and a lightweight P/Invoke layer for taskbar detection.

---

## ✏️ Snippets you might change often

**Auto-start (PowerShell, per-user):**

```powershell
$exe = "$env:LOCALAPPDATA\NetSpeedTray\NetSpeedTray.exe"
New-Item -Force -ItemType Directory (Split-Path $exe) | Out-Null
Copy-Item ".\bin\Release\net8.0\win-x64\publish\NetSpeedTray.exe" $exe -Force
New-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" `
  -Name "NetSpeedTray" -Value $exe -PropertyType String -Force | Out-Null
```

**Shorten display to numbers only (no arrows):**

```csharp
g.DrawString($"{_down}/s", fBold, downBrush, new PointF(0, 4));
g.DrawString($"{_up}/s",   fBold, upBrush,  new PointF(52, 4));
```
