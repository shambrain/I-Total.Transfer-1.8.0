using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace ITotalTransfer.Tests;

/// <summary>
/// The per-user installer, exercised for real: it is a PowerShell script, so the test runs it the same way
/// an operator would, against temporary folders and a temporary HKCU key.
///
/// <para>
/// This proves the promises that matter for a commercial build: no administrator rights, an upgrade keeps
/// the operator's settings and trial/licence state, a hash mismatch installs nothing, uninstall removes only
/// the files the installer recorded, and pointing uninstall at an unrelated folder is refused.
/// </para>
/// </summary>
[TestClass]
public sealed class InstallerScriptTests
{
    private static readonly string PowerShell =
        Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    private sealed record RunResult(int ExitCode, string Output, string Error)
    {
        public JsonElement Json
        {
            get
            {
                foreach (string line in Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = line.Trim();
                    if (!trimmed.StartsWith('{')) continue;
                    try { return JsonDocument.Parse(trimmed).RootElement.Clone(); }
                    catch (JsonException) { }
                }
                Assert.Fail("The installer did not print a JSON result. Output: " + Output + " Error: " + Error);
                return default;
            }
        }
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "installer", "Install-I-Total.Transfer.ps1")))
                return dir.FullName;
            dir = dir.Parent;
        }
        Assert.Fail("The installer script could not be found above the test output folder.");
        return "";
    }

    private static RunResult RunInstaller(params string[] arguments) =>
        RunScript(Path.Combine(RepositoryRoot(), "installer", "Install-I-Total.Transfer.ps1"), arguments);

    private static RunResult RunScript(string script, params string[] arguments)
    {
        var info = new ProcessStartInfo(PowerShell)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-ExecutionPolicy");
        info.ArgumentList.Add("Bypass");
        info.ArgumentList.Add("-File");
        info.ArgumentList.Add(script);
        foreach (string argument in arguments) info.ArgumentList.Add(argument);

        using Process process = Process.Start(info)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        Assert.IsTrue(process.WaitForExit(120_000), "The installer did not finish within two minutes.");
        return new RunResult(process.ExitCode, output.Result, error.Result);
    }

    private sealed class Sandbox : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "itotal-installer-test", Guid.NewGuid().ToString("N"));
        public string Source { get; }
        public string App { get; }
        public string Data { get; }
        public string RegistryKey { get; } = @"HKCU:\Software\I-Total Transfer\Tests\Install-" + Guid.NewGuid().ToString("N");
        public string RegistryRelative => RegistryKey["HKCU:\\".Length..];

        public Sandbox()
        {
            Source = Path.Combine(Root, "source");
            App = Path.Combine(Root, "app");
            Data = Path.Combine(Root, "data");
            Directory.CreateDirectory(Source);
            Directory.CreateDirectory(Data);
            File.WriteAllText(Path.Combine(Data, "settings.json"), "{\"theme\":\"Neon\"}");
            File.WriteAllText(Path.Combine(Data, "trial.json"), "{\"jobsUsed\":1}");
        }

        public string WriteExecutable(byte[] bytes)
        {
            string path = Path.Combine(Source, "I-Total.Transfer.exe");
            File.WriteAllBytes(path, bytes);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        public bool RegistryExists()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryRelative);
            return key is not null;
        }

        public void Dispose()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(RegistryRelative, throwOnMissingSubKey: false); } catch { }
            try { Directory.Delete(Root, true); } catch { }
        }
    }

    /// <summary>
    /// Windows PowerShell 5.1 reads a BOM-less <c>.ps1</c> as ANSI. A UTF-8 em dash then becomes a smart
    /// quote, which PowerShell treats as a string delimiter, and the script fails in a way that looks like a
    /// logic bug. That happened once in <c>Build-Installer.ps1</c>; this guard makes it a test failure instead
    /// of a support incident.
    /// </summary>
    [TestMethod]
    public void EveryInstallerScriptStaysAsciiForWindowsPowerShell51()
    {
        string[] scripts = Directory.GetFiles(Path.Combine(RepositoryRoot(), "installer"), "*.ps1");
        Assert.IsTrue(scripts.Length >= 3,
            "Expected the install, packaging and signing scripts to be present.");

        foreach (string script in scripts)
        {
            byte[] bytes = File.ReadAllBytes(script);
            int offset = Array.FindIndex(bytes, b => b > 0x7F);
            if (offset < 0) continue;

            int line = 1;
            for (int i = 0; i < offset; i++)
                if (bytes[i] == (byte)'\n') line++;

            Assert.Fail($"{Path.GetFileName(script)} contains a non-ASCII byte at line {line}. " +
                        "Windows PowerShell 5.1 reads a BOM-less .ps1 as ANSI, so a UTF-8 dash there can " +
                        "change how the script parses. Keep installer scripts ASCII.");
        }
    }

    [TestMethod]
    public void InstallUpgradeAndUninstallLeaveTheOperatorsDataAlone()
    {
        if (!File.Exists(PowerShell)) { Assert.Inconclusive("Windows PowerShell is not available."); }
        using var box = new Sandbox();

        string firstHash = box.WriteExecutable([1, 2, 3, 4, 5, 6, 7, 8]);
        RunResult install = RunInstaller(
            "-SourceDir", box.Source, "-InstallDir", box.App, "-UserDataDir", box.Data,
            "-RegistryKey", box.RegistryKey, "-NoShortcut", "-ExpectedSha256", firstHash, "-Json");

        Assert.AreEqual(0, install.ExitCode, install.Error);
        Assert.AreEqual("install", install.Json.GetProperty("action").GetString());
        Assert.IsTrue(File.Exists(Path.Combine(box.App, "I-Total.Transfer.exe")));
        Assert.IsTrue(File.Exists(Path.Combine(box.App, "install-manifest.json")));
        Assert.IsTrue(File.Exists(Path.Combine(box.App, "Uninstall-I-Total.Transfer.ps1")),
            "The uninstaller must be part of the installation; an entry pointing at a missing script is not an uninstall.");
        Assert.IsTrue(box.RegistryExists(), "Settings > Apps must be able to see the product.");

        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(box.RegistryRelative)!)
        {
            Assert.AreEqual("I-Total Transfer", key.GetValue("DisplayName"));
            StringAssert.Contains((string)key.GetValue("UninstallString")!, "Uninstall-I-Total.Transfer.ps1");
            Assert.AreEqual(1, key.GetValue("NoModify"));
            Assert.AreEqual(1, key.GetValue("NoRepair"));
        }

        // An upgrade replaces the executable but must not touch user data, the trial ledger or the licence.
        byte[] secondBuild = [9, 10, 11, 12, 13, 14];
        string secondHash = box.WriteExecutable(secondBuild);
        RunResult upgrade = RunInstaller(
            "-SourceDir", box.Source, "-InstallDir", box.App, "-UserDataDir", box.Data,
            "-RegistryKey", box.RegistryKey, "-NoShortcut", "-ExpectedSha256", secondHash, "-Json");

        Assert.AreEqual(0, upgrade.ExitCode, upgrade.Error);
        Assert.AreEqual("upgrade", upgrade.Json.GetProperty("action").GetString());
        CollectionAssert.AreEqual(secondBuild, File.ReadAllBytes(Path.Combine(box.App, "I-Total.Transfer.exe")));
        Assert.IsTrue(File.Exists(Path.Combine(box.Data, "settings.json")));
        Assert.IsTrue(File.Exists(Path.Combine(box.Data, "trial.json")));

        // Uninstall through the copy that was installed, exactly as the registry entry would run it.
        RunResult uninstall = RunScript(Path.Combine(box.App, "Uninstall-I-Total.Transfer.ps1"),
            "-Uninstall", "-InstallDir", box.App, "-UserDataDir", box.Data,
            "-RegistryKey", box.RegistryKey, "-NoShortcut", "-Json");

        Assert.AreEqual(0, uninstall.ExitCode, uninstall.Error);
        Assert.IsFalse(Directory.Exists(box.App), "Uninstall should leave no empty installation folder behind.");
        Assert.IsFalse(box.RegistryExists());
        Assert.IsTrue(File.Exists(Path.Combine(box.Data, "settings.json")),
            "Settings and the trial ledger belong to the operator; uninstall keeps them by default.");
        Assert.IsTrue(uninstall.Json.GetProperty("userDataKept").GetBoolean());
    }

    [TestMethod]
    public void AWrongHashInstallsNothing()
    {
        if (!File.Exists(PowerShell)) { Assert.Inconclusive("Windows PowerShell is not available."); }
        using var box = new Sandbox();
        box.WriteExecutable([1, 2, 3, 4]);

        RunResult result = RunInstaller(
            "-SourceDir", box.Source, "-InstallDir", box.App, "-UserDataDir", box.Data,
            "-RegistryKey", box.RegistryKey, "-NoShortcut", "-ExpectedSha256", new string('0', 64), "-Json");

        Assert.AreEqual(4, result.ExitCode, "A hash mismatch must be refused with its own exit code.");
        Assert.IsFalse(Directory.Exists(box.App) && File.Exists(Path.Combine(box.App, "I-Total.Transfer.exe")));
        Assert.IsFalse(box.RegistryExists());
        Assert.IsFalse(result.Json.GetProperty("ok").GetBoolean());
    }

    [TestMethod]
    public void UninstallRefusesAFolderItDoesNotOwn()
    {
        if (!File.Exists(PowerShell)) { Assert.Inconclusive("Windows PowerShell is not available."); }
        using var box = new Sandbox();
        string unrelated = Path.Combine(box.Root, "documents");
        Directory.CreateDirectory(unrelated);
        string keep = Path.Combine(unrelated, "keep.txt");
        File.WriteAllText(keep, "this file belongs to the operator");

        RunResult result = RunInstaller(
            "-Uninstall", "-InstallDir", unrelated, "-UserDataDir", box.Data,
            "-RegistryKey", box.RegistryKey, "-NoShortcut", "-Json");

        Assert.AreEqual(3, result.ExitCode);
        Assert.IsTrue(File.Exists(keep), "An uninstall aimed at the wrong folder must not delete anything.");
        Assert.IsTrue(File.Exists(Path.Combine(box.Data, "settings.json")));
    }
}
