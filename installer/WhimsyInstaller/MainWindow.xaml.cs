using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private const string ProfileFolderName = "For the Whimsy";
        private const string ConfigFileName = "whimsy_config.json";
        // ─────────────────────────────────────────────────────────────────────────

        private string? _instancesPath;
        private AppConfig _config = new();
        private List<ProfileEntry> _profiles = new();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => Initialise();
        }

        // ── INITIALISE ────────────────────────────────────────────────────────────

        private void Initialise()
        {
            _config = LoadConfig();
            _instancesPath = GetInstancesPath(_config);
            PopulateProfileDropdowns();
        }

        private void PopulateProfileDropdowns()
        {
            _profiles = new List<ProfileEntry>();

            if (!string.IsNullOrEmpty(_instancesPath) && Directory.Exists(_instancesPath))
            {
                IEnumerable<string> dirs;
                try { dirs = Directory.GetDirectories(_instancesPath); }
                catch { dirs = Array.Empty<string>(); }

                foreach (var dir in dirs.OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
                {
                    var name = Path.GetFileName(dir);
                    var hasJm = Directory.Exists(Path.Combine(dir, "journeymap"));
                    var label = hasJm ? $"{name}   ✓ has map data" : name;
                    _profiles.Add(new ProfileEntry
                    {
                        DisplayName = label,
                        Path = dir,
                        HasJourneyMap = hasJm,
                        IsNone = false,
                    });
                }
            }

            InstancesPathHint.Text = Directory.Exists(_instancesPath)
                ? $"Scanning: {_instancesPath}"
                : "Folder not found — click Browse… to locate it.";

            SourceDropdown.ItemsSource = _profiles;
            SourceDropdown.SelectedIndex = -1;

            DestDropdown.ItemsSource = _profiles;
            var preferred = _profiles.FirstOrDefault(p =>
                string.Equals(Path.GetFileName(p.Path), ProfileFolderName, StringComparison.OrdinalIgnoreCase));
            DestDropdown.SelectedItem = preferred;
        }

        // ── BUTTON HANDLERS ───────────────────────────────────────────────────────

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select your CurseForge Instances folder",
                InitialDirectory = Directory.Exists(_instancesPath) ? _instancesPath : null,
            };

            if (dialog.ShowDialog() == true)
            {
                _instancesPath = dialog.FolderName;
                _config.InstancesPath = _instancesPath;
                SaveConfig(_config);
                PopulateProfileDropdowns();
            }
        }

        private void SourceDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nothing needed here for now — reserved for future hints.
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var source = SourceDropdown.SelectedItem as ProfileEntry;
            var dest = DestDropdown.SelectedItem as ProfileEntry;

            if (source == null || dest == null)
            {
                ShowBanner("#fceaea", "#E24B4A", "Select profiles", "Please choose both a source and destination profile.");
                return;
            }

            if (source.Path == dest.Path)
            {
                ShowBanner("#fceaea", "#E24B4A", "Same profile", "Source and destination can't be the same profile.");
                return;
            }

            bool copyJm      = JourneyMapCheck.IsChecked == true;
            bool copyOptions = OptionsCheck.IsChecked == true;
            bool copyServers = ServersCheck.IsChecked == true;

            if (!copyJm && !copyOptions && !copyServers)
            {
                ShowBanner("#fceaea", "#E24B4A", "Nothing selected", "Please select at least one thing to copy.");
                return;
            }

            CopyButton.IsEnabled = false;
            SetFooter("copying...", "#EF9F27");

            var copied  = new List<string>();
            var skipped = new List<string>();
            var errors  = new List<string>();

            await Task.Run(() =>
            {
                // JourneyMap
                if (copyJm)
                {
                    var src = Path.Combine(source.Path!, "journeymap");
                    var dst = Path.Combine(dest.Path!, "journeymap");
                    if (Directory.Exists(src))
                    {
                        try
                        {
                            if (Directory.Exists(dst)) Directory.Delete(dst, recursive: true);
                            CopyDirectory(src, dst);
                            copied.Add("JourneyMap data");
                        }
                        catch (Exception ex) { errors.Add($"JourneyMap: {ex.Message}"); }
                    }
                    else skipped.Add("JourneyMap data (not found in source)");
                }

                // options.txt
                if (copyOptions)
                {
                    var src = Path.Combine(source.Path!, "options.txt");
                    var dst = Path.Combine(dest.Path!, "options.txt");
                    if (File.Exists(src))
                    {
                        try { File.Copy(src, dst, overwrite: true); copied.Add("settings & keybinds"); }
                        catch (Exception ex) { errors.Add($"Settings: {ex.Message}"); }
                    }
                    else skipped.Add("settings & keybinds (not found in source)");
                }

                // servers.dat
                if (copyServers)
                {
                    var src = Path.Combine(source.Path!, "servers.dat");
                    var dst = Path.Combine(dest.Path!, "servers.dat");
                    if (File.Exists(src))
                    {
                        try { File.Copy(src, dst, overwrite: true); copied.Add("server list"); }
                        catch (Exception ex) { errors.Add($"Server list: {ex.Message}"); }
                    }
                    else skipped.Add("server list (not found in source)");
                }
            });

            CopyButton.IsEnabled = true;

            if (errors.Any())
            {
                ShowBanner("#fceaea", "#E24B4A", "Some errors occurred", string.Join(" · ", errors));
                SetFooter("finished with errors", "#E24B4A");
            }
            else if (copied.Any())
            {
                var skippedNote = skipped.Any() ? $" · Skipped: {string.Join(", ", skipped)}" : "";
                ShowBanner("#e8f0de", "#6aaa4a", "Done!", $"Copied {string.Join(", ", copied)}.{skippedNote}");
                SetFooter("done", "#6aaa4a");
            }
            else
            {
                ShowBanner("#e8d8b8", "#f5c842", "Nothing copied", string.Join(" · ", skipped));
                SetFooter("ready", "#6aaa4a");
            }
        }

        // ── HELPERS ───────────────────────────────────────────────────────────────

        private static string GetInstancesPath(AppConfig config)
        {
            if (!string.IsNullOrEmpty(config.InstancesPath))
                return config.InstancesPath;

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "curseforge", "minecraft", "Instances");
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                var target   = Path.Combine(dest, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
        }

        private void ShowBanner(string bg, string dotColor, string title, string subtitle)
        {
            StatusBanner.Visibility = Visibility.Visible;
            StatusBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            BannerDot.Fill          = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dotColor));
            BannerTitle.Text        = title;
            BannerSubtitle.Text     = subtitle;
        }

        private void SetFooter(string text, string dotColor)
        {
            FooterText.Text  = text;
            StatusDot.Fill   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dotColor));
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
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config,
                new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    public class AppConfig
    {
        public string? InstancesPath { get; set; }
    }

    public class ProfileEntry
    {
        public string  DisplayName  { get; set; } = "";
        public string? Path         { get; set; }
        public bool    HasJourneyMap { get; set; }
        public bool    IsNone       { get; set; }
    }
}
