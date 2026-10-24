#nullable enable
using ITotalTransfer.UI.Controls;
namespace ITotalTransfer.UI.Pages;

partial class HistoryPage
{
    private System.ComponentModel.IContainer? components = null;
    private TableLayoutPanel _root = null!;

    private FlowLayoutPanel _toolbar = null!;
    private RoundedButton _refreshButton = null!;
    private RoundedButton _openDestinationButton = null!;
    private Label _databaseLabel = null!;

    private CardPanel _sessionsCard = null!;
    private Label _sessionsTitle = null!;
    private DataGridView _sessionsGrid = null!;
    private DataGridViewTextBoxColumn _sessionIdColumn = null!;
    private DataGridViewTextBoxColumn _deviceColumn = null!;
    private DataGridViewTextBoxColumn _startedColumn = null!;
    private DataGridViewTextBoxColumn _statusColumn = null!;

    private CardPanel _problemsCard = null!;
    private Label _problemsTitle = null!;
    private DataGridView _problemsGrid = null!;
    private DataGridViewTextBoxColumn _problemPathColumn = null!;
    private DataGridViewTextBoxColumn _problemBytesColumn = null!;
    private DataGridViewTextBoxColumn _problemStateColumn = null!;
    private DataGridViewTextBoxColumn _problemAttemptsColumn = null!;
    private DataGridViewTextBoxColumn _problemErrorColumn = null!;

