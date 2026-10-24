using ITotalTransfer.Services;
using ITotalTransfer.UI.Controls;

namespace ITotalTransfer.UI.Pages;

/// <summary>Diagnostics page: connection checks, native dependency provenance and bundle export.</summary>
public partial class DiagnosticsPage : UserControl, IThemedPage
{
    public event EventHandler? RunRequested;
    public event EventHandler? ExportBundleRequested;
    public event EventHandler? OpenAppleGuideRequested;
    public event EventHandler? OpenAppleDevicesRequested;

    private Theme _theme = Theme.NeonInstance;

    public DiagnosticsPage()
    {
        InitializeComponent();
        Surface.FillColumns(_diagnosticsGrid, 28, 14, 58);
        Surface.FillColumns(_dependenciesGrid, 22, 16, 14, 48);
        _theme = Theme.NeonInstance;
        _runButton.Click += (_, _) => RunRequested?.Invoke(this, EventArgs.Empty);
        _exportButton.Click += (_, _) => ExportBundleRequested?.Invoke(this, EventArgs.Empty);
        _supportButton.Click += (_, _) => OpenAppleGuideRequested?.Invoke(this, EventArgs.Empty);
        _appleButton.Click += (_, _) => OpenAppleDevicesRequested?.Invoke(this, EventArgs.Empty);
        ApplyTheme(_theme);
    }

    /// <summary>Records the active palette and applies it to this page's control tree.</summary>
    public void ApplyTheme(Theme theme)
    {
        _theme = theme;
        theme.ApplyAll(this);
    }

    public void SetSummary(string text) => _summaryLabel.Text = text;

    public void SetDiagnostics(IReadOnlyList<DiagnosticItem> items)
    {
        _diagnosticsGrid.SuspendLayout();
        _diagnosticsGrid.Rows.Clear();
        foreach (DiagnosticItem item in items)
        {
            int index = _diagnosticsGrid.Rows.Add(item.Name, item.Status, item.Details);
            _diagnosticsGrid.Rows[index].DefaultCellStyle.ForeColor = LevelColor(item.Status);
        }
        _diagnosticsGrid.ResumeLayout();
        _diagnosticsTitle.Text = $"Connection diagnostics ({items.Count:N0} check(s))";
    }

    /// <summary>
    /// Green means "checked and fine", red means "this is why it fails", amber means "worth your
    /// attention". The status text is a sentence fragment rather than an enum, so the two dynamic
    /// readings - a cached USB record and "nothing seen at all" - are matched by prefix.
    /// </summary>
    private Color LevelColor(string? status)
    {
        string value = (status ?? string.Empty).Trim();
        if (value.StartsWith("CACHED", StringComparison.OrdinalIgnoreCase)) return _theme.Warn;
        if (value.Equals("NONE", StringComparison.OrdinalIgnoreCase)) return _theme.Error;
        return value switch
        {
            "PASS" or "FOUND" or "DETECTED" or "ACCESSIBLE" or "ATTACHED" or "VERIFIED" => _theme.Ok,
            // The family, USB-mode and unlock rows: green is the good reading for each of them, and
            // "NOT IDENTIFIED" / "CHECK PHONE" fall through to amber, which is what they mean.
            "APPLE" or "ANDROID" or "FILE TRANSFER" or "UNLOCKED" => _theme.Ok,
            "NOT FOUND IN COMMON PATHS" or "NOT DETECTED" => _theme.Error,
            _ => _theme.Warn
        };
    }

    public void SetDependencies(DependencyReport report)
    {
        _dependenciesGrid.SuspendLayout();
        _dependenciesGrid.Rows.Clear();
        foreach (DependencyFileStatus file in report.Files)
        {
            int index = _dependenciesGrid.Rows.Add(
                file.Dependency.FileName,
                $"{file.Dependency.Version} / {file.Dependency.Architecture}",
                file.State.ToString(),
                file.Detail);
            _dependenciesGrid.Rows[index].DefaultCellStyle.ForeColor = file.State switch
            {
                DependencyFileState.Verified => _theme.Ok,
                DependencyFileState.Missing => _theme.Muted,
                _ => _theme.Error
            };
        }
        _dependenciesGrid.ResumeLayout();
        string verdict = !report.AnyVerified
            ? "no native runtime verified — native backends stay disabled"
            : report.RequiredDependenciesSatisfied
                ? "required dependencies verified"
                : "some dependencies verified";
        _dependenciesTitle.Text = $"Native dependencies — {verdict} (fail-closed)";
    }
}
