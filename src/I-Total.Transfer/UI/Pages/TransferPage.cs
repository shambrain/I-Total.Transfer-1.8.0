using ITotalTransfer.Core;
using ITotalTransfer.UI.Controls;

namespace ITotalTransfer.UI.Pages;

/// <summary>
/// Live transfer view: progress bar, throughput, the persistent per-file queue and the log console.
/// </summary>
public partial class TransferPage : UserControl, IThemedPage
{
    public event EventHandler? OpenDestinationRequested;
    public event EventHandler? OpenLogRequested;
    public event EventHandler? ExportReportRequested;
    public event EventHandler? ClearLogRequested;

    private Theme _theme = Theme.NeonInstance;

    public TransferPage()
    {
        InitializeComponent();
        Surface.FillColumns(_queueGrid, 38, 10, 12, 14, 26);
        _theme = Theme.NeonInstance;
        _openDestinationButton.Click += (_, _) => OpenDestinationRequested?.Invoke(this, EventArgs.Empty);
        _openLogButton.Click += (_, _) => OpenLogRequested?.Invoke(this, EventArgs.Empty);
        _exportReportButton.Click += (_, _) => ExportReportRequested?.Invoke(this, EventArgs.Empty);
        _clearLogButton.Click += (_, _) => ClearLogRequested?.Invoke(this, EventArgs.Empty);
        ApplyTheme(_theme);
    }

    /// <summary>Records the active palette and repaints this page's own tinted surfaces.</summary>
    public void ApplyTheme(Theme theme)
    {
        _theme = theme;
        theme.ApplyAll(this);
    }

    public void SetPhase(string text) => _phaseLabel.Text = text;

    public void UpdateProgress(TransferProgress p)
    {
        _phaseLabel.Text = $"{p.Phase}: {p.Current:N0}/{p.Total:N0} — {p.Message}";
        _progressBar.IsIndeterminate = p.Total <= 0;
        _progressBar.Maximum = Math.Max(1, p.Total);
        _progressBar.Value = Math.Clamp(p.Current, 0, _progressBar.Maximum);
        _currentFileLabel.Text = string.IsNullOrWhiteSpace(p.CurrentFile) ? "No active file" : p.CurrentFile;
        _speedLabel.Text = "Speed: " + (p.BytesPerSecond > 0 ? FormatBytes((long)p.BytesPerSecond) + "/s" : "—");
        _etaLabel.Text = "ETA: " + (p.Eta.HasValue && p.Eta.Value < TimeSpan.FromDays(1)
            ? p.Eta.Value.ToString("hh\\:mm\\:ss") : "—");
        _countsLabel.Text = $"Copied: {p.Copied:N0}   ·   Skipped: {p.Skipped:N0}   ·   " +
                            $"Failed: {p.Failed:N0}   ·   Unverified: {p.Unverifiable:N0}" +
                            (p.IsPaused ? "   ·   PAUSED" : "");
    }

    public void SetSummary(TransferSummary summary)
    {
        _phaseLabel.Text = summary.Status;
        _phaseLabel.ForeColor = summary.Completed ? _theme.Ok : _theme.Warn;
        _countsLabel.Text = $"Copied: {summary.Copied:N0}   ·   Skipped: {summary.Skipped:N0}   ·   " +
                            $"Failed: {summary.Failed:N0}   ·   Unverified: {summary.Unverifiable:N0}   ·   " +
                            $"Elapsed: {summary.Elapsed:hh\\:mm\\:ss}   ·   Weakest verification: {summary.WeakestLevel}";
        _progressBar.IsIndeterminate = false;
        _progressBar.Value = Math.Clamp(summary.Total, 0, Math.Max(1, _progressBar.Maximum));
        _progressBar.ForeColor = summary.Completed ? _theme.Ok : _theme.Text;
        _progressBar.SnapToValue();
    }

    public void SetQueue(IReadOnlyList<TransferEntry> entries)
    {
        _queueGrid.SuspendLayout();
        _queueGrid.Rows.Clear();
        foreach (TransferEntry entry in entries)
        {
            int index = _queueGrid.Rows.Add(
                entry.RelativePath,
                FormatBytes(entry.ExpectedBytes),
                entry.State.ToString(),
                entry.Verification == VerificationLevel.None ? "—" : entry.Verification.ToString(),
                entry.Error ?? "");
            if (entry.State is TransferState.Failed or TransferState.Inaccessible
                || entry.Verification == VerificationLevel.None && entry.State is TransferState.CopiedUnverified)
                _queueGrid.Rows[index].DefaultCellStyle.ForeColor = _theme.Error;
            else if (entry.State is TransferState.IntegrityVerified)
                _queueGrid.Rows[index].DefaultCellStyle.ForeColor = _theme.Ok;
            else if (entry.State is TransferState.CopiedUnverified or TransferState.SizeVerified)
                _queueGrid.Rows[index].DefaultCellStyle.ForeColor = _theme.Warn;
        }
        _queueSummaryLabel.Text = entries.Count == 0
            ? "Persistent queue — nothing queued yet. Files appear here as soon as a transfer starts."
            : $"Persistent queue ({entries.Count:N0} item(s))";
        _queueGrid.ResumeLayout();
    }

    public void AppendLog(string line, string level)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => AppendLog(line, level))); return; }
        int start = _logBox.TextLength;
        _logBox.AppendText(line + Environment.NewLine);
        _logBox.Select(start, _logBox.TextLength - start);
        _logBox.SelectionColor = _theme.LevelColor(level);
        _logBox.SelectionLength = 0;
        _logBox.SelectionColor = _logBox.ForeColor;
        _logBox.ScrollToCaret();
    }

    public void ClearLog() => _logBox.Clear();

    public string LogText => _logBox.Text;

    private static string FormatBytes(long bytes) => bytes >= 1024L * 1024 * 1024
        ? $"{bytes / (1024d * 1024 * 1024):N2} GB"
        : bytes >= 1024L * 1024 ? $"{bytes / (1024d * 1024):N1} MB"
        : bytes >= 1024 ? $"{bytes / 1024d:N1} KB" : $"{bytes:N0} B";
}
