using System.Diagnostics;
using System.Text.Json;
using ITotalTransfer.Core;
using ITotalTransfer.Devices;
using ITotalTransfer.Devices.Wpd;
using ITotalTransfer.Services;

namespace DeviceDrill;

/// <summary>
/// Hardware drill harness for the real Windows Portable Devices backend and the shared transfer engine.
/// It is deliberately outside the solution so the deterministic build and test suite never touch a phone.
///
/// Usage:
///   dotnet run --project tools\DeviceDrill -c Release -- probe  --family apple|android
///   dotnet run --project tools\DeviceDrill -c Release -- scan   --family apple|android [--report file.json]
///   dotnet run --project tools\DeviceDrill -c Release -- copy   --family apple|android --dest DIR
///            [--exts .heic,.mov] [--max 8] [--policy preserve|rename|overwrite] [--repeat 2]
///            [--cancel-after 20] [--no-verify] [--report file.json]
///
/// The source is only ever read. Nothing on the phone is written or deleted.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0) { Usage(); return 64; }
        string command = args[0].ToLowerInvariant();
        var options = Parse(args.Skip(1).ToArray());
        string family = options.GetValueOrDefault("family", "apple").ToLowerInvariant();
        HandsetPreference preference = family == "android" ? HandsetPreference.AndroidFirst : HandsetPreference.AppleFirst;

        try
        {
            return command switch
            {
                "probe" => Probe(preference, options),
                "scan" => await ScanAsync(preference, options),
                "copy" => await CopyAsync(preference, options),
                _ => Unknown(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("DRILL-ERROR " + ex);
            return 70;
        }
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        Usage();
        return 64;
    }

    private static void Usage() => Console.Error.WriteLine(
        "DeviceDrill probe|scan|copy --family apple|android [--dest DIR] [--exts .heic,.mov] [--max N] " +
        "[--policy preserve|rename|overwrite] [--repeat N] [--cancel-after SECONDS] [--no-verify] [--report FILE]");

    private static Dictionary<string, string> Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i];
            if (!key.StartsWith("--", StringComparison.Ordinal)) continue;
            key = key[2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                map[key] = args[++i];
            }
            else
            {
                map[key] = "1";
            }
        }
        return map;
    }

    private static int Probe(HandsetPreference preference, Dictionary<string, string> options)
    {
        using var device = new WpdDevice(1024 * 1024, preference);
        DeviceInfo info = device.Probe();
        if (options.ContainsKey("require-family") && !FamilyMatches(options.GetValueOrDefault("family", "apple"), info))
        {
            Console.Error.WriteLine($"DEVICE-FAMILY-MISMATCH requested={options.GetValueOrDefault("family", "apple")} actual={info.Family} name={info.Name}");
            return 2;
        }
        WriteJson(options, new
        {
            Action = "probe",
            Backend = device.Name,
            Status = info.Status.ToString(),
            info.Family,
            info.Name,
            info.Model,
            info.OperatingSystem,
            info.StableId,
            info.BackendName,
            info.Message,
            Capabilities = new
            {
                Notes = device.Capabilities.Notes,
                FamilySeen = device.Capabilities.Categories.Count,
                device.Capabilities.CanEnumerate,
                device.Capabilities.CanReadSource,
                device.Capabilities.CanHashSourceBytes,
                device.Capabilities.CanReadBackCommitted,
                device.Capabilities.CanWriteToDevice,
                Categories = device.Capabilities.Categories.Select(c => c.ToString()).ToArray()
            }
        });
        return info.Status.IsUsable() ? 0 : 2;
    }

    private static async Task<int> ScanAsync(HandsetPreference preference, Dictionary<string, string> options)
    {
        using var device = new WpdDevice(1024 * 1024, preference);
        DeviceInfo info = device.Probe();
        if (options.ContainsKey("require-family") && !FamilyMatches(options.GetValueOrDefault("family", "apple"), info))
        {
            Console.Error.WriteLine($"DEVICE-FAMILY-MISMATCH requested={options.GetValueOrDefault("family", "apple")} actual={info.Family} name={info.Name}");
            return 2;
        }
        if (!info.Status.IsUsable())
        {
            Console.Error.WriteLine($"DEVICE-NOT-READY {info.Status}: {info.Message}");
            return 2;
        }

        var log = new LogService(Path.Combine(Path.GetTempPath(), "opencode", "drill-logs"));
        var service = new TransferService(device, log);
        var clock = Stopwatch.StartNew();
        int lastReport = 0;
        var progress = new Progress<string>(message =>
        {
            lastReport++;
            if (lastReport % 5 == 0) Console.Error.WriteLine("SCAN " + message);
        });
        ScanResult scan = await service.ScanAsync(CancellationToken.None, progress);
        clock.Stop();

        ScanAccounting accounting = ScanAccounting.Of(scan.Files);
        var folders = scan.Report?.Folders ?? [];
        WriteJson(options, new
        {
            Action = "scan",
            Device = new { info.Status, info.Family, info.Name, info.StableId, info.Model, info.OperatingSystem },
            Backend = device.Name,
            scan.Status,
            scan.IsComplete,
            FileCount = scan.Files.Count,
            KnownBytes = accounting.KnownBytes,
            ZeroByteFiles = accounting.ZeroByteCount,
            UnknownSizeFiles = accounting.UnknownSizeCount,
            DuplicatePaths = scan.Report?.DuplicatePathCount ?? 0,
            FailedFolders = folders.Count(f => !f.Succeeded),
            FolderCount = folders.Count,
            LivePhotoPairs = scan.Report?.LivePhotoPairCount ?? 0,
            DurationSeconds = Math.Round(scan.Duration.TotalSeconds, 2),
            FilesPerSecond = scan.Duration.TotalSeconds > 0 ? Math.Round(scan.Files.Count / scan.Duration.TotalSeconds, 1) : 0,
            Warnings = scan.Warnings,
            Folders = folders.Select(f => new { f.RelativePath, f.FileCount, f.Bytes, f.Succeeded, f.Error })
        });
        return scan.Status == EnumerationStatus.Complete ? 0 : 3;
    }

    private static async Task<int> CopyAsync(HandsetPreference preference, Dictionary<string, string> options)
    {
        string destination = options.GetValueOrDefault("dest", "");
        if (string.IsNullOrWhiteSpace(destination)) { Console.Error.WriteLine("--dest is required"); return 64; }
        destination = Path.GetFullPath(destination);
        Directory.CreateDirectory(destination);

        using var device = new WpdDevice(1024 * 1024, preference);
        DeviceInfo info = device.Probe();
        if (options.ContainsKey("require-family") && !FamilyMatches(options.GetValueOrDefault("family", "apple"), info))
        {
            Console.Error.WriteLine($"DEVICE-FAMILY-MISMATCH requested={options.GetValueOrDefault("family", "apple")} actual={info.Family} name={info.Name}");
            return 2;
        }
        if (!info.Status.IsUsable())
        {
            Console.Error.WriteLine($"DEVICE-NOT-READY {info.Status}: {info.Message}");
            return 2;
        }

        var log = new LogService(Path.Combine(destination, "logs"));
        var filter = BuildFilter(options);
        int bufferKb = int.TryParse(options.GetValueOrDefault("buffer-kb", "256"), out int parsedBuffer) ? parsedBuffer : 256;
        TransferOptions transfer = TransferOptions.FromSettings(AppSettings.Defaults()) with
        {
            ConflictPolicy = ParsePolicy(options.GetValueOrDefault("policy", "preserve")),
            SkipUnchanged = true,
            MaxRetries = 2,
            BufferSizeKb = Math.Clamp(bufferKb, 64, 4096)
        };
        var service = new TransferService(device, log, transfer, filter);

        var clock = Stopwatch.StartNew();
        ScanResult scan = await service.ScanAsync(CancellationToken.None);
        Console.Error.WriteLine($"SCAN {scan.Files.Count:N0} files, status={scan.Status}, {scan.Duration.TotalSeconds:N1}s");

        int max = int.TryParse(options.GetValueOrDefault("max", "0"), out int parsedMax) ? parsedMax : 0;
        ScanResult planned = Select(scan, options, max);
        Console.Error.WriteLine($"PLAN {planned.Files.Count:N0} files selected for the copy drill.");
        foreach (MediaFile f in planned.Files.Take(5))
            Console.Error.WriteLine($"SELECTED {f.RelativePath} ({f.Size:N0} bytes, sizeKnown={f.SizeKnown})");

        int repeat = int.TryParse(options.GetValueOrDefault("repeat", "1"), out int r) ? Math.Max(1, r) : 1;
        var runs = new List<object>();
        bool cancelled = false;
        using var cts = new CancellationTokenSource();
        if (double.TryParse(options.GetValueOrDefault("cancel-after", "0"), out double cancelAfter) && cancelAfter > 0)
            cts.CancelAfter(TimeSpan.FromSeconds(cancelAfter));

        for (int run = 1; run <= repeat; run++)
        {
            int copied = 0;
            var progress = new Progress<TransferProgress>(p =>
            {
                if (p.Copied != copied)
                {
                    copied = p.Copied;
                    if (copied % 10 == 0) Console.Error.WriteLine($"COPY {p.Copied}/{p.Total} {p.CurrentFile}");
                }
            });
            var runClock = Stopwatch.StartNew();
            try
            {
                TransferSummary summary = await service.CopyAsync(planned, destination, progress, cts.Token);
                runClock.Stop();
                runs.Add(new
                {
                    Run = run,
                    ElapsedSeconds = Math.Round(runClock.Elapsed.TotalSeconds, 2),
                    summary.Completed,
                    summary.Status,
                    summary.Total,
                    summary.Copied,
                    summary.AlreadyPresent,
                    summary.Skipped,
                    summary.Failed,
                    summary.Inaccessible,
                    summary.Unverifiable,
                    summary.BytesCopied,
                    WeakestLevel = summary.WeakestLevel.ToString(),
                    summary.Reconciliation
                });
                Console.Error.WriteLine($"RUN {run}: {summary.Status}; copied={summary.Copied} already={summary.AlreadyPresent} failed={summary.Failed}");
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                Console.Error.WriteLine($"RUN {run}: CANCELLED after {clock.Elapsed.TotalSeconds:N1}s");
                runs.Add(new { Run = run, Cancelled = true, ElapsedSeconds = Math.Round(clock.Elapsed.TotalSeconds, 1) });
                break;
            }
        }
        clock.Stop();

        object? verification = null;
        if (!cancelled && !options.ContainsKey("no-verify"))
        {
            try
            {
                VerificationResult result = await service.VerifyAsync(planned, destination,
                    new Progress<TransferProgress>(_ => { }), CancellationToken.None);
                verification = new
                {
                    result.Completed,
                    result.Total,
                    result.Matching,
                    result.Missing,
                    result.WrongSize,
                    result.WrongHash,
                    result.Unverifiable,
                    result.SourceHashVerified,
                    result.DestinationHashVerified,
                    result.Repaired,
                    Issues = result.Issues
                };
                Console.Error.WriteLine($"VERIFY: completed={result.Completed} matching={result.Matching}/{result.Total} wrongHash={result.WrongHash}");
            }
            catch (Exception ex)
            {
                verification = new { Error = ex.Message };
                Console.Error.WriteLine("VERIFY-ERROR " + ex.Message);
            }
        }

        WriteJson(options, new
        {
            Action = "copy",
            Device = new { info.Status, info.Family, info.Name, info.StableId },
            Backend = device.Name,
            Destination = destination,
            ConflictPolicy = transfer.ConflictPolicy.ToString(),
            BufferSizeKb = transfer.BufferSizeKb,
            Scan = new { scan.Status, Files = scan.Files.Count, KnownBytes = ScanAccounting.Of(scan.Files).KnownBytes },
            Runs = runs,
            Cancelled = cancelled,
            Verification = verification
        });
        return cancelled ? 5 : 0;
    }

    private static ScanResult Select(ScanResult scan, Dictionary<string, string> options, int max)
    {
        IEnumerable<MediaFile> files = scan.Files;
        if (options.TryGetValue("exts", out string? exts) && !string.IsNullOrWhiteSpace(exts))
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string token in exts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                allowed.Add(token.StartsWith('.') ? token : "." + token);
            files = files.Where(f => allowed.Contains(f.Extension));
        }
        if (options.TryGetValue("contains", out string? contains) && !string.IsNullOrWhiteSpace(contains))
            files = files.Where(f => f.RelativePath.Contains(contains, StringComparison.OrdinalIgnoreCase));
        if (options.ContainsKey("zero-only")) files = files.Where(f => f.SizeKnown && f.Size == 0);
        if (options.ContainsKey("unknown-only")) files = files.Where(f => !f.SizeKnown);
        if (options.ContainsKey("sort-size")) files = files.OrderByDescending(f => f.Size);
        if (max > 0) files = files.Take(max);
        return scan with { Files = files.ToList() };
    }

    private static bool FamilyMatches(string requested, DeviceInfo info) =>
        string.Equals(requested, "android", StringComparison.OrdinalIgnoreCase)
            ? info.Family == DeviceFamily.Android
            : info.Family == DeviceFamily.Apple;

    private static ConflictPolicy ParsePolicy(string value) => value.ToLowerInvariant() switch
    {
        "rename" => ConflictPolicy.RenameIncoming,
        "overwrite" => ConflictPolicy.Overwrite,
        "skip" => ConflictPolicy.Skip,
        _ => ConflictPolicy.PreserveAndReport
    };

    private static FilterSpec BuildFilter(Dictionary<string, string> options)
    {
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (options.TryGetValue("exts", out string? raw))
            foreach (string token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                extensions.Add(token.StartsWith('.') ? token : "." + token);
        return new FilterSpec(extensions, [], [], ExcludeSidecars: false, MinSize: 0, MaxSize: 0);
    }

    private static void WriteJson(Dictionary<string, string> options, object payload)
    {
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
        if (options.TryGetValue("report", out string? path) && !string.IsNullOrWhiteSpace(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(Path.GetFullPath(path), json);
            Console.Error.WriteLine("REPORT " + Path.GetFullPath(path));
        }
    }
}
