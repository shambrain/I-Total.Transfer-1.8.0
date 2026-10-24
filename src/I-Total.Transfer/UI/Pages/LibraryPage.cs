using ITotalTransfer.Core;
using ITotalTransfer.UI.Controls;

namespace ITotalTransfer.UI.Pages;

/// <summary>
/// Library browser: the filtered view of everything the last scan enumerated, next to the verification
/// report. The group chips are the banner's four groups, so filtering always matches the dashboard.
/// </summary>
public partial class LibraryPage : UserControl, IThemedPage
{
    public event EventHandler? ApplyFilterRequested;
    public event EventHandler? VerifyRequested;
    public event EventHandler? ExportScanRequested;

    private Theme _theme = Theme.NeonInstance;
    private bool _layingOutToolbar;
    private bool _narrowToolbar;
    private readonly List<RoundedButton> _chips = [];
    private string _kind = "All";

    // The last listing the shell pushed, kept so a live theme switch can rebuild the grid tinting.
    private IReadOnlyList<MediaFile> _files = [];
    private int _totalDiscovered;

    // Non-null while the grid is showing a cached listing rather than the result of the current run.
    private string? _provenance;

    public LibraryPage()
    {
        InitializeComponent();

        _chips.AddRange([_chipAll, _chipPhotos, _chipVideos, _chipMusic, _chipFiles]);
        foreach (RoundedButton chip in _chips)
            chip.Click += (_, _) => SelectKind(chip.Text);
        _chipAll.Active = true;

        _applyButton.Click += (_, _) => ApplyFilterRequested?.Invoke(this, EventArgs.Empty);
        _filterBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            ApplyFilterRequested?.Invoke(this, EventArgs.Empty);
        };
        _verifyButton.Click += (_, _) => VerifyRequested?.Invoke(this, EventArgs.Empty);
        _exportScanButton.Click += (_, _) => ExportScanRequested?.Invoke(this, EventArgs.Empty);

        _repairCheck.Anchor = AnchorStyles.Top | AnchorStyles.Left;         _filterBox.Anchor = AnchorStyles.Top | AnchorStyles.Left;         _toolbarCard.SizeChanged += (_, _) => LayoutToolbar();         SizeChanged += (_, _) => LayoutToolbar();         LayoutToolbar();

