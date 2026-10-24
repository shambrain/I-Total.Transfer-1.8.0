using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ITotalTransfer.Core;
using ITotalTransfer.Licensing;

namespace ITotalTransfer.Services;

/// <summary>
/// Builds a redacted, self-contained diagnostics ZIP. It intentionally excludes file contents, pairing
/// secrets and full device identifiers so a bundle can be shared safely.
/// </summary>
public static class DiagnosticsBundle
{
    public static string Create(string destination, AppSettings settings, BackendSelection? selection,
        IReadOnlyList<BackendCandidate>? candidates, string? logPath, IEnumerable<string>? extra,
        LicenseSnapshot? license = null)
    {
        Directory.CreateDirectory(destination);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string zipPath = Path.Combine(destination, $"I-total-diagnostics-{stamp}.zip");
        string tempDir = Path.Combine(Path.GetTempPath(), "itotal-diag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "environment.txt"), BuildEnvironmentReport(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(tempDir, "settings.json"),
                LogService.RedactSensitive(JsonSerializer.Serialize(settings, AppSettings.JsonOpts)), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(tempDir, "backends.txt"), BuildBackendReport(selection, candidates), new UTF8Encoding(false));

            string nativeDir = Path.Combine(AppContext.BaseDirectory, "native");
            DependencyReport deps = DependencyVerifier.Verify(nativeDir);
            File.WriteAllText(Path.Combine(tempDir, "dependencies.txt"), BuildDependencyReport(deps), new UTF8Encoding(false));

            if (extra is not null)
                File.WriteAllText(Path.Combine(tempDir, "context.txt"),
                    LogService.RedactSensitive(string.Join(Environment.NewLine, extra)), new UTF8Encoding(false));

            File.WriteAllText(Path.Combine(tempDir, "licence.txt"), BuildLicenceReport(license), new UTF8Encoding(false));

            if (!string.IsNullOrWhiteSpace(logPath) && File.Exists(logPath))
                File.WriteAllText(Path.Combine(tempDir, "logs-tail.txt"), ReadLogTail(logPath), new UTF8Encoding(false));

            File.WriteAllText(Path.Combine(tempDir, "README.txt"),
                "I-Total Transfer diagnostics bundle.\r\n" +
                "Contains environment information, redacted settings, backend/dependency status, a licence/trial\r\n" +
                "summary and a log tail.\r\n" +
                "It does not contain photo/video content, pairing secrets, purchase identifiers, trial-ledger\r\n" +
                "fingerprints or full device identifiers.\r\n", new UTF8Encoding(false));

            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return zipPath;
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Reads the tail of the live log, redacted.
    ///
    /// <para>
    /// The application keeps the log open for writing for as long as it runs, so this has to grant write
    /// sharing. <c>File.ReadAllLines</c> does not: it grants read sharing only, Windows checks the new
    /// handle's sharing against the existing writer's access, and the read fails with a sharing violation.
    /// Because that exception escaped, <em>every</em> export failed and the button handed the operator no
    /// bundle at all - not a partial one, nothing. Found by pressing the button on a running copy.
    /// </para>
    ///
    /// <para>
    /// A log that still cannot be read is reported in its own file rather than costing the whole bundle.
    /// The bundle exists to carry evidence off a machine that is misbehaving; losing all of it because one
    /// file was unavailable would be its own failure.
    /// </para>
    /// </summary>
    private static string ReadLogTail(string logPath)
    {
        try
        {
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var lines = new List<string>();
            while (reader.ReadLine() is { } line) lines.Add(line);
            int take = Math.Min(lines.Count, 2000);
            return LogService.RedactSensitive(
                string.Join(Environment.NewLine, lines.GetRange(lines.Count - take, take)));
        }
        catch (Exception ex)
        {
            return LogService.RedactSensitive(
                $"Log tail unavailable ({ex.GetType().Name}): {ex.Message}");
        }
    }

    private static string BuildEnvironmentReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("I-Total Transfer diagnostics");
        sb.AppendLine("Generated     : " + DateTimeOffset.Now.ToString("O"));
        sb.AppendLine("OS            : " + Environment.OSVersion);
        sb.AppendLine("OS 64-bit     : " + Environment.Is64BitOperatingSystem);
        sb.AppendLine("Process arch  : " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
        sb.AppendLine("OS arch       : " + System.Runtime.InteropServices.RuntimeInformation.OSArchitecture);
        sb.AppendLine(".NET          : " + Environment.Version);
        sb.AppendLine("App version   : " + (Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown"));
        sb.AppendLine("Machine       : " + Environment.MachineName);
        sb.AppendLine("Processors    : " + Environment.ProcessorCount);
        return sb.ToString();
    }

    private static string BuildBackendReport(BackendSelection? selection, IReadOnlyList<BackendCandidate>? candidates)
    {
        var sb = new StringBuilder();
        if (selection is not null)
        {
            sb.AppendLine("Selected backend : " + selection.Description);
            sb.AppendLine("Fallback used    : " + selection.IsFallback);
            foreach (string attempt in selection.Attempts) sb.AppendLine("  attempt: " + attempt);
        }
        if (candidates is not null)
        {
            sb.AppendLine();
            sb.AppendLine("All backends:");
            foreach (BackendCandidate c in candidates)
                sb.AppendLine($"  {c.Name}: available={c.Available}; status={c.Status}; {c.Message}");
        }
        return LogService.RedactSensitive(sb.ToString());
    }

    /// <summary>
    /// The trial counter and the entitlement standing, never the purchase identifier or a job fingerprint.
    ///
    /// <para>
    /// Support needs to know why a job was refused ("2 of 3 used", "trial exhausted", "signature invalid");
    /// it never needs to identify the buyer or replay a ledger entry. That is why this is a summary rather
    /// than a copy of <c>trial.json</c>: the ledger's fingerprints are stable per job and would let a bundle
    /// confirm which transfers a person ran, which is more than the counter the operator can already see.
    /// </para>
    /// </summary>
    private static string BuildLicenceReport(LicenseSnapshot? snapshot)
    {
        if (snapshot is null)
            return "Licence and trial state: not available to this bundle." + Environment.NewLine;

        var sb = new StringBuilder();
        sb.AppendLine("I-Total Transfer - licence and trial summary");
        sb.AppendLine("Standing        : " + snapshot.Standing);
        sb.AppendLine("Channel         : " + snapshot.Channel);
        sb.AppendLine("Jobs used       : " + snapshot.JobsUsed);
        sb.AppendLine("Job limit       : " + snapshot.JobLimit);
        sb.AppendLine("Jobs remaining  : " + snapshot.JobsRemaining);
        sb.AppendLine("New job allowed : " + snapshot.CanStartJob);
        if (snapshot.ValidUntilUtc is { } validUntil)
            sb.AppendLine("Valid until     : " + validUntil.ToString("O"));
        if (!string.IsNullOrWhiteSpace(snapshot.Problem))
            sb.AppendLine("Problem         : " + snapshot.Problem);
        sb.AppendLine("Purchase id     : deliberately not included");
        sb.AppendLine("Ledger entries  : deliberately not included");
        return LogService.RedactSensitive(sb.ToString());
    }

    private static string BuildDependencyReport(DependencyReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Native directory: " + report.NativeDirectory);
        sb.AppendLine("Required satisfied: " + report.RequiredDependenciesSatisfied);
        foreach (DependencyFileStatus file in report.Files)
            sb.AppendLine($"{file.Dependency.FileName} [{file.Dependency.Version} {file.Dependency.Architecture} {file.Dependency.License}]: {file.State} — {file.Detail}");
        return sb.ToString();
    }
}
