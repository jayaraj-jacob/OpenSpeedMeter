using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class TaskbarUtil
{
    public enum ABEdge : uint { Left = 0, Top = 1, Right = 2, Bottom = 3 }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public int lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("shell32.dll")] private static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);
    private const uint ABM_GETTASKBARPOS = 0x00000005;
    private const uint ABM_GETSTATE = 0x00000004;
    private const int ABS_AUTOHIDE = 0x1;

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);

    public static bool TryGetTaskbar(out Rectangle rect, out ABEdge edge, out bool autoHide)
    {
        APPBARDATA abd = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
        uint res = SHAppBarMessage(ABM_GETTASKBARPOS, ref abd);
        if (res == 0)
        {
            rect = Rectangle.Empty; edge = ABEdge.Bottom; autoHide = false;
            return false;
        }
        rect = Rectangle.FromLTRB(abd.rc.Left, abd.rc.Top, abd.rc.Right, abd.rc.Bottom);
        edge = (ABEdge)abd.uEdge;

        abd = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
        autoHide = (SHAppBarMessage(ABM_GETSTATE, ref abd) & ABS_AUTOHIDE) != 0;
        return true;
    }

    /// <summary>Bounds of the area that contains the tray icons (chevron lives here).</summary>
    private static bool TryGetTrayNotifyRect(out Rectangle rect)
    {
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero) { rect = Rectangle.Empty; return false; }

        // Classic class names; still present on Win10/11
        var tray = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        if (tray == IntPtr.Zero) { rect = Rectangle.Empty; return false; }

        if (!GetWindowRect(tray, out var r)) { rect = Rectangle.Empty; return false; }
        rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        return true;
    }

    /// <summary>Bounds of the overflow flyout when it is visible (Win10/11).</summary>
    private static bool TryGetOverflowRect(out Rectangle rect)
    {
        // Win10/11 typically uses this class name for the overflow flyout;
        // On some builds there’s also "NotifyIconOverflowWindow" owned by Explorer.
        var overflow = FindWindow("NotifyIconOverflowWindow", null);
        if (overflow != IntPtr.Zero && IsWindowVisible(overflow))
        {
            if (GetWindowRect(overflow, out var r))
            {
                rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                return true;
            }
        }

        // Some Win11 builds use a XAML island with different class names; add more if needed.
        rect = Rectangle.Empty;
        return false;
    }

    public static bool IsOverflowVisible()
    {
        return TryGetOverflowRect(out _);
    }

    /// <summary>
    /// Computes a dock point that sits "before the tray", and shifts left while overflow is open,
    /// so your band never gets covered.
    /// </summary>
    public static Point GetDockPoint(Size bandSize, int margin = 6)
    {
        if (!TryGetTaskbar(out var tb, out var edge, out var autohide))
        {
            var wa = Screen.PrimaryScreen!.WorkingArea;
            return new Point(wa.Right - bandSize.Width - margin, wa.Bottom - bandSize.Height - margin);
        }

        bool hasTray = TryGetTrayNotifyRect(out var trayRect);
        bool overflowVisible = TryGetOverflowRect(out var overflowRect);
        int extra = autohide ? 2 : 0;

        // Prefer to anchor against the visible overflow, else against tray area, else against taskbar edge.
        Rectangle anchor = overflowVisible ? overflowRect : (hasTray ? trayRect : tb);

        return edge switch
        {
            ABEdge.Bottom => new Point(
                x: (overflowVisible ? anchor.Left : anchor.Left) - bandSize.Width - margin,   // to the LEFT of overflow/tray
                y: tb.Bottom - bandSize.Height - extra),

            ABEdge.Top => new Point(
                x: (overflowVisible ? anchor.Left : anchor.Left) - bandSize.Width - margin,
                y: tb.Top + extra),

            ABEdge.Right => new Point(
                x: tb.Right - bandSize.Width - extra,
                y: (overflowVisible ? anchor.Top : anchor.Bottom) - bandSize.Height - margin),

            ABEdge.Left => new Point(
                x: tb.Left + extra,
                y: (overflowVisible ? anchor.Top : anchor.Bottom) - bandSize.Height - margin),

            _ => new Point(tb.Right - bandSize.Width - margin, tb.Bottom - bandSize.Height - margin),
        };
    }
}
