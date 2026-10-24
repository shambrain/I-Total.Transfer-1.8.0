using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ITotalTransfer.Core;
using ITotalTransfer.Devices.Afc;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

[TestClass]
public sealed class DependencyVerifierTests
{
    private static string TempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-deps", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteManifest(string dir, params NativeDependency[] dependencies)
    {
        string json = JsonSerializer.Serialize(new { SchemaVersion = 1, Dependencies = dependencies });
        File.WriteAllText(Path.Combine(dir, DependencyVerifier.ManifestFileName), json);
    }

    private static void WritePinned(string dir, string fileName, string content) =>
        File.WriteAllText(Path.Combine(dir, fileName), content);

    private static NativeDependency Pinned(string fileName, string content) =>
        new(fileName, fileName, "1.0", "x64", "MIT", "", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))), true);

    [TestMethod]
    public void UnpinnedBinaryIsRefusedEvenWhenPresent()
    {
        string dir = TempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "libfoo.dll"), "not a real dll");
            WriteManifest(dir, new NativeDependency("foo", "libfoo.dll", "1.0", "x64", "MIT", "", "", true));

            DependencyReport report = DependencyVerifier.Verify(dir);
            Assert.AreEqual(1, report.Files.Count);
            Assert.AreEqual(DependencyFileState.NotPinned, report.Files[0].State);
            Assert.IsFalse(report.RequiredDependenciesSatisfied);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void HashMismatchIsReported()
    {
        string dir = TempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "libfoo.dll"), "content");
            WriteManifest(dir, new NativeDependency("foo", "libfoo.dll", "1.0", "x64", "MIT", "", new string('a', 64), true));

            DependencyReport report = DependencyVerifier.Verify(dir);
            Assert.AreEqual(DependencyFileState.HashMismatch, report.Files[0].State);
            Assert.IsFalse(report.RequiredDependenciesSatisfied);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void CorrectlyPinnedBinaryVerifies()
    {
        string dir = TempDir();
        try
        {
            string file = Path.Combine(dir, "libfoo.dll");
            File.WriteAllText(file, "content");
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("content")));
            WriteManifest(dir, new NativeDependency("foo", "libfoo.dll", "1.0", "x64", "MIT", "", hash, true));

            DependencyReport report = DependencyVerifier.Verify(dir);
            Assert.AreEqual(DependencyFileState.Verified, report.Files[0].State);
            Assert.IsTrue(report.RequiredDependenciesSatisfied);
            Assert.IsTrue(report.AnyVerified);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void MissingBinaryIsReportedAsMissing()
    {
        string dir = TempDir();
        try
        {
            WriteManifest(dir, new NativeDependency("foo", "libfoo.dll", "1.0", "x64", "MIT", "", new string('a', 64), false));
            DependencyReport report = DependencyVerifier.Verify(dir);
            Assert.AreEqual(DependencyFileState.Missing, report.Files[0].State);
            // Not required, so the required set is trivially satisfied, but nothing was verified.
            Assert.IsTrue(report.RequiredDependenciesSatisfied);
            Assert.IsFalse(report.AnyVerified);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void MissingManifestYieldsNoDependencies()
    {
        string dir = TempDir();
        try
        {
            DependencyReport report = DependencyVerifier.Verify(dir);
            Assert.AreEqual(0, report.Files.Count);
            Assert.IsTrue(report.RequiredDependenciesSatisfied);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void AMalformedNativeManifestEnablesNothingAndDoesNotThrow()
    {
        string dir = TempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, DependencyVerifier.ManifestFileName), "{ this is not json");
            DependencyReport report = DependencyVerifier.Verify(dir);

            Assert.AreEqual(0, report.Files.Count);
            using var afc = new AfcDevice(dir);
            Assert.IsFalse(afc.RuntimeAvailable);
            StringAssert.Contains(afc.AvailabilityMessage, "No AFC dependency manifest entries");
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void AWindowsLockOnANativeBinaryIsARefusalNotACrash()
    {
        string dir = TempDir();
        try
        {
            string file = Path.Combine(dir, "libfoo.dll");
            File.WriteAllText(file, "content");
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("content")));
            WriteManifest(dir, new NativeDependency("foo", "libfoo.dll", "1.0", "x64", "MIT", "", hash, true));

            using (FileStream locked = new(file, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                DependencyReport report = DependencyVerifier.Verify(dir);
                Assert.AreEqual(DependencyFileState.HashMismatch, report.Files[0].State);
                StringAssert.Contains(report.Files[0].Detail, "could not be read");
                Assert.IsFalse(report.RequiredDependenciesSatisfied);
            }
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void AfcProbeDeclaresItsAppleFamilyEvenWhenTheRuntimeIsDisabled()
    {
        string dir = TempDir();
        try
        {
            using var afc = new AfcDevice(dir);
            DeviceInfo info = afc.Probe();

            Assert.AreEqual(DeviceStatus.NotFound, info.Status);
            Assert.AreEqual(DeviceFamily.Apple, info.Family,
                "AFC is an Apple-only backend; an unknown family would let the UI call an iPhone a generic phone.");
            Assert.AreEqual("AFC / libimobiledevice", info.BackendName);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void ANativeManifestCannotPointOutsideTheNativeFolder()
    {
        string dir = TempDir();
        try
        {
            WriteManifest(dir, new NativeDependency("evil", "..\\evil.dll", "1.0", "x64", "MIT", "",
                new string('A', 64), true));

            DependencyReport report = DependencyVerifier.Verify(dir);

            Assert.AreEqual(1, report.Files.Count);
            Assert.AreEqual(DependencyFileState.NotPinned, report.Files[0].State);
            StringAssert.Contains(report.Files[0].Detail, "outside the native folder");
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void ANullDependencyListIsTreatedAsAnEmptyManifest()
    {
        string dir = TempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, DependencyVerifier.ManifestFileName),
                "{\"schemaVersion\":1,\"dependencies\":null}");

            DependencyReport report = DependencyVerifier.Verify(dir);

            Assert.AreEqual(0, report.Files.Count);
            using var afc = new AfcDevice(dir);
            Assert.IsFalse(afc.RuntimeAvailable);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void APinnedRuntimeThatWindowsCannotLoadStaysDisabled()
    {
        string dir = TempDir();
        try
        {
            // The hash matches, so dependency verification succeeds; the file is still not a valid
            // DLL. A constructor must not announce an available backend and defer the failure to the
            // first P/Invoke in the middle of a transfer.
            WritePinned(dir, "libimobiledevice-1.0.dll", "this is not a PE image");
            WriteManifest(dir, Pinned("libimobiledevice-1.0.dll", "this is not a PE image"));

            using var afc = new AfcDevice(dir);

            Assert.IsFalse(afc.RuntimeAvailable);
            Assert.IsFalse(afc.Capabilities.CanEnumerate);
            StringAssert.Contains(afc.AvailabilityMessage, "could not be loaded");
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void AfcStaysDisabledWhenOneListedRuntimeBinaryIsMissing()

    {
        string dir = TempDir();
        try
        {
            WritePinned(dir, "libimobiledevice-1.0.dll", "imobiledevice");
            WritePinned(dir, "libplist-2.0.dll", "plist");
            WriteManifest(dir,
                Pinned("libimobiledevice-1.0.dll", "imobiledevice"),
                Pinned("libplist-2.0.dll", "plist"),
                Pinned("libusbmuxd-2.0.dll", "usbmuxd"));

            using var afc = new AfcDevice(dir);

            Assert.IsFalse(afc.RuntimeAvailable,
                "Verifying only libimobiledevice would let the backend start with missing runtime pieces.");
            StringAssert.Contains(afc.AvailabilityMessage, "libusbmuxd-2.0.dll");
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void ShippedManifestHasNoPinnedBinaryThatCouldSilentlyEnableAfc()
    {
        // Fail-closed contract: the manifest that ships must only list binaries that require a pinned
        // hash, and none of them may be shipped unpinned. This documents the "no DLLs bundled" stance.
        string nativeDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "I-Total.Transfer", "native");
        nativeDir = Path.GetFullPath(nativeDir);
        if (!Directory.Exists(nativeDir)) Assert.Inconclusive($"Native directory not found at {nativeDir}.");
        DependencyReport report = DependencyVerifier.Verify(nativeDir);
        Assert.IsTrue(report.Files.All(f => f.State == DependencyFileState.Missing || f.Dependency.IsPinned),
            "No dependency may be enabled without a pinned SHA-256.");
    }
}
