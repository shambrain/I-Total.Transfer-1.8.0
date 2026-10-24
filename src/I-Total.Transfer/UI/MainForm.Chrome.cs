using System.Runtime.InteropServices;

namespace ITotalTransfer.UI;

/// <summary>
/// Owner-drawn window chrome. The app keeps the standard resizable window styles — so Windows still
/// gives it a drop shadow, rounded corners, snap layouts, the Alt+Space system menu and correct
/// taskbar/Alt+Tab behaviour — but the non-client area is collapsed with WM_NCCALCSIZE, which removes
/// the OS caption entirely. The brand band becomes the title bar, and the caption buttons are painted
/// on the same ramp so the window reads as one uninterrupted surface.
/// </summary>
public sealed partial class MainForm
{
    private const int WmNcCalcSize = 0x0083;
    private const int WmNcHitTest = 0x0084;
    private const int WmGetMinMaxInfo = 0x0024;
    private const int WmSysCommand = 0x0112;
    private const int WmSize = 0x0005;
    private const int WmActivate = 0x0006;
    private const int WmExitSizeMove = 0x0232;
    private const int WmNcLButtonDown = 0x00A1;
    private const int WmNcLButtonDblClk = 0x00A3;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonDblClk = 0x0203;

    private const int ScMaximize = 0xF030;
    private const int ScRestore = 0xF120;

    private const int SmCxSizeFrame = 32;
    private const int SmCySizeFrame = 33;
    private const int SmCxPaddedBorder = 92;

    private const int HtClient = 1;
    private const int HtCaption = 2;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NcCalcSizeParams { public NativeRect Rgrc0, Rgrc1, Rgrc2; public IntPtr LpPos; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    /// <summary>The window rectangle to return to when a maximized window is restored.</summary>
    private Rectangle _normalBounds;

    /// <summary>True while a restore is in flight, so the sizing messages it raises are not recorded.</summary>
    private bool _restoring;

    /// <summary>Records the current rectangle as the one to restore to, unless the window is not in its normal state.</summary>
    private void RememberNormalBounds()
    {
        if (_closing || !IsHandleCreated || IsZoomed(Handle) || IsIconic(Handle))
        {
            return;
        }
        _normalBounds = Bounds;
    }

    /// <summary>Thickness of the invisible resize border, in device pixels.</summary>
    private int ResizeBorder => Math.Max(6, (int)Math.Round(6 * DeviceDpi / 96.0));

    /// <summary>Height of the draggable caption band, in device pixels.</summary>
    private int CaptionHeight => _header.Height;

    /// <summary>Installs the caption-drag filter. Called once the shell is built.</summary>
    private void InstallChromeBehaviour()
    {
        _minimizeButton.AccessibleName = "Minimize";
        _maximizeButton.AccessibleName = "Maximize or restore";
        _closeButton.AccessibleName = "Close";
        _minimizeButton.Click += (_, _) => WindowState = FormWindowState.Minimized;
        _maximizeButton.Click += (_, _) => ToggleMaximize();
        _closeButton.Click += (_, _) => Close();
        Application.AddMessageFilter(new CaptionDragFilter(this));
        UpdateMaximizeGlyph();
    }

    private void ToggleMaximize()
    {
        // Both directions are routed through SC_MAXIMIZE/SC_RESTORE so that the caption button, a
        // double-click on the band and the system menu all take the same code path — the one that
        // keeps the normal rectangle on record.
        int command = IsZoomed(Handle) || WindowState == FormWindowState.Maximized ? ScRestore : ScMaximize;
        SendMessage(Handle, WmSysCommand, (IntPtr)command, IntPtr.Zero);
    }

    private void UpdateMaximizeGlyph()
    {
        string glyph = IsZoomed(Handle) || WindowState == FormWindowState.Maximized
            ? Glyphs.ChromeRestore
            : Glyphs.ChromeMaximize;
        if (_maximizeButton.Glyph == glyph) return;
        _maximizeButton.Glyph = glyph;
        _maximizeButton.Invalidate();
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WmGetMinMaxInfo:
                // A window with a collapsed non-client area has no frame for Windows to subtract, so
                // "maximized" is defined here as exactly the monitor's working area. Doing it with
                // WinForms' MaximizedBounds property instead makes the restored rectangle creep outwards
                // by the frame on every maximise/restore cycle.
                ApplyMaximizedBounds(m.LParam);
                return;

            case WmNcCalcSize when m.WParam != IntPtr.Zero:
                // Claim the whole non-client area as client, keeping the window's styles intact so DWM
                // still supplies the shadow, snap layouts and the system menu. A maximized window's
                // rectangle is inflated by the frame Windows still believes it has, so that inset is
                // handed back — otherwise the top of the caption band would sit off-screen.
                if (IsZoomed(Handle))
                {
                    int fx = GetSystemMetrics(SmCxSizeFrame) + GetSystemMetrics(SmCxPaddedBorder);
                    int fy = GetSystemMetrics(SmCySizeFrame) + GetSystemMetrics(SmCxPaddedBorder);
                    var p = Marshal.PtrToStructure<NcCalcSizeParams>(m.LParam);
                    p.Rgrc0.Left += fx;
                    p.Rgrc0.Top += fy;
                    p.Rgrc0.Right -= fx;
                    p.Rgrc0.Bottom -= fy;
                    Marshal.StructureToPtr(p, m.LParam, false);
                }
                m.Result = IntPtr.Zero;
                return;

            case WmNcHitTest:
                m.Result = (IntPtr)HitTest(m);
                return;

            case WmSysCommand:
                if (HandleSysCommand(ref m)) return;
                break;
        }

        base.WndProc(ref m);

        // WM_SIZE / WM_ACTIVATE — keep the maximize/restore glyph in step with the real window state.
        if (m.Msg is WmSize or WmActivate) UpdateMaximizeGlyph();
        // The remembered normal rectangle is refreshed only when a user drag or resize finishes. Sizing
        // messages raised while maximise/restore is being settled carry intermediate rectangles, and
        // recording one of those would make the next restore land on the wrong size.
        if (m.Msg == WmExitSizeMove && !_restoring)
        {
            RememberNormalBounds();
        }
    }

