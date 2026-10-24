using ITotalTransfer.Core;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

[TestClass]
public sealed class SettingsTests
{
    private static string TempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-settings", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [TestMethod]
    public void DefaultsAreValidatedAndNonEmpty()
    {
        AppSettings defaults = AppSettings.Defaults();
        Assert.IsFalse(string.IsNullOrWhiteSpace(defaults.Destination));
        Assert.IsTrue(defaults.MaxRetries is >= 1 and <= 10);
        Assert.IsTrue(defaults.BufferSizeKb is 64 or 128 or 256 or 512 or 1024);
    }

    [TestMethod]
    public void TheBothPhonesPreferenceIsRememberedAndDefaultsToTheIphone()
    {
        // Two phones on one desk is a real configuration, and "whichever Windows lists first" is not a
        // choice the operator made. The preference must survive a restart, and its absence from an older
        // settings file must mean the pre-Android behaviour (iPhone first) rather than a random pick.
        Assert.AreEqual(HandsetPreference.AppleFirst, AppSettings.Defaults().PreferredHandset);

        string dir = TempDir();
        try
        {
            string path = Path.Combine(dir, "settings.json");
            AppSettings settings = AppSettings.Defaults();
            settings.PreferredHandset = HandsetPreference.AndroidFirst;
            new SettingsService(path).Save(settings);

            Assert.AreEqual(HandsetPreference.AndroidFirst, new SettingsService(path).Load().PreferredHandset);

            // An older file has no such property at all; deserialising it must not pick the other family.
            string json = File.ReadAllText(path).Replace(
                "\"PreferredHandset\": \"AndroidFirst\",", "");
            File.WriteAllText(path, json);
            Assert.AreEqual(HandsetPreference.AppleFirst, new SettingsService(path).Load().PreferredHandset);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void TheSetAsideSessionSurvivesAReload()
    {
        // The dismissal is remembered across launches, so a restarted app does not nag about a session the
        // operator has already acknowledged.
        string dir = TempDir();
        try
        {
            string path = Path.Combine(dir, "settings.json");
            AppSettings settings = AppSettings.Defaults();
            Assert.AreEqual("", settings.DismissedRecovery, "Nothing is set aside by default.");

            settings.DismissedRecovery = @"C:\backup|638900000000000000|26335|24903";
            new SettingsService(path).Save(settings);

            AppSettings reloaded = new SettingsService(path).Load();
            Assert.AreEqual(settings.DismissedRecovery, reloaded.DismissedRecovery);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void NormalizeClampsOutOfRangeValues()
    {
        var settings = new AppSettings
        {
            MaxRetries = 999,
            CopyTimeoutSeconds = 1,
            FileConcurrency = 99,
            WirelessPort = 5,
            PairingTokenMinutes = -3,
            MinFileSizeBytes = -100,
            MaxFileSizeBytes = -5,
            LogLevel = "  "
        };
        settings.Normalize();
        Assert.AreEqual(10, settings.MaxRetries);
        Assert.AreEqual(30, settings.CopyTimeoutSeconds);
        Assert.AreEqual(4, settings.FileConcurrency);
        Assert.AreEqual(1024, settings.WirelessPort);
        Assert.AreEqual(1, settings.PairingTokenMinutes);
        Assert.AreEqual(0, settings.MinFileSizeBytes);
        Assert.AreEqual(0, settings.MaxFileSizeBytes);
        Assert.AreEqual("INFO", settings.LogLevel);
    }

    [TestMethod]
    public void SaveThenLoadRoundTripsValues()
    {
        string dir = TempDir();
        try
        {
            string path = Path.Combine(dir, "settings.json");
            var service = new SettingsService(path);
            AppSettings settings = AppSettings.Defaults();
            settings.ConflictPolicy = ConflictPolicy.RenameIncoming;
            settings.VerificationPolicy = VerificationPolicy.SizeOnly;
            settings.Backend = BackendPreference.Afc;
            settings.MaxRetries = 3;
            settings.Theme = AppTheme.Light;
            service.Save(settings);

            AppSettings reloaded = new SettingsService(path).Load();
            Assert.AreEqual(ConflictPolicy.RenameIncoming, reloaded.ConflictPolicy);
            Assert.AreEqual(VerificationPolicy.SizeOnly, reloaded.VerificationPolicy);
            Assert.AreEqual(BackendPreference.Afc, reloaded.Backend);
            Assert.AreEqual(3, reloaded.MaxRetries);
            Assert.AreEqual(AppTheme.Light, reloaded.Theme);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void CorruptSettingsAreQuarantinedAndDefaultsReturned()
    {
        string dir = TempDir();
        try
        {
            string path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, "{ this is not valid json ");
            AppSettings loaded = new SettingsService(path).Load();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(5, loaded.MaxRetries); // default
            Assert.IsFalse(File.Exists(path), "The corrupt file should be moved aside.");
            Assert.IsTrue(Directory.GetFiles(dir, "settings.json.corrupt-*").Length == 1);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void ResetToDefaultsWritesDefaults()
    {
        string dir = TempDir();
        try
        {
            string path = Path.Combine(dir, "settings.json");
            var service = new SettingsService(path);
            AppSettings settings = AppSettings.Defaults();
            settings.MaxRetries = 9;
            service.Save(settings);
            AppSettings defaults = service.ResetToDefaults();
            Assert.AreEqual(5, defaults.MaxRetries);
            Assert.AreEqual(5, service.Load().MaxRetries);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void TransferOptionsBufferSizeIsClamped()
    {
        var options = new TransferOptions { BufferSizeKb = 4 };
        Assert.AreEqual(64 * 1024, options.BufferSize);
        var options2 = new TransferOptions { BufferSizeKb = 100_000 };
        Assert.AreEqual(1024 * 1024, options2.BufferSize);
    }
}
