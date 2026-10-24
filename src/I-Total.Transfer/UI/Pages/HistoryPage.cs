using ITotalTransfer.Services;
using ITotalTransfer.UI.Controls;

namespace ITotalTransfer.UI.Pages;

/// <summary>History page: recent transfer sessions and the persistent problem-file list.</summary>
public partial class HistoryPage : UserControl, IThemedPage
{
    public event EventHandler? RefreshRequested;
    public event EventHandler? OpenDestinationRequested;

    public HistoryPage()
    {
        InitializeComponent();
        // Fill mode keeps every header readable at any window width; the weights keep paths dominant.
        Surface.FillColumns(_sessionsGrid, 26, 30, 24, 20);
        Surface.FillColumns(_problemsGrid, 34, 13, 13, 13, 27);
        // The database path needs a second row when the window is narrow; grow the toolbar instead of
        // letting the label wrap behind the sessions card where it cannot be read.
        _toolbar.AutoSize = true;
        _toolbar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _root.RowStyles[0] = new RowStyle(SizeType.AutoSize);
        _refreshButton.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        _openDestinationButton.Click += (_, _) => OpenDestinationRequested?.Invoke(this, EventArgs.Empty);
        Theme.DarkInstance.ApplyAll(this);
    }

    public void ApplyTheme(Theme theme) => theme.ApplyAll(this);

    public void SetDatabasePath(string path) => _databaseLabel.Text = "Session database: " + path;

    public void SetSessions(IReadOnlyList<SessionHistoryRow> rows)
    {
        _sessionsGrid.SuspendLayout();
        _sessionsGrid.Rows.Clear();
        foreach (SessionHistoryRow row in rows)
            _sessionsGrid.Rows.Add(Shorten(row.SessionId), row.DeviceName, row.StartedUtc, row.Status);
        _sessionsGrid.ResumeLayout();
        _sessionsTitle.Text = $"Recent sessions ({rows.Count:N0})";
    }

    public void SetProblems(IReadOnlyList<FileHistoryRow> rows)
    {
        _problemsGrid.SuspendLayout();
        _problemsGrid.Rows.Clear();
        foreach (FileHistoryRow row in rows)
            _problemsGrid.Rows.Add(row.RelativePath, row.ExpectedBytes.ToString("N0"), row.State, row.Attempts.ToString(), row.Error ?? "");
        _problemsGrid.ResumeLayout();
        _problemsTitle.Text = $"Problem files ({rows.Count:N0}) — failed, inaccessible or unverified";
    }

    private static string Shorten(string sessionId) => sessionId.Length > 16 ? sessionId[..16] + "…" : sessionId;
}
