using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace HostingSpike;

/// <summary>Light or dark: follows Windows by default, or the user's choice in the gear menu (saved in the app's own settings.json).</summary>
public static class Theme
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;
    private static readonly string SettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Multipane", "settings.json");

    [DllImport("kernel32.dll")] private static extern bool GetPhysicallyInstalledSystemMemory(out long kb);

    public static bool IsDark { get; private set; }
    /// <summary>"system", "light" or "dark".</summary>
    public static string Mode { get; private set; } = "system";
    public static event Action? Changed;

    public static void Load(Application app, string? forced = null)
    {
        Mode = forced ?? ReadSavedMode();
        Apply(app, Mode, save: false);
    }

    public static void Apply(Application app, string mode, bool save = true)
    {
        Mode = mode;
        IsDark = mode == "dark" || (mode == "system" && WindowsIsDark());
        var dicts = app.Resources.MergedDictionaries;
        var theme = new ResourceDictionary { Source = new Uri(IsDark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative) };
        if (dicts.Count == 0) { dicts.Add(theme); dicts.Add(new ResourceDictionary { Source = new Uri("Themes/Controls.xaml", UriKind.Relative) }); }
        else dicts[0] = theme;
        if (save) SaveMode(mode);
        Changed?.Invoke();
    }

    private static bool WindowsIsDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
    }

    private static string ReadSavedMode()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return "system";
            var mode = JsonNode.Parse(File.ReadAllText(SettingsFile))?["appearance"]?["theme"]?.GetValue<string>();
            return mode is "light" or "dark" ? mode : "system";
        }
        catch { return "system"; }
    }

    private static void SaveMode(string mode)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            JsonObject root;
            try { root = File.Exists(SettingsFile) ? JsonNode.Parse(File.ReadAllText(SettingsFile)) as JsonObject ?? new JsonObject() : new JsonObject(); }
            catch { root = new JsonObject(); }
            root["version"] = 1;
            root["appearance"] = new JsonObject { ["theme"] = mode };
            var tmp = SettingsFile + ".tmp";
            File.WriteAllText(tmp, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, SettingsFile, true);
        }
        catch { /* a failed save only loses the preference */ }
    }

    /// <summary>Dark title bar and rounded corners through DWM. Mica itself is left out of the spike (open question Q6 in docs/tech/12).</summary>
    public static void ApplyBackdrop(IntPtr hwnd, Window window)
    {
        var dark = IsDark ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
        var corner = DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, 4);
        _ = window;
    }

    public static long TotalMemoryBytes() => GetPhysicallyInstalledSystemMemory(out var kb) ? kb * 1024 : 0;
}
