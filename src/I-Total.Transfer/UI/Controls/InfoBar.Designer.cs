#nullable enable
using System.ComponentModel;

namespace ITotalTransfer.UI.Controls;

partial class InfoBar
{
    private System.ComponentModel.IContainer? components = null;
    private Label _title = null!;
    private Label _message = null!;
    private RoundedButton _action = null!;
    private RoundedButton _dismiss = null!;

    /// <summary>Designer-owned layout for an inline Fluent message bar.</summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _title = new Label();
        _message = new Label();
        _action = new RoundedButton();
        _dismiss = new RoundedButton();
        SuspendLayout();
        //
        // _title
        //
        _title.AutoSize = true;
        _title.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);
        _title.Location = new Point(52, 12);
        _title.Name = "_title";
        _title.Size = new Size(38, 17);
        _title.TabIndex = 0;
        _title.Text = "Title";
        //
        // _message
        //
        // The anchors stay Top|Left: the message width is set by LayoutButtons so the text can never run
        // underneath the buttons, which are positioned from the right edge at layout time.
        _message.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        // Two lines tall: the bar's message carries the counts and the reason it matters, and a
        // single line would slice the guidance off. AutoEllipsis marks it if even two lines are not
        // enough on a narrow window, rather than cutting a sentence in half with no sign.
        _message.AutoEllipsis = true;
        _message.Font = new Font("Segoe UI", 9F);
        _message.Location = new Point(54, 30);
        _message.Name = "_message";
        _message.Size = new Size(760, 40);
        _message.TabIndex = 1;
        _message.Tag = "muted";
        _message.Text = "Message";
        //
        // _action
        //
        _action.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        _action.Location = new Point(830, 14);
        _action.Name = "_action";
        _action.Size = new Size(150, 32);
        _action.TabIndex = 2;
        _action.Tag = "primary";
        _action.Text = "Action";
        _action.Visible = false;
        //
        // _dismiss
        //
        _dismiss.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        _dismiss.Location = new Point(712, 14);
        _dismiss.Name = "_dismiss";
        _dismiss.Size = new Size(110, 32);
        _dismiss.TabIndex = 3;
        _dismiss.Tag = "subtle";
        _dismiss.Text = "Not now";
        _dismiss.Visible = false;
        //
        // InfoBar
        //
        Controls.Add(_dismiss);
        Controls.Add(_action);
        Controls.Add(_message);
        Controls.Add(_title);
        Margin = new Padding(0, 0, 0, 10);
        Name = "InfoBar";
        Padding = new Padding(0);
        Size = new Size(1000, 84);
        ResumeLayout(false);
        PerformLayout();
    }
}