        ApplyTheme(_theme);
        SetFiles([], 0);
    }


    /// <summary>
    /// Keeps the library toolbar usable at the shell's minimum width. The Designer places the search
    /// box, five group chips, Apply and Repair on one 1144 px row; below that the chips and the checkbox
    /// used to overlap and Apply ran past the card. A second row is used when the width cannot hold one.
    /// </summary>
    private void LayoutToolbar()
    {
        if (_layingOutToolbar || _toolbarCard.Width <= 0) return;
        _layingOutToolbar = true;
        bool rowChanged = false;
        try
        {
            float dpi = Math.Max(1f, DeviceDpi / 96f);
            int Px(int value) => (int)Math.Round(value * dpi);
            int width = _toolbarCard.ClientSize.Width;
            bool narrow = width < Px(1010);
            if (narrow != _narrowToolbar)
            {
                _narrowToolbar = narrow;
                _root.RowStyles[0] = new RowStyle(SizeType.Absolute, narrow ? Px(108) : Px(70));
                // Set the card now as well: a TestHost or a design-time surface has no handle, so the
                // deferred row layout below cannot run there and the card would stay single-row high.
                _toolbarCard.Height = narrow ? Px(96) : Px(58);
                rowChanged = true;
            }
            _searchIcon.SetBounds(Px(16), Px(17), Px(24), Px(24));
            if (narrow)
            {
                _applyButton.SetBounds(width - Px(114), Px(14), Px(96), Px(30));
                _filterBox.SetBounds(Px(46), Px(15), Math.Max(Px(120), width - Px(172)), Px(25));
                _kindRow.SetBounds(Px(18), Px(52), Px(500), Px(38));
                _repairCheck.SetBounds(Px(536), Px(60), Px(150), Px(22));
            }
            else
            {
                _applyButton.SetBounds(Px(864), Px(14), Px(96), Px(30));
                _filterBox.SetBounds(Px(46), Px(15), Px(280), Px(25));
                _kindRow.SetBounds(Px(340), Px(10), Px(510), Px(38));
                _repairCheck.SetBounds(width - Px(172), Px(18), Px(150), Px(22));
            }
        }
        finally { _layingOutToolbar = false; }
        // The row height is applied after this layout pass: PerformLayout during a child's SizeChanged
        // is ignored by WinForms, which left the toolbar at its single-row height.
        if (!rowChanged) return;
        void ApplyRow() { if (!IsDisposed) _root.PerformLayout(); }
        if (IsHandleCreated) BeginInvoke((Action)ApplyRow); else ApplyRow();
    }

    /// <summary>Records the active palette and repaints the chip row, the empty state and the grid.</summary>
    public void ApplyTheme(Theme theme)
    {
        _theme = theme;
        theme.ApplyAll(this);

        // The chips read as selectable filters: a neutral pill normally, the accent fill once chosen.
        foreach (RoundedButton chip in _chips)
        {
            chip.Fill = theme.SurfaceAlt;
            chip.HoverFill = theme.SurfaceHover;
            chip.Stroke = theme.Border;
            chip.ForeColor = theme.Text;
            chip.ActiveFill = theme.Accent;
            chip.ActiveForeColor = theme.AccentText;
            chip.GlyphActiveColor = theme.AccentText;
            chip.Radius = 15;
            chip.Invalidate();
        }

        _verifyButton.ActiveFill = theme.Accent;
        _verifyButton.ActiveForeColor = theme.AccentText;
        _verifyButton.Invalidate();

        _emptyState.HostColor = theme.Surface;
        _emptyState.GlyphColor = theme.Muted;
        _emptyState.TitleColor = theme.Text;
        _emptyState.SubtitleColor = theme.Muted;
        _emptyState.Invalidate();

        Rebuild();
    }

    /// <summary>The free-text filter typed into the search box.</summary>
    public string FilterText => _filterBox.Text.Trim();

    /// <summary>The selected group: "All", or one of the banner's four group names.</summary>
    public string KindFilter => _kind;

    /// <summary>Whether the operator asked verification to re-copy files that are missing or wrong.</summary>
    public bool RepairRequested => _repairCheck.Checked;

    /// <summary>Replaces the one-line summary above the grid.</summary>
    public void SetSummary(string text) => _summaryLabel.Text = text;

    /// <summary>
    /// States where the listed files came from, or clears that statement when the listing is a live scan.
    /// A cached listing must never be presented as a fresh measurement, so this line is rendered ahead of
    /// the counts and tinted with the theme's warning colour: the operator can tell at a glance that the
    /// grid is history while the live scan is still running.
    /// </summary>
    public void SetProvenance(string? text)
    {
        _provenance = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        Rebuild();
    }

    /// <summary>
    /// Lists the files that survived the filter. <paramref name="totalDiscovered"/> is the unfiltered
    /// count, used to say honestly how much is being hidden by the filter.
    /// </summary>
    public void SetFiles(IReadOnlyList<MediaFile> files, int totalDiscovered)
    {
        _files = files;
        _totalDiscovered = totalDiscovered;
        Rebuild();
    }

    private void Rebuild()
    {
        _grid.SuspendLayout();
        _grid.Rows.Clear();
        foreach (MediaFile file in _files)
        {
            _grid.Rows.Add(
                file.Folder,
                file.Name,
                MediaSummary.FormatBytes(file.Size),
                file.ModifiedUtc?.ToString("yyyy-MM-dd HH:mm") ?? "—",
                file.Category.Title());
        }
        _grid.ResumeLayout();

        bool empty = _files.Count == 0;
        _grid.Visible = !empty;
        _emptyState.Visible = empty;
        // While the empty state is up it carries the message, so the summary caption is hidden rather
        // than repeating it two lines above. The exception is a cached listing: the provenance line is
        // the difference between "nothing here" and "here is what was there last time, and the filter
        // excludes all of it", so it stays on screen.
        _summaryLabel.Visible = !empty || _provenance is not null;
        if (empty) _emptyState.BringToFront();

        if (empty)
        {
            _summaryLabel.Text = _totalDiscovered == 0
                ? "Nothing enumerated yet."
                : $"0 of {_totalDiscovered:N0} enumerated file(s) match the filter.";
            _emptyState.SetState(
                _totalDiscovered == 0 ? Glyphs.Photo : Glyphs.Search,
                _totalDiscovered == 0 ? "No media listed yet" : "No matches",
                _totalDiscovered == 0
                    ? "Choose Scan device on the Overview page to list the phone's media. If no phone is attached, connect and unlock it first."
                    : "No media matches the current filter. Choose All and clear the search box to see everything.");
        }
        else
        {
            long bytes = _files.Sum(file => Math.Max(0, file.Size));
            _summaryLabel.Text = _files.Count == _totalDiscovered
                ? $"{_files.Count:N0} file(s) · {MediaSummary.FormatBytes(bytes)}"
                : $"{_files.Count:N0} of {_totalDiscovered:N0} file(s) shown · {MediaSummary.FormatBytes(bytes)} (filter active)";
        }

        // A cached listing leads with where it came from, and says so in a different colour, so it can
        // never be read as the result of the run in progress.
        if (_provenance is not null)
        {
            _summaryLabel.Text = _provenance + " · " + _summaryLabel.Text;
            _summaryLabel.ForeColor = _theme.Info;
        }
        else
        {
            _summaryLabel.ForeColor = _theme.Muted;
        }
    }

    private void SelectKind(string kind)
    {
        _kind = kind;
        foreach (RoundedButton chip in _chips)
        {
            chip.Active = string.Equals(chip.Text, kind, StringComparison.OrdinalIgnoreCase);
            chip.Invalidate();
        }
        ApplyFilterRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Renders the destination-verification outcome, including every issue it found.</summary>
    public void SetVerification(VerificationResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(result.Completed ? "RESULT: verification passed" : "RESULT: verification found issues");
        sb.AppendLine($"Total scanned      : {result.Total:N0}");
        sb.AppendLine($"Size/byte matches  : {result.Matching:N0}");
        sb.AppendLine($"Source+dest hashed : {result.SourceHashVerified:N0}");
        sb.AppendLine($"Destination hashed : {result.DestinationHashVerified:N0}");
        sb.AppendLine($"Missing            : {result.Missing:N0}");
        sb.AppendLine($"Size mismatches    : {result.WrongSize:N0}");
        sb.AppendLine($"Unverifiable       : {result.Unverifiable:N0}");
        sb.AppendLine($"Collisions         : {result.Collisions:N0}");
        sb.AppendLine($"Repaired           : {result.Repaired:N0}");
        if (!string.IsNullOrWhiteSpace(result.ReportPath)) sb.AppendLine($"Report             : {result.ReportPath}");
        sb.AppendLine();
        sb.AppendLine("Issues:");
        foreach (string issue in result.Issues.Take(500)) sb.AppendLine("  • " + issue);
        if (result.Issues.Count > 500) sb.AppendLine($"  … and {result.Issues.Count - 500:N0} more.");
        if (result.Issues.Count == 0) sb.AppendLine("  (none)");
        _resultBox.Text = sb.ToString();
    }

    /// <summary>Replaces the report pane with arbitrary text (used for the enumeration report).</summary>
    public void SetReportText(string text) => _resultBox.Text = text;

    /// <summary>Appends one line to the report pane, marshalling to the UI thread when needed.</summary>
    public void AppendResult(string line)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => AppendResult(line))); return; }
        _resultBox.AppendText(line + Environment.NewLine);
    }
}
