using ITotalTransfer.Core;
using ITotalTransfer.Services;
using ITotalTransfer.UI;

namespace ITotalTransfer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // The persisted theme is read before the first window exists so the native Win32 common
        // controls (scroll bars, text boxes, list views, menus) adopt the same light/dark colour mode
        // as the custom-painted Fluent surfaces. In .NET 10 this API is no longer experimental.
        AppSettings settings;
        try { settings = new SettingsService().Load(); }
        catch { settings = AppSettings.Defaults(); }
        Application.SetColorMode(ColorModeFor(settings.Theme));

        Application.Run(new MainForm());
    }

    /// <summary>Maps the app theme onto the Windows Forms native colour mode.</summary>
    internal static SystemColorMode ColorModeFor(AppTheme theme) => theme switch
    {
        AppTheme.Light => SystemColorMode.Classic,
        AppTheme.System => SystemColorMode.System,
        _ => SystemColorMode.Dark
    };
}
