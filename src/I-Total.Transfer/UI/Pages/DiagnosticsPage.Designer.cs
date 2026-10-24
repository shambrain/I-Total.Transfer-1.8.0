#nullable enable
using ITotalTransfer.UI.Controls;
namespace ITotalTransfer.UI.Pages;

partial class DiagnosticsPage
{
    private System.ComponentModel.IContainer? components = null;
    private TableLayoutPanel _root = null!;

    private FlowLayoutPanel _toolbar = null!;
    private RoundedButton _runButton = null!;
    private RoundedButton _exportButton = null!;
    private RoundedButton _supportButton = null!;
    private RoundedButton _appleButton = null!;

    private Label _summaryLabel = null!;

    private CardPanel _diagnosticsCard = null!;
    private Label _diagnosticsTitle = null!;
    private DataGridView _diagnosticsGrid = null!;
    private DataGridViewTextBoxColumn _checkNameColumn = null!;
    private DataGridViewTextBoxColumn _checkStatusColumn = null!;
    private DataGridViewTextBoxColumn _checkDetailColumn = null!;

    private CardPanel _dependenciesCard = null!;
    private Label _dependenciesTitle = null!;
    private DataGridView _dependenciesGrid = null!;
    private DataGridViewTextBoxColumn _depFileColumn = null!;
    private DataGridViewTextBoxColumn _depVersionColumn = null!;
    private DataGridViewTextBoxColumn _depStateColumn = null!;
    private DataGridViewTextBoxColumn _depDetailColumn = null!;

