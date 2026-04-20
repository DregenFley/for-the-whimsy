# For the Whimsy — Modpack

A cosy, whimsical Minecraft survival modpack. No elytras. Travel by boat, mount, and foot — building closer together actually matters.

## 🌿 For Players

Visit the [landing page](https://YOUR_GITHUB_USERNAME.github.io/for-the-whimsy) to download the installer.

The installer will:
- Download and install the latest modpack version into CurseForge
- Keep your JourneyMap data across updates (with your permission)
- Auto-update next time you run it

## 🔧 For the Curious

This repo is fully open so you can see exactly what the installer does before running it.

- `installer/` — C# WPF installer source code
- `version.json` — current version info fetched by the installer
- `modpack/` — the modpack zip downloaded by the installer
- `index.html` — the landing page (served via GitHub Pages)

## 📦 Releasing a New Version

1. Export your modpack from CurseForge and place the zip in `modpack/ForTheWhimsy.zip`
2. Update `version.json`:
   ```json
   {
     "version": "1.x.x",
     "download_url": "https://raw.githubusercontent.com/YOUR_GITHUB_USERNAME/for-the-whimsy/main/modpack/ForTheWhimsy.zip",
     "changelog": "What changed in this version"
   }
   ```
3. Commit and push — players get the update next time they run the installer

## VirusTotal

[View the latest scan →](https://www.virustotal.com/YOUR_VIRUSTOTAL_LINK)
