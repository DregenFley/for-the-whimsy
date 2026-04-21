# For the Whimsy — Modpack

A cosy, whimsical Minecraft survival modpack. No elytras. Travel by boat, mount, and foot — building closer together actually matters.

## 🌿 For Players

Visit the [landing page](https://DregenFley.github.io/for-the-whimsy) to download the modpack and setup tool.

**Getting started:**
1. Download CurseForge if you don't have it already
2. Download the modpack zip and import it into CurseForge via **Minecraft → Create Custom Profile → Import**
3. Download and run the setup tool to copy your map data, keybinds, and settings from an existing profile

**The setup tool lets you copy across:**
- JourneyMap data — your explored map and waypoints
- Settings & keybinds — video settings, controls, and key bindings
- Server list — your saved server connections

## 🔧 For the Curious

This repo is fully open so you can see exactly what the setup tool does before running it.

- `installer/` — C# WPF setup tool source code
- `version.json` — current modpack version info
- `docs/index.html` — the landing page (served via GitHub Pages)
- Modpack zips and the setup tool are hosted as assets on [GitHub Releases](https://github.com/DregenFley/for-the-whimsy/releases)

## 📦 Releasing a New Modpack Version

1. Export your modpack from CurseForge and zip the profile folder directly (so `mods/`, `config/`, etc. are at the root of the zip)
2. Include `minecraftinstance.json` and `servers.dat` inside the zip
3. Name the zip `ForTheWhimsy.zip`
4. Create a new GitHub Release tagged `modpack-v1.x.x` and upload the zip as a release asset
5. Update `version.json`:
   ```json
   {
     "version": "1.x.x",
     "download_url": "https://github.com/DregenFley/for-the-whimsy/releases/download/modpack-v1.x.x/ForTheWhimsy.zip",
     "changelog": "What changed in this version"
   }
   ```
6. Commit and push `version.json` — the website download button updates automatically

## 🛠 Releasing a New Setup Tool Version

1. Build and publish the exe: `dotnet publish -c Release`
2. The output is at `installer/WhimsyInstaller/bin/Release/net8.0-windows/win-x64/publish/ForTheWhimsy-SetupTool.exe`
3. Create a new GitHub Release tagged `setuptool-v1.x.x` and upload the exe
4. Run a VirusTotal scan on the new exe and update the link below
5. Update the download URL in `docs/index.html` to point to the new tag

## VirusTotal

[View the latest scan →](https://www.virustotal.com/gui/file/fb1a39e1c9476841f4679181095b6550f772c29d3edbf4d9ecc194945952800c/detection)