    /// <summary>
    /// Keeps the "normal" rectangle on record so that restoring is exact. Windows rounds the restored
    /// rectangle outwards by the frame it still reserves for a window whose non-client area is
    /// collapsed, which would otherwise make the window grow a little on every maximise/restore cycle.
    /// </summary>
    private bool HandleSysCommand(ref Message m)
    {
        int command = (int)m.WParam & 0xFFF0;
        switch (command)
        {
            case ScMaximize:
                if (!IsZoomed(Handle)) RememberNormalBounds();
                base.WndProc(ref m);
                return true;

            case ScRestore:
                Rectangle target = _normalBounds;
                _restoring = true;
                try { base.WndProc(ref m); }
                finally { _restoring = false; }
                if (!target.IsEmpty && !IsZoomed(Handle) && !IsIconic(Handle))
                {
                    // The window rectangle is set directly rather than through Form.Bounds: WinForms
                    // converts a Form's bounds through the frame it believes the window still has, so
                    // assigning Bounds would hand back a slightly different rectangle than requested.
                    SetWindowPos(Handle, IntPtr.Zero, target.Left, target.Top, target.Width, target.Height,
                        SwpNoZOrder | SwpNoActivate);
                    // Restoring from the minimized state is finished by WinForms after this message, so
                    // the rectangle is asserted once more when the message queue drains.
                    BeginInvoke(() =>
                    {
                        if (_closing || IsDisposed || !IsHandleCreated) return;
                        if (IsZoomed(Handle) || IsIconic(Handle)) return;
                        SetWindowPos(Handle, IntPtr.Zero, target.Left, target.Top, target.Width, target.Height,
                            SwpNoZOrder | SwpNoActivate);
                    });
                }
                return true;
        }
        return false;
    }

    /// <summary>Pins the maximized window (and therefore its client, since they are the same size) to the working area.</summary>
    private void ApplyMaximizedBounds(IntPtr lParam)
    {
        if (lParam == IntPtr.Zero) return;
        Rectangle wa = Screen.FromHandle(Handle).WorkingArea;
        var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        info.MaxPosition = new NativePoint { X = wa.Left, Y = wa.Top };
        info.MaxSize = new NativePoint { X = wa.Width, Y = wa.Height };
        int minW = (int)Math.Round(760 * DeviceDpi / 96.0);
        int minH = (int)Math.Round(520 * DeviceDpi / 96.0);
        info.MinTrackSize = new NativePoint { X = minW, Y = minH };
        Marshal.StructureToPtr(info, lParam, false);
    }

    /// <summary>Resolves the window region under the cursor: resize edges first, then the caption band.</summary>
    private int HitTest(Message m)
    {
        int x = unchecked((short)(long)m.LParam);
        int y = unchecked((short)((long)m.LParam >> 16));
        var screen = new NativeRect { Left = x, Top = y };
        NativeRect window = screen;
        window.Left -= Left;
        window.Top -= Top;

        if (!IsZoomed(Handle))
        {
            int border = ResizeBorder;
            bool left = x <= window.Left + border;
            bool right = x >= window.Left + Width - border;
            bool top = y <= window.Top + border;
            bool bottom = y >= window.Top + Height - border;

            if (top && left) return HtTopLeft;
            if (top && right) return HtTopRight;
            if (bottom && left) return HtBottomLeft;
            if (bottom && right) return HtBottomRight;
            if (left) return HtLeft;
            if (right) return HtRight;
            if (top) return HtTop;
            if (bottom) return HtBottom;
        }

        int clientY = y - window.Top;
        return clientY >= 0 && clientY < CaptionHeight ? HtCaption : HtClient;
    }

    /// <summary>
    /// Turns a left press inside the caption band into an OS window-move, even when the press landed on
    /// one of the band's child labels. Without this, dragging by the title or the device caption would
    /// do nothing because child windows never report HTCAPTION for themselves.
    /// </summary>
    private sealed class CaptionDragFilter(MainForm form) : IMessageFilter
    {
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg is not (WmLButtonDown or WmLButtonDblClk)) return false;
            if (form.IsDisposed || !form.IsHandleCreated) return false;

            Control? target = Control.FromHandle(m.HWnd);
            if (target is null || target.TopLevelControl != form) return false;
            // Real controls in the band (the caption buttons) keep their own behaviour.
            if (target is ButtonBase or TextBoxBase or ComboBox) return false;

            Point client = form.PointToClient(Cursor.Position);
            if (client.Y < 0 || client.Y >= form.CaptionHeight) return false;

            ReleaseCapture();
            SendMessage(form.Handle, m.Msg == WmLButtonDblClk ? WmNcLButtonDblClk : WmNcLButtonDown, HtCaption, 0);
            return true;
        }
    }
}
