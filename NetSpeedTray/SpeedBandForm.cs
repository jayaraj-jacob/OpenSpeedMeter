using System;
using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using Microsoft.Win32;
using System.Runtime.InteropServices;
internal class SpeedBandForm : Form
{
    private string _up = "0";
    private string _down = "0";
    private readonly Timer _repositionTimer;
    private bool _overflowWasVisible;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
    int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    public SpeedBandForm()
    {
        // Small strip; tweak as you like
        Width = 200; Height = 20;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        BackColor = Color.Black;
        TransparencyKey = BackColor;
        //Opacity =  0.9; // gentle translucency

        // Prevent activation (no stealing focus)
        // and hide from Alt+Tab by using a tool window style.
        // Also avoids ugly taskbar button.
        //var cp = CreateParams;
        //// WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000
        //cp.ExStyle |= 0x00000080;
        //cp.ExStyle |= 0x08000000;
        //CreateParams = cp;

        // Reposition occasionally (taskbar moves, DPI changes, etc.)
        //_repositionTimer = new Timer { Interval = 1500 };
        //_repositionTimer.Tick += (_, __) => Reposition();
        //_repositionTimer.Start();

        _repositionTimer = new Timer { Interval = 250 };
        _repositionTimer.Tick += (_, __) => TickReposition();
        _repositionTimer.Start();
        // First position
        Reposition();

        // Optional: hide when an exclusive fullscreen window is active.
        // Leave commented unless you want that behavior.
        // var fsTimer = new Timer { Interval = 2000 };
        // fsTimer.Tick += (_, __) => Visible = !IsFullScreenAppRunning();
        // fsTimer.Start();

        // Handle DPI/Display changes
        SystemEvents_DisplayChanged(this, EventArgs.Empty);
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += SystemEvents_DisplayChanged;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080; // TOOLWINDOW
            cp.ExStyle |= 0x08000000; // NOACTIVATE
            return cp;
        }
    }


    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Reposition();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Background pill
        using var bg = new SolidBrush(Color.FromArgb(220, 25, 25, 25));
        using var pen = new Pen(Color.FromArgb(80, 255, 255, 255));
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        //g.FillRectangle(bg, rect);
        //g.DrawRectangle(pen, rect);

        // Text
        using var fBold = new Font("Segoe UI", 12, FontStyle.Bold, GraphicsUnit.Point);
        using var upBrush = new SolidBrush(Color.LightGreen);
        using var downBrush = new SolidBrush(Color.SkyBlue);

        g.DrawString($"↑ {_up}/s", fBold, upBrush, new PointF(6, 4));
        g.DrawString($"↓ {_down}/s", fBold, downBrush, new PointF(95, 4));
    }

    public void UpdateSpeeds(string up, string down)
    {
        _up = up;
        _down = down;
        Invalidate();
    }

    public void Reposition()
    {
        var pt = TaskbarUtil.GetDockPoint(new Size(Width, Height), margin: 5);
        Location = pt;
    }

    private void SystemEvents_DisplayChanged(object? sender, EventArgs e) => Reposition();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplayChanged;
            _repositionTimer?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void ReassertTopmost()
    {
        if (IsHandleCreated)
        {
            // Push back above taskbar & other topmost windows
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        // Belt & suspenders: toggle TopMost to refresh z-order
        BeginInvoke(new Action(() => { TopMost = false; TopMost = true; }));
        if (!Visible) Show(); // in case it was hidden by the system
    }

    private void TickReposition()
    {
        bool overflowNow = TaskbarUtil.IsOverflowVisible();

        // Always keep position fresh (multi-monitor / DPI / taskbar movement)
        Reposition();

        // If the overflow just closed, reassert topmost so we pop back above taskbar
        if (!overflowNow)
        {
            ReassertTopmost();
        }

        _overflowWasVisible = overflowNow;
    }

    // If you want “hide when fullscreen app”, you can implement a check here.
    // private bool IsFullScreenAppRunning() { ... }
}