    /// <summary>Designer-owned history page: recent sessions and problem files from the session database.</summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _root = new TableLayoutPanel();
        _toolbar = new FlowLayoutPanel();
        _refreshButton = new RoundedButton();
        _openDestinationButton = new RoundedButton();
        _databaseLabel = new Label();
        _sessionsCard = new CardPanel();
        _sessionsTitle = new Label();
        _sessionsGrid = new DataGridView();
        _sessionIdColumn = new DataGridViewTextBoxColumn();
        _deviceColumn = new DataGridViewTextBoxColumn();
        _startedColumn = new DataGridViewTextBoxColumn();
        _statusColumn = new DataGridViewTextBoxColumn();
        _problemsCard = new CardPanel();
        _problemsTitle = new Label();
        _problemsGrid = new DataGridView();
        _problemPathColumn = new DataGridViewTextBoxColumn();
        _problemBytesColumn = new DataGridViewTextBoxColumn();
        _problemStateColumn = new DataGridViewTextBoxColumn();
        _problemAttemptsColumn = new DataGridViewTextBoxColumn();
        _problemErrorColumn = new DataGridViewTextBoxColumn();
        _root.SuspendLayout();
        _toolbar.SuspendLayout();
        _sessionsCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_sessionsGrid).BeginInit();
        _problemsCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_problemsGrid).BeginInit();
        SuspendLayout();
        //
        // _root
        //
        _root.ColumnCount = 1;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.Controls.Add(_toolbar, 0, 0);
        _root.Controls.Add(_sessionsCard, 0, 1);
        _root.Controls.Add(_problemsCard, 0, 2);
        _root.Dock = DockStyle.Fill;
        _root.Name = "_root";
        _root.RowCount = 3;
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));
        _root.Size = new Size(1100, 640);
        _root.TabIndex = 0;
        //
        // _toolbar
        //
        _toolbar.Controls.Add(_refreshButton);
        _toolbar.Controls.Add(_openDestinationButton);
        _toolbar.Controls.Add(_databaseLabel);
        _toolbar.Dock = DockStyle.Fill;
        _toolbar.Name = "_toolbar";
        _toolbar.Padding = new Padding(0, 8, 0, 0);
        _toolbar.Size = new Size(1100, 46);
        _toolbar.TabIndex = 0;
        //
        // toolbar items
        //
        _refreshButton.Margin = new Padding(0, 0, 10, 0);
        _refreshButton.Name = "_refreshButton";
        _refreshButton.Size = new Size(150, 28);
        _refreshButton.TabIndex = 0;
        _refreshButton.Text = "Refresh history";
        _openDestinationButton.Margin = new Padding(0, 0, 10, 0);
        _openDestinationButton.Name = "_openDestinationButton";
        _openDestinationButton.Size = new Size(150, 28);
        _openDestinationButton.TabIndex = 1;
        _openDestinationButton.Text = "Open destination";
        _databaseLabel.Margin = new Padding(0, 8, 0, 0);
        _databaseLabel.Name = "_databaseLabel";
        _databaseLabel.Size = new Size(700, 15);
        _databaseLabel.TabIndex = 2;
        _databaseLabel.Tag = "muted";
        _databaseLabel.Text = "Session database: —";
        //
        // _sessionsCard
        //
        _sessionsCard.Controls.Add(_sessionsGrid);
        _sessionsCard.Controls.Add(_sessionsTitle);
        _sessionsCard.Dock = DockStyle.Fill;
        _sessionsCard.Margin = new Padding(0, 0, 0, 12);
        _sessionsCard.Name = "_sessionsCard";
        _sessionsCard.Size = new Size(1100, 270);
        _sessionsCard.TabIndex = 1;
        _sessionsCard.Tag = "card";
        //
        // _sessionsTitle
        //
        _sessionsTitle.Dock = DockStyle.Top;
        _sessionsTitle.Font = new Font("Segoe UI Semibold", 10F);
        _sessionsTitle.Location = new Point(0, 0);
        _sessionsTitle.Name = "_sessionsTitle";
        _sessionsTitle.Padding = new Padding(14, 10, 0, 0);
        _sessionsTitle.Size = new Size(1100, 34);
        _sessionsTitle.TabIndex = 0;
        _sessionsTitle.Text = "Recent sessions";
        //
        // _sessionsGrid
        //
        _sessionsGrid.AllowUserToAddRows = false;
        _sessionsGrid.AllowUserToDeleteRows = false;
        _sessionsGrid.AllowUserToResizeRows = false;
        _sessionsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _sessionsGrid.Columns.AddRange(_sessionIdColumn, _deviceColumn, _startedColumn, _statusColumn);
        _sessionsGrid.Dock = DockStyle.Fill;
        _sessionsGrid.Location = new Point(0, 34);
        _sessionsGrid.MultiSelect = false;
        _sessionsGrid.Name = "_sessionsGrid";
        _sessionsGrid.ReadOnly = true;
        _sessionsGrid.RowHeadersVisible = false;
        _sessionsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _sessionsGrid.Size = new Size(1100, 236);
        _sessionsGrid.TabIndex = 1;
        //
        // columns
        //
        _sessionIdColumn.HeaderText = "Session";
        _sessionIdColumn.Name = "_sessionIdColumn";
        _sessionIdColumn.ReadOnly = true;
        _sessionIdColumn.Width = 220;
        _deviceColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _deviceColumn.HeaderText = "Device";
        _deviceColumn.Name = "_deviceColumn";
        _deviceColumn.ReadOnly = true;
        _startedColumn.HeaderText = "Started (UTC)";
        _startedColumn.Name = "_startedColumn";
        _startedColumn.ReadOnly = true;
        _startedColumn.Width = 200;
        _statusColumn.HeaderText = "Status";
        _statusColumn.Name = "_statusColumn";
        _statusColumn.ReadOnly = true;
        _statusColumn.Width = 200;
        //
        // _problemsCard
        //
        _problemsCard.Controls.Add(_problemsGrid);
        _problemsCard.Controls.Add(_problemsTitle);
        _problemsCard.Dock = DockStyle.Fill;
        _problemsCard.Margin = new Padding(0);
        _problemsCard.Name = "_problemsCard";
        _problemsCard.Size = new Size(1100, 290);
        _problemsCard.TabIndex = 2;
        _problemsCard.Tag = "card";
        //
        // _problemsTitle
        //
        _problemsTitle.Dock = DockStyle.Top;
        _problemsTitle.Font = new Font("Segoe UI Semibold", 10F);
        _problemsTitle.Location = new Point(0, 0);
        _problemsTitle.Name = "_problemsTitle";
        _problemsTitle.Padding = new Padding(14, 10, 0, 0);
        _problemsTitle.Size = new Size(1100, 34);
        _problemsTitle.TabIndex = 0;
        _problemsTitle.Text = "Problem files (failed, inaccessible or unverified)";
        //
        // _problemsGrid
        //
        _problemsGrid.AllowUserToAddRows = false;
        _problemsGrid.AllowUserToDeleteRows = false;
        _problemsGrid.AllowUserToResizeRows = false;
        _problemsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _problemsGrid.Columns.AddRange(_problemPathColumn, _problemBytesColumn, _problemStateColumn, _problemAttemptsColumn, _problemErrorColumn);
        _problemsGrid.Dock = DockStyle.Fill;
        _problemsGrid.Location = new Point(0, 34);
        _problemsGrid.MultiSelect = false;
        _problemsGrid.Name = "_problemsGrid";
        _problemsGrid.ReadOnly = true;
        _problemsGrid.RowHeadersVisible = false;
        _problemsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _problemsGrid.Size = new Size(1100, 256);
        _problemsGrid.TabIndex = 1;
        //
        // columns
        //
        _problemPathColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _problemPathColumn.HeaderText = "File";
        _problemPathColumn.Name = "_problemPathColumn";
        _problemPathColumn.ReadOnly = true;
        _problemBytesColumn.HeaderText = "Expected";
        _problemBytesColumn.Name = "_problemBytesColumn";
        _problemBytesColumn.ReadOnly = true;
        _problemBytesColumn.Width = 110;
        _problemStateColumn.HeaderText = "State";
        _problemStateColumn.Name = "_problemStateColumn";
        _problemStateColumn.ReadOnly = true;
        _problemStateColumn.Width = 130;
        _problemAttemptsColumn.HeaderText = "Attempts";
        _problemAttemptsColumn.Name = "_problemAttemptsColumn";
        _problemAttemptsColumn.ReadOnly = true;
        _problemAttemptsColumn.Width = 80;
        _problemErrorColumn.HeaderText = "Note";
        _problemErrorColumn.Name = "_problemErrorColumn";
        _problemErrorColumn.ReadOnly = true;
        _problemErrorColumn.Width = 260;
        //
        // HistoryPage
        //
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Controls.Add(_root);
        Name = "HistoryPage";
        Padding = new Padding(0, 4, 0, 0);
        Size = new Size(1100, 640);
        _root.ResumeLayout(false);
        _toolbar.ResumeLayout(false);
        _toolbar.PerformLayout();
        _sessionsCard.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_sessionsGrid).EndInit();
        _problemsCard.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_problemsGrid).EndInit();
        ResumeLayout(false);
    }
}
