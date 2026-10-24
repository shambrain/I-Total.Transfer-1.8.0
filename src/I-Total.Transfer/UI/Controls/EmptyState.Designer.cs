#nullable enable
using System.ComponentModel;

namespace ITotalTransfer.UI.Controls;

partial class EmptyState
{
    private System.ComponentModel.IContainer? components = null;
    private Label _titleLabel = null!;
    private Label _subtitleLabel = null!;

    /// <summary>Designer-owned layout for the empty-collection placeholder.</summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _titleLabel = new Label();
        _subtitleLabel = new Label();
        SuspendLayout();
        //
        // _titleLabel
        //
        _titleLabel.AutoSize = true;
        _titleLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _titleLabel.Location = new Point(0, 0);
        _titleLabel.Name = "_titleLabel";
        _titleLabel.Size = new Size(120, 21);
        _titleLabel.TabIndex = 0;
        _titleLabel.Text = "Nothing here yet";
        _titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // _subtitleLabel
        //
        _subtitleLabel.AutoSize = true;
        _subtitleLabel.Font = new Font("Segoe UI", 9F);
        _subtitleLabel.Location = new Point(0, 24);
        _subtitleLabel.Name = "_subtitleLabel";
        _subtitleLabel.Size = new Size(220, 15);
        _subtitleLabel.TabIndex = 1;
        _subtitleLabel.Tag = "muted";
        _subtitleLabel.Text = "";
        _subtitleLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // EmptyState
        //
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Controls.Add(_subtitleLabel);
        Controls.Add(_titleLabel);
        Name = "EmptyState";
        Size = new Size(400, 200);
        ResumeLayout(false);
        PerformLayout();
    }
}
