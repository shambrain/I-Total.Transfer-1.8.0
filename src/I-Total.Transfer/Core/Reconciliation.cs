namespace ITotalTransfer.Core;

/// <summary>
/// The Part 3 reconciliation: every object the scan exposed is counted once, under the outcome that
/// actually happened. A report built from these numbers can state what was discovered, what the filter
/// excluded, what was already present, what was copied at each verification grade, and what is still
/// unresolved, without deriving any of it from a progress percentage.
///
/// <para>
/// Byte totals use only measured sizes. A file whose size the backend could not read contributes no
/// bytes and is counted in <see cref="UnknownSizeFiles"/>; its committed length, when it was copied,
/// is in <see cref="CopiedBytes"/> through <see cref="TransferEntry.BytesWritten"/>.
/// </para>
/// </summary>
public sealed record ReconciliationReport(
    int DiscoveredFiles,
    long DiscoveredKnownBytes,
    int UnknownSizeFiles,
    int EligibleFiles,
    long EligibleBytes,
    int ExcludedFiles,
    int ConflictSkippedFiles,
    int AlreadyPresentFiles,
    long AlreadyPresentBytes,
    int CopiedFiles,
    long CopiedBytes,
    int SourceHashVerifiedFiles,
    int DestinationHashVerifiedFiles,
    int SizeVerifiedFiles,
    int UnverifiableFiles,
    int InaccessibleFiles,
    int FailedFiles,
    long FailedBytes,
    int OutstandingFiles,
    int DuplicatePathCount,
    int FailedFolderCount)
{
    public static ReconciliationReport Build(ScanResult scan, IEnumerable<TransferEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(scan);
        ArgumentNullException.ThrowIfNull(entries);

        var byPath = new Dictionary<string, TransferEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (TransferEntry entry in entries)
        {
            if (!byPath.ContainsKey(entry.RelativePath)) byPath[entry.RelativePath] = entry;
        }

        ScanAccounting accounting = ScanAccounting.Of(scan.Files);
        int eligible = 0, excluded = 0, conflictSkipped = 0, alreadyPresent = 0, copied = 0;
        int sourceVerified = 0, destinationVerified = 0, sizeVerified = 0, unverifiable = 0;
        int inaccessible = 0, failed = 0, outstanding = 0;
        long eligibleBytes = 0, alreadyPresentBytes = 0, copiedBytes = 0, failedBytes = 0;

        foreach (MediaFile file in scan.Files)
        {
            string relative;
            try { relative = TransferRules.ValidateRelativePath(file.Folder, file.Name); }
            catch (InvalidDataException) { inaccessible++; continue; }

            long measured = file.SizeKnown ? Math.Max(0, file.Size) : 0;
            bool filterExcluded = byPath.TryGetValue(relative, out TransferEntry? entry) &&
                                  entry.Outcome == OutcomeReason.FilterExcluded;
            if (!filterExcluded)
            {
                eligible++;
                eligibleBytes = checked(eligibleBytes + measured);
            }

            if (entry is null)
            {
                // The scan exposed it but the manifest has no row: it is unresolved, never "copied".
                outstanding++;
                continue;
            }

            switch (entry.Outcome)
            {
                case OutcomeReason.FilterExcluded:
                    excluded++;
                    break;

                case OutcomeReason.ConflictSkipped:
                    conflictSkipped++;
                    break;

                case OutcomeReason.Copied:
                    copied++;
                    copiedBytes = checked(copiedBytes + (entry.BytesWritten > 0 ? entry.BytesWritten : measured));
                    switch (entry.Verification)
                    {
                        case VerificationLevel.SourceAndDestinationHash: sourceVerified++; break;
                        case VerificationLevel.DestinationHash: destinationVerified++; break;
                        case VerificationLevel.SizeOnly: sizeVerified++; break;
                    }
                    if (entry.State == TransferState.CopiedUnverified) unverifiable++;
                    break;

                case OutcomeReason.AlreadyPresent:
                    alreadyPresent++;
                    alreadyPresentBytes = checked(alreadyPresentBytes +
                        (entry.SizeKnown ? measured : Math.Max(0, entry.BytesWritten)));
                    if (entry.State == TransferState.CopiedUnverified) unverifiable++;
                    break;

                case OutcomeReason.Failed:
                    failed++;
                    failedBytes = checked(failedBytes + measured);
                    break;

                case OutcomeReason.Inaccessible:
                    inaccessible++;
                    break;

                default:
                    // Planned/None with a terminal state is still unresolved from the report's point of view.
                    outstanding++;
                    break;
            }
        }

        return new ReconciliationReport(
            accounting.FileCount, accounting.KnownBytes, accounting.UnknownSizeCount,
            eligible, eligibleBytes,
            excluded, conflictSkipped,
            alreadyPresent, alreadyPresentBytes,
            copied, copiedBytes,
            sourceVerified, destinationVerified, sizeVerified, unverifiable,
            inaccessible, failed, failedBytes, outstanding,
            scan.Report?.DuplicatePathCount ?? 0,
            scan.Report?.FailedFolderCount ?? 0);
    }

    /// <summary>CSV rows for the reconciliation file: one metric per line, no interpretation.</summary>
    public IEnumerable<(string Metric, string Value)> Rows()
    {
        yield return ("DiscoveredFiles", DiscoveredFiles.ToString());
        yield return ("DiscoveredKnownBytes", DiscoveredKnownBytes.ToString());
        yield return ("UnknownSizeFiles", UnknownSizeFiles.ToString());
        yield return ("EligibleFiles", EligibleFiles.ToString());
        yield return ("EligibleBytes", EligibleBytes.ToString());
        yield return ("ExcludedByFilterFiles", ExcludedFiles.ToString());
        yield return ("SkippedByConflictPolicyFiles", ConflictSkippedFiles.ToString());
        yield return ("AlreadyPresentFiles", AlreadyPresentFiles.ToString());
        yield return ("AlreadyPresentBytes", AlreadyPresentBytes.ToString());
        yield return ("CopiedFiles", CopiedFiles.ToString());
        yield return ("CopiedBytes", CopiedBytes.ToString());
        yield return ("SourceHashVerifiedFiles", SourceHashVerifiedFiles.ToString());
        yield return ("DestinationHashVerifiedFiles", DestinationHashVerifiedFiles.ToString());
        yield return ("SizeVerifiedFiles", SizeVerifiedFiles.ToString());
        yield return ("UnverifiableFiles", UnverifiableFiles.ToString());
        yield return ("InaccessibleFiles", InaccessibleFiles.ToString());
        yield return ("FailedFiles", FailedFiles.ToString());
        yield return ("FailedBytes", FailedBytes.ToString());
        yield return ("OutstandingFiles", OutstandingFiles.ToString());
        yield return ("DuplicatePathCount", DuplicatePathCount.ToString());
        yield return ("FailedFolderCount", FailedFolderCount.ToString());
    }
}