    /// <summary>Designer-owned diagnostics page: connection checks and native dependency provenance.</summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _root = new TableLayoutPanel();
        _toolbar = new FlowLayoutPanel();
        _runButton = new RoundedButton();
        _exportButton = new RoundedButton();
        _supportButton = new RoundedButton();
        _appleButton = new RoundedButton();
        _summaryLabel = new Label();
        _diagnosticsCard = new CardPanel();
        _diagnosticsTitle = new Label();
        _diagnosticsGrid = new DataGridView();
        _checkNameColumn = new DataGridViewTextBoxColumn();
        _checkStatusColumn = new DataGridViewTextBoxColumn();
        _checkDetailColumn = new DataGridViewTextBoxColumn();
        _dependenciesCard = new CardPanel();
        _dependenciesTitle = new Label();
        _dependenciesGrid = new DataGridView();
        _depFileColumn = new DataGridViewTextBoxColumn();
        _depVersionColumn = new DataGridViewTextBoxColumn();
        _depStateColumn = new DataGridViewTextBoxColumn();
        _depDetailColumn = new DataGridViewTextBoxColumn();
        _root.SuspendLayout();
        _toolbar.SuspendLayout();
        _diagnosticsCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_diagnosticsGrid).BeginInit();
        _dependenciesCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_dependenciesGrid).BeginInit();
        SuspendLayout();
        //
        // _root
        //
        _root.ColumnCount = 1;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.Controls.Add(_toolbar, 0, 0);
        _root.Controls.Add(_summaryLabel, 0, 1);
        _root.Controls.Add(_diagnosticsCard, 0, 2);
        _root.Controls.Add(_dependenciesCard, 0, 3);
        _root.Dock = DockStyle.Fill;
        _root.Name = "_root";
        _root.RowCount = 4;
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));
        _root.Size = new Size(1100, 640);
        _root.TabIndex = 0;
        //
        // _toolbar
        //
        _toolbar.Controls.Add(_runButton);
        _toolbar.Controls.Add(_exportButton);
        _toolbar.Controls.Add(_supportButton);
        _toolbar.Controls.Add(_appleButton);
        _toolbar.Dock = DockStyle.Fill;
        _toolbar.Name = "_toolbar";
        _toolbar.Padding = new Padding(0, 8, 0, 0);
        _toolbar.Size = new Size(1100, 46);
        _toolbar.TabIndex = 0;
        //
        // toolbar items
        //
        _runButton.Margin = new Padding(0, 0, 10, 0);
        _runButton.Name = "_runButton";
        _runButton.Size = new Size(170, 28);
        _runButton.TabIndex = 0;
        _runButton.Tag = "primary";
        _runButton.Text = "Run diagnostics";
        _exportButton.Margin = new Padding(0, 0, 10, 0);
        _exportButton.Name = "_exportButton";
        _exportButton.Size = new Size(170, 28);
        _exportButton.TabIndex = 1;
        _exportButton.Text = "Export diagnostics ZIP";
        _supportButton.Margin = new Padding(0, 0, 10, 0);
        _supportButton.Name = "_supportButton";
        _supportButton.Size = new Size(190, 28);
        _supportButton.TabIndex = 2;
        _supportButton.Text = "Apple connection guide";
        _appleButton.Margin = new Padding(0, 0, 0, 0);
        _appleButton.Name = "_appleButton";
        _appleButton.Size = new Size(210, 28);
        _appleButton.TabIndex = 3;
        _appleButton.Text = "Official Apple Devices page";
        //
        // _summaryLabel
        //
        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.Name = "_summaryLabel";
        _summaryLabel.Padding = new Padding(2, 6, 0, 0);
        _summaryLabel.Size = new Size(1100, 30);
        _summaryLabel.TabIndex = 1;
        _summaryLabel.Tag = "muted";
        _summaryLabel.Text = "Run the checks to describe the current USB, driver and trust state.";
        //
        // _diagnosticsCard
        //
        _diagnosticsCard.Controls.Add(_diagnosticsGrid);
        _diagnosticsCard.Controls.Add(_diagnosticsTitle);
        _diagnosticsCard.Dock = DockStyle.Fill;
        _diagnosticsCard.Margin = new Padding(0, 0, 0, 12);
        _diagnosticsCard.Name = "_diagnosticsCard";
        _diagnosticsCard.Size = new Size(1100, 270);
        _diagnosticsCard.TabIndex = 2;
        _diagnosticsCard.Tag = "card";
        //
        // _diagnosticsTitle
        //
        _diagnosticsTitle.Dock = DockStyle.Top;
        _diagnosticsTitle.Font = new Font("Segoe UI Semibold", 10F);
        _diagnosticsTitle.Location = new Point(0, 0);
        _diagnosticsTitle.Name = "_diagnosticsTitle";
        _diagnosticsTitle.Padding = new Padding(14, 10, 0, 0);
        _diagnosticsTitle.Size = new Size(1100, 34);
        _diagnosticsTitle.TabIndex = 0;
        _diagnosticsTitle.Text = "Connection diagnostics";
        //
        // _diagnosticsGrid
        //
        _diagnosticsGrid.AllowUserToAddRows = false;
        _diagnosticsGrid.AllowUserToDeleteRows = false;
        _diagnosticsGrid.AllowUserToResizeRows = false;
        // A diagnostics page that silently truncates its own findings is worse than no page, and the
        // details here are full sentences. Wrap them and let the row grow; the grid scrolls instead.
        _diagnosticsGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _diagnosticsGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _diagnosticsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _diagnosticsGrid.Columns.AddRange(_checkNameColumn, _checkStatusColumn, _checkDetailColumn);
        _diagnosticsGrid.Dock = DockStyle.Fill;
        _diagnosticsGrid.Location = new Point(0, 34);
        _diagnosticsGrid.MultiSelect = false;
        _diagnosticsGrid.Name = "_diagnosticsGrid";
        _diagnosticsGrid.ReadOnly = true;
        _diagnosticsGrid.RowHeadersVisible = false;
        _diagnosticsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _diagnosticsGrid.Size = new Size(1100, 236);
        _diagnosticsGrid.TabIndex = 1;
        //
        // columns
        //
        _checkNameColumn.HeaderText = "Check";
        _checkNameColumn.Name = "_checkNameColumn";
        _checkNameColumn.ReadOnly = true;
        _checkNameColumn.Width = 230;
        _checkStatusColumn.HeaderText = "Status";
        _checkStatusColumn.Name = "_checkStatusColumn";
        _checkStatusColumn.ReadOnly = true;
        _checkStatusColumn.Width = 190;
        _checkDetailColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _checkDetailColumn.HeaderText = "Detail";
        _checkDetailColumn.Name = "_checkDetailColumn";
        _checkDetailColumn.ReadOnly = true;
        //
        // _dependenciesCard
        //
        _dependenciesCard.Controls.Add(_dependenciesGrid);
        _dependenciesCard.Controls.Add(_dependenciesTitle);
        _dependenciesCard.Dock = DockStyle.Fill;
        _dependenciesCard.Margin = new Padding(0);
        _dependenciesCard.Name = "_dependenciesCard";
        _dependenciesCard.Size = new Size(1100, 290);
        _dependenciesCard.TabIndex = 3;
        _dependenciesCard.Tag = "card";
        //
        // _dependenciesTitle
        //
        _dependenciesTitle.Dock = DockStyle.Top;
        _dependenciesTitle.Font = new Font("Segoe UI Semibold", 10F);
        _dependenciesTitle.Location = new Point(0, 0);
        _dependenciesTitle.Name = "_dependenciesTitle";
        _dependenciesTitle.Padding = new Padding(14, 10, 0, 0);
        _dependenciesTitle.Size = new Size(1100, 34);
        _dependenciesTitle.TabIndex = 0;
        _dependenciesTitle.Text = "Native dependencies (pinned SHA-256, fail-closed)";
        //
        // _dependenciesGrid
        //
        _dependenciesGrid.AllowUserToAddRows = false;
        _dependenciesGrid.AllowUserToDeleteRows = false;
        _dependenciesGrid.AllowUserToResizeRows = false;
        _dependenciesGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _dependenciesGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _dependenciesGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _dependenciesGrid.Columns.AddRange(_depFileColumn, _depVersionColumn, _depStateColumn, _depDetailColumn);
        _dependenciesGrid.Dock = DockStyle.Fill;
        _dependenciesGrid.Location = new Point(0, 34);
        _dependenciesGrid.MultiSelect = false;
        _dependenciesGrid.Name = "_dependenciesGrid";
        _dependenciesGrid.ReadOnly = true;
        _dependenciesGrid.RowHeadersVisible = false;
        _dependenciesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _dependenciesGrid.Size = new Size(1100, 256);
        _dependenciesGrid.TabIndex = 1;
        //
        // columns
        //
        _depFileColumn.HeaderText = "Binary";
        _depFileColumn.Name = "_depFileColumn";
        _depFileColumn.ReadOnly = true;
        _depFileColumn.Width = 240;
        _depVersionColumn.HeaderText = "Version / arch";
        _depVersionColumn.Name = "_depVersionColumn";
        _depVersionColumn.ReadOnly = true;
        _depVersionColumn.Width = 180;
        _depStateColumn.HeaderText = "State";
        _depStateColumn.Name = "_depStateColumn";
        _depStateColumn.ReadOnly = true;
        _depStateColumn.Width = 140;
        _depDetailColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _depDetailColumn.HeaderText = "Detail";
        _depDetailColumn.Name = "_depDetailColumn";
        _depDetailColumn.ReadOnly = true;
        //
        // DiagnosticsPage
        //
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Controls.Add(_root);
        Name = "DiagnosticsPage";
        Padding = new Padding(0, 4, 0, 0);
        Size = new Size(1100, 640);
        _root.ResumeLayout(false);
        _toolbar.ResumeLayout(false);
        _toolbar.PerformLayout();
        _diagnosticsCard.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_diagnosticsGrid).EndInit();
        _dependenciesCard.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_dependenciesGrid).EndInit();
        ResumeLayout(false);
    }
}
