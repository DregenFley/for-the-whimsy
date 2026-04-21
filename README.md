# For the Whimsy — Modpack

A cosy, whimsical Minecraft survival modpack. No elytras. Travel by boat, mount, and foot — building closer together actually matters.

## 🌿 For Players

Visit the [landing page](https://DregenFley.github.io/for-the-whimsy) to download the installer.

The installer will:
- Download and install the latest modpack version into CurseForge
- Let you pick any existing CurseForge profile as the source of your JourneyMap data (handy if you've renamed profiles or are migrating from another modpack), or skip to start with a fresh map
- Auto-update next time you run it

## 🔧 For the Curious

This repo is fully open so you can see exactly what the installer does before running it.

- `installer/` — C# WPF installer source code
- `version.json` — current version info fetched by the installer
- `index.html` — the landing page (served via GitHub Pages)
- Modpack zips are hosted as assets on [GitHub Releases](https://github.com/DregenFley/for-the-whimsy/releases)

## 📦 Releasing a New Version

1. Export your modpack from CurseForge as a zip
2. Create a new GitHub Release tagged `modpack-v1.x.x` and upload the zip as a release asset
3. Update `version.json`:
   ```json
   {
     "version": "1.x.x",
     "download_url": "https://github.com/DregenFley/for-the-whimsy/releases/download/modpack-v1.x.x/For.The.Whimsy.zip",
     "changelog": "What changed in this version"
   }
   ```
4. Commit and push `version.json` — players get the update next time they run the installer

## VirusTotal

[View the latest scan →](https://www.virustotal.com/YOUR_VIRUSTOTAL_LINK)
