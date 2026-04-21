using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WhimsyInstaller
{
    public partial class MainWindow : Window
    {
        // ─── CONFIG ───────────────────────────────────────────────────────────────
        private const string VersionJsonUrl =
            "https://raw.githubusercontent.com/DregenFley/for-the-whimsy/main/version.json";
        private const string ProfileFolderName = "For the Whimsy";
        private const string ConfigFileName = "whimsy_config.json";
        // ─────────────────────────────────────────────────────────────────────────

        private static readonly HttpClient Http = new();

        private string? _latestVersion;
        private string? _installedVersion;
        private string? _downloadUrl;
        private string? _changelog;
        private string? _instancesPath;
        private string? _profilePath;
        private string? _selectedSourceProfilePath;
        private AppConfig _config = new();
        private List<ProfileEntry> _profiles = new();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await InitialiseAsync();
        }

        // ── INITIALISE ────────────────────────────────────────────────────────────

        private async Task InitialiseAsync()
        {
            SetFooter("connecting...", "#6aaa4a");

            _config = LoadConfig();
            _instancesPath = GetInstancesPath();
            _profilePath = Path.Combine(_instancesPath, ProfileFolderName);

            _installedVersion = _config.InstalledVersion;
            InstalledVersionText.Text = _installedVersion ?? "—";

            try
            {
                await FetchLatestVersionAsync();
            }
            catch
            {
                SetBanner("#e8d8b8", "#f5c842", "Could not connect", "Check your internet connection and try again.");
                SetFooter("offline", "#E24B4A");
                ActionButton.IsEnabled = false;
                return;
            }

            if (_installedVersion == null)
            {
                // Fresh install
                SetBanner("#e8f0de", "#6aaa4a", $"Ready to install — {_latestVersion}", _changelog ?? "");
                ActionButton.Content = "Install modpack";
                ActionButton.IsEnabled = true;
            }
            else if (_installedVersion == _latestVersion)
            {
                // Up to date
                SetBanner("#e8f0de", "#6aaa4a", "You're up to date!", $"Version {_latestVersion} is installed.");
                ActionButton.Content = "Launch CurseForge";
                ActionButton.IsEnabled = true;
            }
            else
            {
                // Update available
                SetBanner("#f0ebe0", "#EF9F27", $"Update available — {_latestVersion}", _changelog ?? "");
                ActionButton.Content = $"Update to {_latestVersion}";
                ActionButton.IsEnabled = true;
            }

            SetFooter($"connected · {(_installedVersion != null ? _installedVersion + " installed" : "not installed")}", "#6aaa4a");
        }

        private async Task FetchLatestVersionAsync()
        {
            var json = await Http.GetStringAsync(VersionJsonUrl);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            _latestVersion = root.GetProperty("version").GetString();
            _downloadUrl = root.GetProperty("download_url").GetString();
            _changelog = root.TryGetProperty("changelog", out var cl) ? cl.GetString() : null;

            LatestVersionText.Text = _latestVersion;
        }

        // ── BUTTON HANDLERS ───────────────────────────────────────────────────────

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_installedVersion == _latestVersion)
            {
                LaunchCurseForge();
                return;
            }

            // Show the pre-install profile picker instead of starting immediately.
            PopulateProfileDropdown();
            ActionButton.IsEnabled = false;
            ActionButton.Visibility = Visibility.Collapsed;
            PreInstallPanel.Visibility = Visibility.Visible;
        }

        private async void ConfirmInstallButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = ProfileDropdown.SelectedItem as ProfileEntry;
            _selectedSourceProfilePath = (selected != null && !selected.IsNone) ? selected.Path : null;

            PreInstallPanel.Visibility = Visibility.Collapsed;
            ActionButton.Visibility = Visibility.Visible;
            ActionButton.IsEnabled = false;

            await RunInstallAsync();
        }

        private void CancelPreInstallButton_Click(object sender, RoutedEventArgs e)
        {
            PreInstallPanel.Visibility = Visibility.Collapsed;
            ActionButton.Visibility = Visibility.Visible;
            ActionButton.IsEnabled = true;
        }

        private void ProfileDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = ProfileDropdown.SelectedItem as ProfileEntry;
            if (selected == null)
            {
                ProfileHint.Text = "";
                return;
            }

            if (selected.IsNone)
            {
                ProfileHint.Text = "No map data will be copied. JourneyMap will start blank.";
            }
            else if (selected.HasJourneyMap)
            {
                ProfileHint.Text = $"Will copy JourneyMap data from \"{System.IO.Path.GetFileName(selected.Path)}\".";
            }
            else
            {
                ProfileHint.Text = $"\"{System.IO.Path.GetFileName(selected.Path)}\" has no JourneyMap folder — nothing will be copied.";
            }
        }

        // ── INSTALL ───────────────────────────────────────────────────────────────

        private async Task RunInstallAsync()
        {
            LogPanel.Visibility = Visibility.Visible;
            ProgressPanel.Visibility = Visibility.Visible;

            // Step 1 — back up JourneyMap from the selected source profile (if any).
            // We back up first because the destination may be the same folder as the source
            // (e.g. updating the existing "For the Whimsy" profile in place).
            string? backupPath = null;
            string? sourceName = null;
            if (_selectedSourceProfilePath != null)
            {
                var srcJm = Path.Combine(_selectedSourceProfilePath, "journeymap");
                if (Directory.Exists(srcJm))
                {
                    sourceName = Path.GetFileName(_selectedSourceProfilePath);
                    SetLog($"Backing up JourneyMap from \"{sourceName}\"...", "", "");
                    var backupTarget = Path.Combine(Path.GetTempPath(), "whimsy_journeymap_backup");
                    backupPath = backupTarget;
                    try
                    {
                        if (Directory.Exists(backupTarget)) Directory.Delete(backupTarget, recursive: true);
                        await Task.Run(() => CopyDirectory(srcJm, backupTarget));
                    }
                    catch (Exception ex)
                    {
                        SetBanner("#fceaea", "#E24B4A", "Backup failed", ex.Message);
                        SetLog("Could not back up JourneyMap data.", "", "");
                        ActionButton.Content = "Try again";
                        ActionButton.IsEnabled = true;
                        return;
                    }
                }
            }

            // Step 2 — download
            SetLog("Fetching version info... done", "Downloading modpack from GitHub...", "");
            SetFooter("downloading...", "#6aaa4a");

            var zipPath = Path.Combine(Path.GetTempPath(), "whimsy_modpack.zip");
            try
            {
                await DownloadFileAsync(_downloadUrl!, zipPath);
            }
            catch (Exception ex)
            {
                SetBanner("#fceaea", "#E24B4A", "Download failed", ex.Message);
                SetLog("Download failed.", "", "");
                ActionButton.Content = "Try again";
                ActionButton.IsEnabled = true;
                return;
            }

            // Step 3 — extract
            SetLog("Fetching version info... done", "Download complete.", "Installing to CurseForge...");
            SetFooter("installing...", "#6aaa4a");
            await Task.Run(() => ExtractModpack(zipPath));

            // Step 4 — restore JourneyMap data into the newly-extracted profile.
            bool mapKept = false;
            if (backupPath != null && Directory.Exists(backupPath))
            {
                SetLog("Install complete.", $"Restoring JourneyMap from \"{sourceName}\"...", "");
                try
                {
                    var dest = Path.Combine(_profilePath!, "journeymap");
                    var src = backupPath;
                    if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true);
                    await Task.Run(() => CopyDirectory(src, dest));
                    Directory.Delete(backupPath, recursive: true);
                    mapKept = true;
                }
                catch (Exception ex)
                {
                    // Non-fatal: install succeeded, just couldn't restore map data.
                    MessageBox.Show(
                        $"The modpack installed successfully, but we couldn't copy your JourneyMap data:\n\n{ex.Message}\n\nYour backup is at:\n{backupPath}",
                        "For the Whimsy",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            // Step 5 — save config
            _config.InstalledVersion = _latestVersion;
            _config.ProfilePath = _profilePath;
            SaveConfig(_config);
            InstalledVersionText.Text = _latestVersion;
            _installedVersion = _latestVersion;

            FinishInstall(mapKept, sourceName);
        }

        private void ExtractModpack(string zipPath)
        {
            if (Directory.Exists(_profilePath))
                Directory.Delete(_profilePath, recursive: true);

            Directory.CreateDirectory(_profilePath!);
            ZipFile.ExtractToDirectory(zipPath, _profilePath!);
            File.Delete(zipPath);
        }

        private void FinishInstall(bool mapKept, string? sourceName)
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            LogPanel.Visibility = Visibility.Collapsed;
            PreInstallPanel.Visibility = Visibility.Collapsed;

            var mapNote = mapKept
                ? (sourceName != null ? $" · map data copied from \"{sourceName}\"" : " · map data kept")
                : "";
            SetBanner("#e8f0de", "#6aaa4a", "You're all set!", $"{_latestVersion} installed{mapNote}. Time to play!");
            SetFooter($"ready · {_latestVersion} installed", "#6aaa4a");

            ActionButton.Visibility = Visibility.Visible;
            ActionButton.Content = "Launch CurseForge";
            ActionButton.IsEnabled = true;
        }

        // ── PROFILE SCANNING ──────────────────────────────────────────────────────

        private void PopulateProfileDropdown()
        {
            _profiles = new List<ProfileEntry>();

            // Always offer a "skip" option first.
            var skipEntry = new ProfileEntry
            {
                IsNone = true,
                DisplayName = "— Start with a fresh map —",
                Path = null,
                HasJourneyMap = false,
            };
            _profiles.Add(skipEntry);

            if (!string.IsNullOrEmpty(_instancesPath) && Directory.Exists(_instancesPath))
            {
                // Enumerate top-level CurseForge profile directories.
                IEnumerable<string> dirs;
                try
                {
                    dirs = Directory.GetDirectories(_instancesPath);
                }
                catch
                {
                    dirs = Array.Empty<string>();
                }

                foreach (var dir in dirs.OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
                {
                    var name = Path.GetFileName(dir);
                    var hasJm = Directory.Exists(Path.Combine(dir, "journeymap"));
                    var label = hasJm ? $"{name}   ✓ has map data" : $"{name}   (no map data)";
                    _profiles.Add(new ProfileEntry
                    {
                        DisplayName = label,
                        Path = dir,
                        HasJourneyMap = hasJm,
                        IsNone = false,
                    });
                }
            }

            ProfileDropdown.ItemsSource = _profiles;

            // Pick a sensible default:
            //  1. "For the Whimsy" profile if it has JM data (most common — upgrading in place)
            //  2. Any profile with JM data
            //  3. The "skip" entry
            var preferred = _profiles.FirstOrDefault(p =>
                                !p.IsNone &&
                                p.HasJourneyMap &&
                                string.Equals(Path.GetFileName(p.Path), ProfileFolderName, StringComparison.OrdinalIgnoreCase))
                         ?? _profiles.FirstOrDefault(p => !p.IsNone && p.HasJourneyMap)
                         ?? skipEntry;

            ProfileDropdown.SelectedItem = preferred;
        }

        // ── LAUNCH ────────────────────────────────────────────────────────────────

        private void LaunchCurseForge()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "curseforge://",
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show("Couldn't open CurseForge automatically.\nPlease open it manually.", "For the Whimsy", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ── DOWNLOAD WITH PROGRESS ────────────────────────────────────────────────

        private async Task DownloadFileAsync(string url, string destination)
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var file = File.Create(destination);

            var buffer = new byte[8192];
            long downloaded = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read));
                downloaded += read;

                if (total > 0)
                {
                    var pct = (int)(downloaded * 100 / total);
                    Dispatcher.Invoke(() => UpdateProgress(pct));
                }
            }
        }

        private void UpdateProgress(int pct)
        {
            ProgressPercent.Text = $"{pct}%";
            var totalWidth = ((System.Windows.FrameworkElement)ProgressBar.Parent).ActualWidth;
            ProgressBar.Width = totalWidth * pct / 100;
            ProgressLabel.Text = pct < 100 ? "Downloading modpack..." : "Download complete.";
        }

        // ── CONFIG ────────────────────────────────────────────────────────────────

        private static string ConfigPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

        private static AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath)) ?? new AppConfig();
            }
            catch { }
            return new AppConfig();
        }

        private static void SaveConfig(AppConfig config)
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        }

        // ── HELPERS ───────────────────────────────────────────────────────────────

        private static string GetInstancesPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "curseforge", "minecraft", "Instances");
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                var target = Path.Combine(dest, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
        }

        private void SetBanner(string bg, string dotColor, string title, string subtitle)
        {
            StatusBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            BannerTitle.Text = title;
            BannerSubtitle.Text = subtitle;
        }

        private void SetLog(string line1, string line2, string line3)
        {
            LogLine1.Text = line1 != "" ? $"» {line1}" : "";
            LogLine2.Text = line2 != "" ? $"» {line2}" : "";
            LogLine3.Text = line3 != "" ? $"» {line3}" : "";
            LogLine3.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4a7c3f"));
        }

        private void SetFooter(string text, string dotColor)
        {
            FooterText.Text = text;
            StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dotColor));
        }
    }

    public class AppConfig
    {
        public string? InstalledVersion { get; set; }
        public string? ProfilePath { get; set; }
    }

    public class ProfileEntry
    {
        public string DisplayName { get; set; } = "";
        public string? Path { get; set; }
        public bool HasJourneyMap { get; set; }
        public bool IsNone { get; set; }
    }
}
