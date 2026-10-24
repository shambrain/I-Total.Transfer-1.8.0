using System.Security.Cryptography;
using System.Text.Json;

namespace ITotalTransfer.Services;

/// <summary>A single pinned native dependency entry from the dependency manifest.</summary>
public sealed record NativeDependency(
    string Name,
    string FileName,
    string Version,
    string Architecture,
    string License,
    string SourceUrl,
    string Sha256,
    bool Required)
{
    public bool IsPinned =>
        !string.IsNullOrWhiteSpace(Sha256) && Sha256.Length == 64 && Sha256.All(Uri.IsHexDigit);
}

public sealed record DependencyManifest(int SchemaVersion, IReadOnlyList<NativeDependency> Dependencies);

public enum DependencyFileState { Verified, Missing, HashMismatch, NotPinned }

public sealed record DependencyFileStatus(NativeDependency Dependency, DependencyFileState State, string Detail);

public sealed record DependencyReport(IReadOnlyList<DependencyFileStatus> Files, string NativeDirectory)
{
    public bool RequiredDependenciesSatisfied =>
        Files.Where(f => f.Dependency.Required).All(f => f.State == DependencyFileState.Verified);
    public bool AnyVerified => Files.Any(f => f.State == DependencyFileState.Verified);
}

/// <summary>
/// Verifies native binary provenance before they are loaded. A binary whose SHA-256 is unknown is
/// rejected (fail-closed), so dropping an unpinned DLL into the folder can never enable it.
/// </summary>
public static class DependencyVerifier
{
    public const string ManifestFileName = "dependencies.json";

    public static DependencyManifest LoadManifest(string nativeDirectory)
    {
        string path = Path.Combine(nativeDirectory, ManifestFileName);
        try
        {
            if (!File.Exists(path))
                return new DependencyManifest(1, []);
            string json = File.ReadAllText(path);
            DependencyManifest? manifest = JsonSerializer.Deserialize<DependencyManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return manifest?.Dependencies is null ? new DependencyManifest(1, []) : manifest;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A corrupt or unreadable manifest is not a reason to crash the backend factory. It is a
            // reason to enable nothing: an empty manifest makes AFC unavailable and the operator sees
            // that no pinned runtime was found.
            return new DependencyManifest(1, []);
        }
    }

    public static DependencyReport Verify(string nativeDirectory)
    {
        var statuses = new List<DependencyFileStatus>();
        DependencyManifest manifest = LoadManifest(nativeDirectory);
        foreach (NativeDependency dep in manifest.Dependencies)
        {
            if (!IsSimpleFileName(dep.FileName))
            {
                statuses.Add(new DependencyFileStatus(dep, DependencyFileState.NotPinned,
                    "The manifest names a path outside the native folder; the entry is refused."));
                continue;
            }
            string filePath = Path.Combine(nativeDirectory, dep.FileName);
            if (!File.Exists(filePath))
            {
                statuses.Add(new DependencyFileStatus(dep, DependencyFileState.Missing, "Native binary is not present."));
                continue;
            }
            if (!dep.IsPinned)
            {
                statuses.Add(new DependencyFileStatus(dep, DependencyFileState.NotPinned, "No pinned SHA-256 is recorded; the binary is refused."));
                continue;
            }
            try
            {
                string actual = ComputeSha256(filePath);
                statuses.Add(string.Equals(actual, dep.Sha256, StringComparison.OrdinalIgnoreCase)
                    ? new DependencyFileStatus(dep, DependencyFileState.Verified, "SHA-256 verified.")
                    : new DependencyFileStatus(dep, DependencyFileState.HashMismatch, $"SHA-256 mismatch. expected={dep.Sha256}; actual={actual}"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A binary that cannot be read cannot be verified. Refuse it rather than letting an
                // exception escape and taking the backend factory down with it.
                statuses.Add(new DependencyFileStatus(dep, DependencyFileState.HashMismatch,
                    "The binary could not be read for verification: " + ex.Message));
            }
        }
        return new DependencyReport(statuses, nativeDirectory);
    }

    private static bool IsSimpleFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        !fileName.Contains('/') && !fileName.Contains('\\') && !fileName.Contains(':') &&
        string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal);

    public static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }
}
