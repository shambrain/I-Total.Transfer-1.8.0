#nullable enable
using System.ComponentModel;

namespace ITotalTransfer.UI.Controls;

partial class MetricCard
{
    private System.ComponentModel.IContainer? components = null;
    private Label _titleLabel = null!;
    private Label _valueLabel = null!;
    private Label _footnoteLabel = null!;

    /// <summary>Designer-owned layout for a single metric tile (icon chip, title, value, footnote).</summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _titleLabel = new Label();
        _valueLabel = new Label();
        _footnoteLabel = new Label();
        SuspendLayout();
        //
        // _titleLabel — sits to the right of the painted icon chip.
        //
        _titleLabel.AutoSize = true;
        _titleLabel.Font = new Font("Segoe UI", 9F);
        _titleLabel.Location = new Point(64, 22);
        _titleLabel.Name = "_titleLabel";
        _titleLabel.Size = new Size(38, 15);
        _titleLabel.TabIndex = 0;
        _titleLabel.Tag = "muted";
        _titleLabel.Text = "Metric";
        //
        // _valueLabel
        //
        _valueLabel.AutoSize = true;
        _valueLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        _valueLabel.Location = new Point(16, 54);
        _valueLabel.Name = "_valueLabel";
        _valueLabel.Size = new Size(32, 32);
        _valueLabel.TabIndex = 1;
        _valueLabel.Text = "—";
        //
        // _footnoteLabel
        //
        _footnoteLabel.AutoEllipsis = true;
        _footnoteLabel.Font = new Font("Segoe UI", 8.5F);
        _footnoteLabel.Location = new Point(16, 92);
        _footnoteLabel.Name = "_footnoteLabel";
        _footnoteLabel.Size = new Size(214, 16);
        _footnoteLabel.TabIndex = 2;
        _footnoteLabel.Tag = "muted";
        _footnoteLabel.Text = "Footnote";
        //
        // MetricCard
        //
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Controls.Add(_footnoteLabel);
        Controls.Add(_valueLabel);
        Controls.Add(_titleLabel);
        Margin = new Padding(0, 0, 12, 0);
        Name = "MetricCard";
        Padding = new Padding(0);
        Size = new Size(248, 116);
        ResumeLayout(false);
        PerformLayout();
    }
}
