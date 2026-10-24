using Microsoft.Win32;
using ITotalTransfer.Core;

namespace ITotalTransfer.Services;

public sealed record DiagnosticItem(string Name, string Status, string Details);

public static class ConnectionDiagnostics
{
    public static IReadOnlyList<DiagnosticItem> Collect(DeviceInfo device, string? backendName = null,
        DependencyReport? dependencies = null, AttachmentReport? attachment = null)
    {
        bool android = device.Family == DeviceFamily.Android;
        var items = new List<DiagnosticItem>();
        if (attachment is not null && !android)
        {
            // First, because "is it plugged in" is the question the operator is actually asking, and
            // because the answer has to separate "attached" from "Windows still remembers it". An
            // Android device is not diagnosed by this row at all: it says nothing about Apple handsets
            // specifically, and the Android rows below answer the plugged-in question instead.
            string detail = attachment.Headline + " " + attachment.Guidance;
            if (attachment.CachedSupportNames.Count > 0)
                detail += $" (Apple support interfaces also on record: {attachment.CachedSupportNames.Count:N0} - "
                    + "these accompany a handset but cannot supply media.)";
            items.Add(new("Apple handsets seen over USB", attachment.Status, detail));
        }
        items.Add(new("Windows device access", device.Status == DeviceStatus.Ready ? "PASS" : "CHECK", device.Message));
        items.Add(new("Selected backend", string.IsNullOrWhiteSpace(backendName) ? "NONE" : backendName,
            string.IsNullOrWhiteSpace(backendName)
                ? "No device backend has been selected yet."
                : "The engine will use this backend. Capabilities are reported on the Diagnostics page."));
        items.Add(new("Device family", device.Family switch
            {
                DeviceFamily.Apple => "APPLE",
                DeviceFamily.Android => "ANDROID",
                _ => "NOT IDENTIFIED"
            },
            device.Family switch
            {
                DeviceFamily.Apple => "The handset identified itself as an iPhone or iPad, so the camera-roll rules apply.",
                DeviceFamily.Android => "The device identified itself as an Android phone or tablet, so its whole storage is read rather than only a camera roll.",
                _ => "No attached device identified itself as an iPhone, iPad or Android device. The checks below describe this PC; they are not a verdict on any particular phone."
            }));

        if (android)
        {
            // Android's two failure modes are its USB mode and its lock screen, and they present
            // identically to the operator - an empty folder - so each gets its own row.
            items.Add(new("Android USB mode", device.Status == DeviceStatus.Ready ? "FILE TRANSFER" : "CHECK PHONE",
                device.Status == DeviceStatus.Ready
                    ? "Storage was readable during this probe, which means the phone is in \"File transfer\" (MTP) mode."
                    : "Set the USB notification on the phone to \"File transfer\". \"Charging only\" hides everything, and \"Transfer photos\" (PTP) hides everything except DCIM and Pictures."));
            items.Add(new("Unlock state", device.Status == DeviceStatus.Ready ? "UNLOCKED" : "CHECK PHONE",
                device.Status == DeviceStatus.Ready
                    ? "Storage was readable while the probe ran. Android hides it again the moment the screen locks."
                    : "Unlock the phone and keep the screen awake while it transfers. Recent Android versions return an empty folder to the PC as soon as the phone locks."));
            items.Add(new("Exposed file set", "MANUAL CHECK",
                "Android answers MTP through its own MtpDatabase, which combines filesystem operations with metadata from its MediaProvider. What Windows sees is Android's MTP view of the storage, not a promise that every file the phone's own apps can see is exposed; compare against the phone's Files app if a folder looks short."));
        }
        else
        {
            bool appleDevices = IsInstalled("Apple Devices") || IsInstalled("Apple Mobile Device Support") || IsInstalled("iTunes");
            items.Add(new("Apple software", appleDevices ? "DETECTED" : "NOT DETECTED", appleDevices ? "An Apple Windows component appears to be installed. This does not guarantee the driver is working." : "Apple Devices / Apple Mobile Device Support / iTunes was not found in common uninstall registry entries."));
            string[] driverPaths =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Common Files", "Apple", "Mobile Device Support", "Drivers", "usbaapl64.inf"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Common Files", "Apple", "Mobile Device Support", "Drivers", "usbaapl64.inf"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Common Files", "Apple", "Mobile Device Support", "Drivers", "usbaapl64.sys"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Common Files", "Apple", "Mobile Device Support", "Drivers", "usbaapl64.sys")
            ];
            string? found = driverPaths.FirstOrDefault(File.Exists);
            items.Add(new("Apple USB driver files", found is not null ? "FOUND" : "NOT FOUND IN COMMON PATHS", found ?? "The expected Apple driver files were not found in common locations. Newer Apple Devices installs can use different layouts."));

            // Device-specific advice is only given when the device said what it is. With nothing
            // identified, "CHECK IPHONE" would send an Android owner looking for a Trust prompt their
            // phone will never show, so the same row names both prompts and says why it cannot choose.
            bool apple = device.Family == DeviceFamily.Apple;
            items.Add(new("Trust state",
                device.Status == DeviceStatus.Ready ? "ACCESSIBLE" : apple ? "CHECK IPHONE" : "CHECK PHONE",
                device.Status == DeviceStatus.Ready
                    ? "Media storage was accessible during this probe."
                    : apple
                        ? "Unlock the iPhone and accept Trust if prompted. Disconnect/reconnect if the prompt does not appear."
                        : "Unlock the phone and accept any prompt it shows: an iPhone asks you to tap Trust, an Android phone asks you to allow file access and choose \"File transfer\". The page cannot tell which it is until the phone identifies itself."));
            if (apple)
                items.Add(new("iCloud originals", "MANUAL CHECK", "Windows USB import can only copy media currently exposed by iOS. If Optimize iPhone Storage is enabled, cloud-only originals may not be available."));
        }
        items.Add(new("USB cable / port", "MANUAL CHECK", "Use a known data-capable cable, connect directly to the PC, avoid hubs during diagnosis, and try another USB port if enumeration is slow or unstable."));
        if (dependencies is not null)
        {
            int verified = dependencies.Files.Count(f => f.State == DependencyFileState.Verified);
            bool satisfied = dependencies.RequiredDependenciesSatisfied;
            items.Add(new("Native AFC runtime",
                satisfied ? "VERIFIED" : verified > 0 ? "PARTIAL" : "NOT PRESENT",
                verified == 0
                    ? "No pinned native binary verified, so the AFC backend stays inert (fail-closed). Install a pinned runtime to enable Windows-to-iPhone transfers."
                    : $"{verified} of {dependencies.Files.Count} native binaries verified. Required set satisfied: {satisfied}."));
        }
        return items;
    }

    private static bool IsInstalled(string displayNameContains)
    {
        string[] subKeys =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        ];
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                foreach (string subKey in subKeys)
                {
                    using RegistryKey? root = baseKey.OpenSubKey(subKey);
                    if (root is null) continue;
                    foreach (string childName in root.GetSubKeyNames())
                    {
                        using RegistryKey? child = root.OpenSubKey(childName);
                        string? displayName = child?.GetValue("DisplayName") as string;
                        if (!string.IsNullOrWhiteSpace(displayName) && displayName.Contains(displayNameContains, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Registry diagnostic failed: {ex.Message}"); }
        }
        return false;
    }
}
