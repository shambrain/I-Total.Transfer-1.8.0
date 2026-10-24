using System.Diagnostics;
using System.Text.Json;
using ITotalTransfer.Core;

namespace ITotalTransfer.Services;

/// <summary>
/// Loads, validates (clamps) and atomically persists <see cref="AppSettings"/>. A corrupt settings
/// file is quarantined rather than deleted, and defaults are used until the user saves again.
/// </summary>
public sealed class SettingsService
{
    public string SettingsPath { get; }

    public SettingsService(string? path = null)
    {
        SettingsPath = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "I-Total Transfer", "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), AppSettings.JsonOpts);
                if (loaded is not null) return loaded.Normalize();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings load failed: {ex.Message}");
            try { File.Move(SettingsPath, SettingsPath + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}", true); } catch { }
        }
        return AppSettings.Defaults();
    }

    public void Save(AppSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        string temporary = SettingsPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, AppSettings.JsonOpts));
        if (!File.Exists(SettingsPath)) { File.Move(temporary, SettingsPath); return; }

        // Replacing a file is not atomic against everything on Windows: a virus scanner or the search
        // indexer can hold the destination for a moment, and File.Replace then fails with "Unable to
        // remove the file to be replaced" - observed once as a sporadic failure while running the test
        // suite. Losing the operator's settings (or the record of a session they set aside) to that is not
        // acceptable, so the replacement is retried briefly before it is allowed to fail.
        for (int attempt = 1; ; attempt++)
        {
            try { File.Replace(temporary, SettingsPath, null, true); return; }
            catch (IOException) when (attempt < 3) { Thread.Sleep(50 * attempt); }
        }
    }

    public AppSettings ResetToDefaults()
    {
        AppSettings defaults = AppSettings.Defaults();
        Save(defaults);
        return defaults;
    }
}
