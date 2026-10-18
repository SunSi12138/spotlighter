using System;
using System.Globalization;
using System.IO;
namespace Spotlighter;

internal sealed class LightSettings
{
    internal static LightSettings Current { get; private set; } = Load();
    internal float Size = 1, Brightness = 1, Softness = 1, Ambient = .01f, Temperature = 0;
    internal static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spotlighter");
    internal static string? SaveError;
    internal static void Reset() => Current = new LightSettings();
    internal void Clamp()
    {
        Size = Bound(Size, .35f, 2.5f, 1); Brightness = Bound(Brightness, .2f, 1.8f, 1);
        Softness = Bound(Softness, 0, 2.5f, 1); Ambient = Bound(Ambient, 0, .3f, .01f);
        Temperature = Bound(Temperature, -1, 1, 0);
    }
    private static float Bound(float x, float min, float max, float fallback) => float.IsFinite(x) ? Math.Clamp(x, min, max) : fallback;
    private static LightSettings Load()
    {
        LightSettings result = new();
        bool currentVersion = false, normalizedTemperature = false;
        try
        {
            foreach (string line in File.ReadAllLines(Path.Combine(Folder, "settings.ini")))
            {
                string[] parts = line.Split('=');
                if (parts.Length != 2 || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) continue;
                if (parts[0] == "Version") { currentVersion=value>=4; normalizedTemperature=value>=5; }
                switch (parts[0]) { case "Size": result.Size = value; break; case "Brightness": result.Brightness = value; break; case "Softness": result.Softness = value; break; case "Ambient": result.Ambient = value; break; case "Temperature": result.Temperature = value; break; }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        // Apply the new ambient default once when upgrading an older settings file.
        if (!currentVersion) result.Ambient = .01f;
        if (!normalizedTemperature) result.Temperature = 0;
        result.Clamp(); return result;
    }
    internal static void Save()
    {
        try
        {
            Current.Clamp(); Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "settings.ini");
            File.WriteAllText(path + ".tmp", string.Create(CultureInfo.InvariantCulture, $"Version=5\nSize={Current.Size}\nBrightness={Current.Brightness}\nSoftness={Current.Softness}\nAmbient={Current.Ambient}\nTemperature={Current.Temperature}\n"));
            File.Move(path + ".tmp", path, true); SaveError = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { SaveError = e.Message; }
    }
}
