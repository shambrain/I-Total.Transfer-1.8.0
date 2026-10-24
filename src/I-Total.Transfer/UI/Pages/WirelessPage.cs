using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ITotalTransfer.Core;
using ITotalTransfer.Services;
using ITotalTransfer.UI.Controls;

namespace ITotalTransfer.UI.Pages;

/// <summary>Wireless receiver control: bind settings, pairing ticket, approval queue and receipts.</summary>
public partial class WirelessPage : UserControl, IThemedPage
{
    public event EventHandler? StartRequested;
    public event EventHandler? StopRequested;
    public event EventHandler? PairRequested;

    /// <summary>Raised with the relative path the operator approved.</summary>
    public event Action<string>? ApproveRequested;

    /// <summary>Raised with the relative path the operator refused.</summary>
    public event Action<string>? RefuseRequested;

    public event EventHandler? ApproveAllRequested;
    public event EventHandler? RefuseAllRequested;

    private Theme _theme = Theme.NeonInstance;
    private bool _running;

    public WirelessPage()
    {
        InitializeComponent();
        Surface.FillColumns(_receivedGrid, 34, 12, 34, 20);
        Surface.FillColumns(_pendingGrid, 52, 14, 34);
        _theme = Theme.NeonInstance;
        _startButton.Click += (_, _) => StartRequested?.Invoke(this, EventArgs.Empty);
        _stopButton.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);
        _pairButton.Click += (_, _) => PairRequested?.Invoke(this, EventArgs.Empty);
        _approveButton.Click += (_, _) => RaiseDecision(ApproveRequested);
        _refuseButton.Click += (_, _) => RaiseDecision(RefuseRequested);
        _approveAllButton.Click += (_, _) => ApproveAllRequested?.Invoke(this, EventArgs.Empty);
        _refuseAllButton.Click += (_, _) => RefuseAllRequested?.Invoke(this, EventArgs.Empty);
        _pendingGrid.SelectionChanged += (_, _) => UpdateDecisionButtons();
        _autoApproveCheck.CheckedChanged += (_, _) =>
        {
            UpdateStartEnabled();
            AutoApproveChanged?.Invoke(_autoApproveCheck.Checked);
        };
        _enableCheck.CheckedChanged += (_, _) => UpdateStartEnabled();
        ApplyTheme(_theme);
        SetRunning(false, "", false);
        SetPending([]);
    }

    /// <summary>Records the active palette and applies it to this page's control tree.</summary>
    public void ApplyTheme(Theme theme)
    {
        _theme = theme;
        theme.ApplyAll(this);
        // The QR is always dark modules on white; that is a scanner requirement, not a theme choice.
        _qrBox.BackColor = Color.White;
        _ticketStateLabel.ForeColor = _theme.Muted;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool WirelessEnabled
    {
        get => _enableCheck.Checked;
        set => _enableCheck.Checked = value;
    }

    public string BindAddress => _addressCombo.SelectedIndex <= 0 ? "" : (_addressCombo.SelectedItem as string ?? "");

    public int Port => (int)_portBox.Value;

    public int TokenMinutes => (int)_tokenMinutesBox.Value;

    public bool RequireTls => _tlsCheck.Checked;

    /// <summary>True when the operator has chosen to skip the approval list.</summary>
    public bool AutoApprove => _autoApproveCheck.Checked;

    /// <summary>Raised when the operator changes the automatic-approval preference.</summary>
    public event Action<bool>? AutoApproveChanged;

    public void LoadSettings(AppSettings settings)
    {
        _enableCheck.Checked = settings.WirelessEnabled;
        _portBox.Value = Math.Clamp(settings.WirelessPort, (int)_portBox.Minimum, (int)_portBox.Maximum);
        _tokenMinutesBox.Value = Math.Clamp(settings.PairingTokenMinutes, (int)_tokenMinutesBox.Minimum, (int)_tokenMinutesBox.Maximum);
        _tlsCheck.Checked = settings.WirelessRequireTls;
        _autoApproveCheck.Checked = settings.WirelessAutoApprove;
        SetAddresses(WirelessReceiver.AvailableAddresses(), settings.WirelessBindAddress);
    }

    public void SetAddresses(IReadOnlyList<string> addresses, string selected)
    {
        _addressCombo.Items.Clear();
        _addressCombo.Items.Add("Automatic (private interface)");
        foreach (string address in addresses) _addressCombo.Items.Add(address);
        int index = 0;
        if (!string.IsNullOrWhiteSpace(selected))
        {
            int found = _addressCombo.Items.IndexOf(selected);
            if (found >= 0) index = found;
        }
        _addressCombo.SelectedIndex = index;
    }

    public void SetRunning(bool running, string url, bool tls)
    {
        _running = running;
        _startButton.Enabled = !running && WirelessEnabled;
        _stopButton.Enabled = running;
        _pairButton.Enabled = running;
        _addressCombo.Enabled = _portBox.Enabled = _tlsCheck.Enabled = _tokenMinutesBox.Enabled = !running;
        _enableCheck.Enabled = _autoApproveCheck.Enabled = !running;
        _statusLabel.Text = running
            ? $"Listening on {url} ({(tls ? "TLS" : "HTTP; no certificate")})."
            : "Receiver stopped.";
        _statusLabel.ForeColor = running ? (tls ? _theme.Ok : _theme.Warn) : _theme.Muted;
        if (!running)
        {
            SetTicket(null);
            SetPending([]);
        }
        UpdateDecisionButtons();
    }

    public void SetStatus(string text, bool warn)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = warn ? _theme.Warn : _theme.Muted;
    }

    /// <summary>Shows the single-use pairing secret as a URL and a scannable QR code.</summary>
    public void SetTicket(PairingTicket? ticket)
    {
        if (ticket is null)
        {
            _pairUrlBox.Text = "";
            SetQrImage(null);
            _ticketStateLabel.Text = "No pairing ticket. Start the receiver, then choose New ticket.";
            return;
        }

        _pairUrlBox.Text = ticket.Url;
        SetQrImage(RenderQr(ticket.Qr));
        _ticketStateLabel.Text =
            $"Single use. Valid until {ticket.ExpiresUtc.ToLocalTime():HH:mm:ss} (local time).{Environment.NewLine}{Environment.NewLine}" +
            "Scan the code with the sender, or paste the URL. The secret is spent the first time it is redeemed — " +
            "choose New ticket for another device.";
    }

    /// <summary>Updates the ticket line after a sender redeems (or fails to redeem) the secret.</summary>
    public void SetTicketState(string text, bool warn)
    {
        _ticketStateLabel.Text = text;
        _ticketStateLabel.ForeColor = warn ? _theme.Warn : _theme.Muted;
    }

    /// <summary>
    /// Withdraws a ticket that has been redeemed. The secret is single use, so leaving the URL and a
    /// scannable QR code on screen would offer an affordance that cannot work. The activity log still
    /// names the sender that redeemed it.
    /// </summary>
    public void MarkTicketSpent()
    {
        _pairUrlBox.Text = "";
        SetQrImage(null);
        _ticketStateLabel.ForeColor = _theme.Muted;
        _ticketStateLabel.Text =
            "The pairing secret has been redeemed and cannot be used again. The URL and QR code have been " +
            "withdrawn - choose New ticket to pair another sender.";
    }

    /// <summary>Replaces the approval list. Only offers still waiting are shown.</summary>
    public void SetPending(IReadOnlyList<OfferRecord> offers)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => SetPending(offers))); return; }
        string? selected = SelectedPendingPath();
        _pendingGrid.SuspendLayout();
        _pendingGrid.Rows.Clear();
        foreach (OfferRecord record in offers.Where(o => o.IsPending))
            _pendingGrid.Rows.Add(record.Offer.RelativePath, record.Offer.DeclaredBytes.ToString("N0"), record.Offer.Client);
        _pendingGrid.ResumeLayout();

        if (selected is not null)
        {
            foreach (DataGridViewRow row in _pendingGrid.Rows)
                if (string.Equals(row.Cells[0].Value as string, selected, StringComparison.OrdinalIgnoreCase))
                {
                    row.Selected = true;
                    break;
                }
        }

        int count = _pendingGrid.Rows.Count;
        _pendingTitle.Text = count == 0
            ? "Waiting for approval (none)"
            : $"Waiting for approval ({count:N0}) — nothing is written until you decide";
        UpdateDecisionButtons();
    }

    public void SetReceived(IReadOnlyList<ReceivedFile> files)
    {
        _receivedGrid.SuspendLayout();
        _receivedGrid.Rows.Clear();
        foreach (ReceivedFile file in files)
        {
            string head = file.Sha256.Length > 16 ? file.Sha256[..16] + "…" : file.Sha256;
            _receivedGrid.Rows.Add(file.RelativePath, file.Bytes.ToString("N0"), head, file.ReceivedUtc.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        _receivedGrid.ResumeLayout();
        _receivedTitle.Text = $"Received files ({files.Count:N0}) — SHA-256 verified on receipt";
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

    private void RaiseDecision(Action<string>? handler)
    {
        string? path = SelectedPendingPath();
        if (path is null) return;
        handler?.Invoke(path);
    }

    private string? SelectedPendingPath()
    {
        if (_pendingGrid.SelectedRows.Count == 0) return null;
        return _pendingGrid.SelectedRows[0].Cells[0].Value as string;
    }

    private void UpdateDecisionButtons()
    {
        bool hasSelection = _pendingGrid.SelectedRows.Count > 0;
        bool hasAny = _pendingGrid.Rows.Count > 0;
        _approveButton.Enabled = hasSelection;
        _refuseButton.Enabled = hasSelection;
        _approveAllButton.Enabled = hasAny;
        _refuseAllButton.Enabled = hasAny;
    }

    /// <summary>The receiver may only start when the operator has enabled it.</summary>
    private void UpdateStartEnabled()
    {
        _startButton.Enabled = !_running && WirelessEnabled;
        _autoApproveCheck.Enabled = !_running;
    }

    private void SetQrImage(Bitmap? bitmap)
    {
        Image? previous = _qrBox.Image;
        _qrBox.Image = bitmap;
        previous?.Dispose();
        _qrBox.Visible = bitmap is not null;
    }

    /// <summary>
    /// Renders a QR symbol at a whole number of device pixels per module, so every module is a crisp
    /// square. A 4-module quiet zone is included because a scanner needs it to find the symbol.
    /// </summary>
    private Bitmap RenderQr(QrCode.Symbol symbol)
    {
        bool[,] modules = QrCode.WithQuietZone(symbol);
        int count = modules.GetLength(0);
        // Aim for roughly 360 device pixels so the control stays sharp at 150% and 200% scaling.
        int scale = Math.Max(2, 360 / count);
        var bitmap = new Bitmap(count * scale, count * scale, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bitmap);
        g.Clear(Color.White);
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        using var brush = new SolidBrush(Color.Black);
        for (int y = 0; y < count; y++)
            for (int x = 0; x < count; x++)
                if (modules[y, x])
                    g.FillRectangle(brush, x * scale, y * scale, scale, scale);
        return bitmap;
    }
}

