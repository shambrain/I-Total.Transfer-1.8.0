using ITotalTransfer.Core;
using ITotalTransfer.Devices;
using ITotalTransfer.Devices.Afc;
using ITotalTransfer.Devices.Wpd;

namespace ITotalTransfer.Services;

public sealed record BackendCandidate(BackendPreference Preference, string Name, bool Available, DeviceStatus Status, string Message);

public sealed record BackendSelection(ITransferBackend Backend, string Description, IReadOnlyList<string> Attempts, bool IsFallback);

/// <summary>
/// Selects a device backend by preference with an explicit, logged fallback chain. Native operations
/// stay isolated behind <see cref="ITransferBackend"/>; this factory is the only place that knows
/// which concrete implementations exist.
/// </summary>
public static class BackendFactory
{
    public static BackendSelection Create(AppSettings settings, LogService log)
    {
        var attempts = new List<string>();
        var order = Order(settings);
        ITransferBackend? chosen = null;
        string chosenDescription = "";
        bool fallback = false;
        bool first = true;

        foreach (BackendPreference preference in order)
        {
            ITransferBackend backend = Instantiate(preference, settings);
            DeviceInfo info;
            try { info = backend.Probe(); }
            catch (Exception ex) { info = new DeviceInfo(DeviceStatus.Error, "", ex.Message); }

            attempts.Add($"{backend.Name}: {info.Status} — {info.Message}");
            log.Write($"Backend probe {backend.Name}: {info.Status} — {info.Message}");

            bool usable = info.Status is DeviceStatus.Ready or DeviceStatus.MediaAvailable;
            if (usable)
            {
                chosen = backend;
                chosenDescription = backend.Name + " — " + info.Message;
                fallback = !first;
                break;
            }
            backend.Dispose();
            first = false;
        }

        if (chosen is null)
        {
            // Nothing probed healthy: keep Shell so the authoring UI still functions and reports the
            // real device status rather than throwing during construction.
            chosen = new ShellIPhoneDevice();
            chosenDescription = chosen.Name + " — no healthy backend was detected; using the compatibility backend.";
            fallback = true;
        }

        return new BackendSelection(chosen, chosenDescription, attempts, fallback);
    }

    /// <summary>Describes every backend (for the Settings UI) without leaving probe sessions open.</summary>
    public static IReadOnlyList<BackendCandidate> Describe(AppSettings settings)
    {
        var result = new List<BackendCandidate>();
        foreach (BackendPreference preference in Order(settings))
        {
            using ITransferBackend backend = Instantiate(preference, settings);
            DeviceInfo info;
            try { info = backend.Probe(); }
            catch (Exception ex) { info = new DeviceInfo(DeviceStatus.Error, "", ex.Message); }
            result.Add(new BackendCandidate(preference, backend.Name,
                info.Status is DeviceStatus.Ready or DeviceStatus.MediaAvailable, info.Status, info.Message));
        }
        return result;
    }

    private static ITransferBackend Instantiate(BackendPreference preference, AppSettings settings) => preference switch
    {
        BackendPreference.Wpd => new WpdDevice(settings.BufferSize, settings.PreferredHandset),
        BackendPreference.Afc => new AfcDevice(),
        BackendPreference.Shell => new ShellIPhoneDevice(),
        _ => new WpdDevice(settings.BufferSize, settings.PreferredHandset)
    };

    private static IEnumerable<BackendPreference> Order(AppSettings settings)
    {
        var order = new List<BackendPreference>();
        switch (settings.Backend)
        {
            case BackendPreference.Wpd:
                order.Add(BackendPreference.Wpd);
                break;
            case BackendPreference.Afc:
                order.Add(BackendPreference.Afc);
                break;
            case BackendPreference.Shell:
                order.Add(BackendPreference.Shell);
                return order;
            default: // Auto
                order.Add(BackendPreference.Wpd);
                if (settings.AllowAfcBackend) order.Add(BackendPreference.Afc);
                break;
        }

        if (settings.AllowShellFallback && !order.Contains(BackendPreference.Shell))
            order.Add(BackendPreference.Shell);
        if (!settings.AllowAfcBackend) order.Remove(BackendPreference.Afc);
        return order;
    }
}
