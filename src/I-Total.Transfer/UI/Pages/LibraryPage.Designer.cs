#nullable enable
using ITotalTransfer.UI.Controls;

namespace ITotalTransfer.UI.Pages;

partial class LibraryPage
{
    private System.ComponentModel.IContainer? components = null;
    private TableLayoutPanel _root = null!;

    private CardPanel _toolbarCard = null!;
    private Label _searchIcon = null!;
    private TextBox _filterBox = null!;
    private FlowLayoutPanel _kindRow = null!;
    private RoundedButton _chipAll = null!;
    private RoundedButton _chipPhotos = null!;
    private RoundedButton _chipVideos = null!;
    private RoundedButton _chipMusic = null!;
    private RoundedButton _chipFiles = null!;
    private RoundedButton _applyButton = null!;
    private FluentCheckBox _repairCheck = null!;

    private TableLayoutPanel _split = null!;
    private CardPanel _gridCard = null!;
    private Label _summaryLabel = null!;
    private DataGridView _grid = null!;
    private EmptyState _emptyState = null!;
    private CardPanel _reportCard = null!;
    private Label _reportTitle = null!;
    private RoundedButton _verifyButton = null!;
    private RoundedButton _exportScanButton = null!;
    private RichTextBox _resultBox = null!;

    /// <summary>Designer-owned layout: filter toolbar, the media grid and the verification report.</summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _root = new TableLayoutPanel();
        _toolbarCard = new CardPanel();
        _searchIcon = new Label();
        _filterBox = new TextBox();
        _kindRow = new FlowLayoutPanel();
        _chipAll = new RoundedButton();
        _chipPhotos = new RoundedButton();
        _chipVideos = new RoundedButton();
        _chipMusic = new RoundedButton();
        _chipFiles = new RoundedButton();
        _applyButton = new RoundedButton();
        _repairCheck = new FluentCheckBox();
        _split = new TableLayoutPanel();
        _gridCard = new CardPanel();
        _summaryLabel = new Label();
        _grid = new DataGridView();
        _emptyState = new EmptyState();
        _reportCard = new CardPanel();
        _reportTitle = new Label();
        _verifyButton = new RoundedButton();
        _exportScanButton = new RoundedButton();
        _resultBox = new RichTextBox();
        _root.SuspendLayout();
        _toolbarCard.SuspendLayout();
        _kindRow.SuspendLayout();
        _split.SuspendLayout();
        _gridCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_grid).BeginInit();
        _reportCard.SuspendLayout();
        SuspendLayout();
        //
        // _root
        //
        _root.ColumnCount = 1;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.Controls.Add(_toolbarCard, 0, 0);
        _root.Controls.Add(_split, 0, 1);
        _root.Dock = DockStyle.Fill;
        _root.Margin = new Padding(0);
        _root.Name = "_root";
        _root.RowCount = 2;
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _root.Size = new Size(1144, 692);
        _root.TabIndex = 0;
        //
        // _toolbarCard — one place to narrow the library: search, group, repair.
        //
        _toolbarCard.Controls.Add(_searchIcon);
        _toolbarCard.Controls.Add(_filterBox);
        _toolbarCard.Controls.Add(_kindRow);
        _toolbarCard.Controls.Add(_applyButton);
        _toolbarCard.Controls.Add(_repairCheck);
        _toolbarCard.Dock = DockStyle.Fill;
        _toolbarCard.Margin = new Padding(0, 0, 0, 12);
        _toolbarCard.Name = "_toolbarCard";
        _toolbarCard.Size = new Size(1144, 58);
        _toolbarCard.StrokeWidth = 1;
        _toolbarCard.TabIndex = 0;
        _toolbarCard.Tag = "card";
        //
        // _searchIcon
        //
        _searchIcon.BackColor = Color.Transparent;
        _searchIcon.Font = new Font("Segoe UI Symbol", 11F);
        _searchIcon.Location = new Point(16, 17);
        _searchIcon.Margin = new Padding(0);
        _searchIcon.Name = "_searchIcon";
        _searchIcon.Size = new Size(24, 24);
        _searchIcon.TabIndex = 0;
        _searchIcon.Tag = "glyph muted";
        _searchIcon.Text = Glyphs.Search;
        _searchIcon.TextAlign = ContentAlignment.MiddleCenter;
        //
        // _filterBox
        //
        _filterBox.Location = new Point(46, 15);
        _filterBox.Margin = new Padding(0);
        _filterBox.Name = "_filterBox";
        _filterBox.PlaceholderText = "Filter by name or folder…";
        _filterBox.Size = new Size(280, 25);
        _filterBox.TabIndex = 1;
        //
        // _kindRow — the banner's four groups plus All, as selectable chips.
        //
        _kindRow.Controls.Add(_chipAll);
        _kindRow.Controls.Add(_chipPhotos);
        _kindRow.Controls.Add(_chipVideos);
        _kindRow.Controls.Add(_chipMusic);
        _kindRow.Controls.Add(_chipFiles);
        _kindRow.Location = new Point(340, 10);
        _kindRow.Margin = new Padding(0);
        _kindRow.Name = "_kindRow";
        _kindRow.Padding = new Padding(0, 4, 0, 0);
        _kindRow.Size = new Size(510, 38);
        _kindRow.TabIndex = 2;
        _kindRow.WrapContents = false;
        //
        // group chips
        //
        _chipAll.Location = new Point(0, 4);
        _chipAll.Margin = new Padding(0, 0, 6, 0);
        _chipAll.Name = "_chipAll";
        _chipAll.Size = new Size(94, 30);
        _chipAll.TabIndex = 0;
        _chipAll.Tag = "chip";
        _chipAll.Text = "All";
        //
        _chipPhotos.Location = new Point(100, 4);
        _chipPhotos.Margin = new Padding(0, 0, 6, 0);
        _chipPhotos.Name = "_chipPhotos";
        _chipPhotos.Size = new Size(94, 30);
        _chipPhotos.TabIndex = 1;
        _chipPhotos.Tag = "chip";
        _chipPhotos.Text = "Photos";
        //
        _chipVideos.Location = new Point(200, 4);
        _chipVideos.Margin = new Padding(0, 0, 6, 0);
        _chipVideos.Name = "_chipVideos";
        _chipVideos.Size = new Size(94, 30);
        _chipVideos.TabIndex = 2;
        _chipVideos.Tag = "chip";
        _chipVideos.Text = "Videos";
        //
        _chipMusic.Location = new Point(300, 4);
        _chipMusic.Margin = new Padding(0, 0, 6, 0);
        _chipMusic.Name = "_chipMusic";
        _chipMusic.Size = new Size(94, 30);
        _chipMusic.TabIndex = 3;
        _chipMusic.Tag = "chip";
        _chipMusic.Text = "Music";
        //
        _chipFiles.Location = new Point(400, 4);
        _chipFiles.Margin = new Padding(0);
        _chipFiles.Name = "_chipFiles";
        _chipFiles.Size = new Size(94, 30);
        _chipFiles.TabIndex = 4;
        _chipFiles.Tag = "chip";
        _chipFiles.Text = "Files";
        //
        // _applyButton
        //
        _applyButton.Glyph = Glyphs.Search;
        _applyButton.Location = new Point(864, 14);
        _applyButton.Margin = new Padding(0);
        _applyButton.Name = "_applyButton";
        _applyButton.Size = new Size(96, 30);
        _applyButton.TabIndex = 3;
        _applyButton.Tag = "subtle";
        _applyButton.Text = "Apply";
        //
        // _repairCheck
        //
        _repairCheck.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _repairCheck.Location = new Point(972, 18);
        _repairCheck.Margin = new Padding(0);
        _repairCheck.Name = "_repairCheck";
        _repairCheck.Size = new Size(150, 22);
        _repairCheck.TabIndex = 4;
        _repairCheck.Text = "Repair missing";
        //
        // _split — the media grid on the left, the verification report on the right.
        //
        _split.ColumnCount = 2;
        _split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63F));
        _split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37F));
        _split.Controls.Add(_gridCard, 0, 0);
        _split.Controls.Add(_reportCard, 1, 0);
        _split.Dock = DockStyle.Fill;
        _split.Margin = new Padding(0);
        _split.Name = "_split";
        _split.RowCount = 1;
        _split.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _split.Size = new Size(1144, 622);
        _split.TabIndex = 1;
        //
        // _gridCard
        //
        _gridCard.Controls.Add(_summaryLabel);
        _gridCard.Controls.Add(_grid);
        _gridCard.Controls.Add(_emptyState);
        _gridCard.Dock = DockStyle.Fill;
        _gridCard.Margin = new Padding(0, 0, 12, 0);
        _gridCard.Name = "_gridCard";
        _gridCard.Size = new Size(708, 622);
        _gridCard.StrokeWidth = 1;
        _gridCard.TabIndex = 0;
        _gridCard.Tag = "card";
        //
        // _summaryLabel
        //
        _summaryLabel.AutoSize = true;
        _summaryLabel.Font = new Font("Segoe UI", 9F);
        _summaryLabel.Location = new Point(18, 14);
        _summaryLabel.Margin = new Padding(0);
        _summaryLabel.Name = "_summaryLabel";
        _summaryLabel.Size = new Size(200, 15);
        _summaryLabel.TabIndex = 0;
        _summaryLabel.Tag = "muted";
        _summaryLabel.Text = "No scan yet.";
        //
        // _grid
        //
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.ColumnHeadersHeight = 40;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Folder", Width = 190, AutoSizeMode = DataGridViewAutoSizeColumnMode.None },
            new DataGridViewTextBoxColumn { HeaderText = "File", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 160 },
            new DataGridViewTextBoxColumn { HeaderText = "Size", Width = 90, AutoSizeMode = DataGridViewAutoSizeColumnMode.None },
            new DataGridViewTextBoxColumn { HeaderText = "Modified", Width = 140, AutoSizeMode = DataGridViewAutoSizeColumnMode.None },
            new DataGridViewTextBoxColumn { HeaderText = "Group", Width = 90, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
        _grid.Location = new Point(12, 40);
        _grid.Margin = new Padding(0);
        _grid.MultiSelect = false;
        _grid.Name = "_grid";
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.RowTemplate.Height = 30;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.Size = new Size(684, 566);
        _grid.TabIndex = 1;
        //
        // _emptyState — shown in place of the grid while there is nothing to list.
        //
        _emptyState.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _emptyState.Location = new Point(12, 40);
        _emptyState.Margin = new Padding(0);
        _emptyState.Name = "_emptyState";
        _emptyState.Size = new Size(684, 566);
        _emptyState.TabIndex = 2;
        //
        // _reportCard
        //
        _reportCard.Controls.Add(_reportTitle);
        _reportCard.Controls.Add(_verifyButton);
        _reportCard.Controls.Add(_exportScanButton);
        _reportCard.Controls.Add(_resultBox);
        _reportCard.Dock = DockStyle.Fill;
        _reportCard.Margin = new Padding(0);
        _reportCard.Name = "_reportCard";
        _reportCard.Size = new Size(424, 622);
        _reportCard.StrokeWidth = 1;
        _reportCard.TabIndex = 1;
        _reportCard.Tag = "card";
        //
        // _reportTitle
        //
        _reportTitle.AutoSize = true;
        _reportTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _reportTitle.Location = new Point(16, 14);
        _reportTitle.Margin = new Padding(0);
        _reportTitle.Name = "_reportTitle";
        _reportTitle.Size = new Size(200, 17);
        _reportTitle.TabIndex = 0;
        _reportTitle.Text = "Verification report";
        //
        // _verifyButton
        //
        _verifyButton.Location = new Point(16, 42);
        _verifyButton.Margin = new Padding(0);
        _verifyButton.Name = "_verifyButton";
        _verifyButton.Size = new Size(210, 32);
        _verifyButton.TabIndex = 1;
        _verifyButton.Tag = "chip";
        _verifyButton.Text = "Verify destination";
        //
        // _exportScanButton
        //
        _exportScanButton.Location = new Point(234, 42);
        _exportScanButton.Margin = new Padding(0);
        _exportScanButton.Name = "_exportScanButton";
        _exportScanButton.Size = new Size(120, 32);
        _exportScanButton.TabIndex = 2;
        _exportScanButton.Tag = "subtle";
        _exportScanButton.Text = "Export scan";
        //
        // _resultBox
        //
        _resultBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _resultBox.BackColor = Color.FromArgb(11, 11, 13);
        _resultBox.BorderStyle = BorderStyle.None;
        _resultBox.Font = new Font("Consolas", 8.5F);
        _resultBox.Location = new Point(14, 86);
        _resultBox.Margin = new Padding(0);
        _resultBox.Name = "_resultBox";
        _resultBox.ReadOnly = true;
        _resultBox.Size = new Size(396, 520);
        _resultBox.TabIndex = 3;
        _resultBox.Text = "";
        //
        // LibraryPage
        //
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Controls.Add(_root);
        Margin = new Padding(0);
        Name = "LibraryPage";
        Size = new Size(1144, 692);
        _root.ResumeLayout(false);
        _toolbarCard.ResumeLayout(false);
        _toolbarCard.PerformLayout();
        _kindRow.ResumeLayout(false);
        _split.ResumeLayout(false);
        _gridCard.ResumeLayout(false);
        _gridCard.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_grid).EndInit();
        _reportCard.ResumeLayout(false);
        _reportCard.PerformLayout();
        ResumeLayout(false);
    }
}
