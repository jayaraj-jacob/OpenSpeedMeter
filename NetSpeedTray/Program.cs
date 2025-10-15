using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace NetSpeedTray
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new NetworkSpeedContext());
        }
    }

    /// <summary>
    /// Headless ApplicationContext hosting a NotifyIcon that shows current up/down speeds.
    /// </summary>
    internal class NetworkSpeedContext : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly Timer _timer;
        private readonly Dictionary<string, (long rx, long tx)> _last = new();
        private Icon? _lastIcon;
        private readonly SpeedBandForm _band = new();

        public NetworkSpeedContext()
        {
            // Tray menu
            var menu = new ContextMenuStrip();
            menu.Items.Add("Exit", null, (_, __) => ExitThread());

            _tray = new NotifyIcon
            {
                Visible = true,
                ContextMenuStrip = menu,
                Text = "NetSpeedTray",
                Icon = SystemIcons.Application
            };

            _timer = new Timer { Interval = 1000 }; // 1s
            _timer.Tick += (_, __) => UpdateSpeeds();
            _timer.Start();

            _band.Show();


            // Initial draw
            UpdateSpeeds();
        }

        protected override void ExitThreadCore()
        {
            _timer.Stop();
            _timer.Dispose();
            _band.Close();
            _band.Dispose();
            if (_lastIcon is not null) _lastIcon.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            base.ExitThreadCore();
        }

        private void UpdateSpeeds()
        {
            // Sum deltas across active NICs (exclude loopback/tunnel/virtual-ish)
            long rxTotalDelta = 0;
            long txTotalDelta = 0;

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;

                // Filter out loopback & tunnel
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                // Optional: ignore very low speed virtual adapters
                // if (nic.Speed > 0 && nic.Speed < 1_000_000) continue; // <1Mbps links, usually virtual

                var stats = nic.GetIPv4Statistics(); // cumulative bytes since boot
                var key = nic.Id;

                if (!_last.TryGetValue(key, out var prev))
                {
                    // prev is (long rx, long tx) with names
                    prev = (rx: stats.BytesReceived, tx: stats.BytesSent);
                }

                long rxDelta = stats.BytesReceived - prev.rx;
                long txDelta = stats.BytesSent - prev.tx;

                // Clamp negatives (rare, but just in case of counter resets)
                if (rxDelta < 0) rxDelta = 0;
                if (txDelta < 0) txDelta = 0;

                rxTotalDelta += rxDelta;
                txTotalDelta += txDelta;

                _last[key] = (stats.BytesReceived, stats.BytesSent);
            }

            // Bytes/sec → bits/sec for display (more intuitive for network)
            double downBps = rxTotalDelta * 8.0;
            double upBps = txTotalDelta * 8.0;

            var downText = FormatBitsPerSec(downBps);
            var upText = FormatBitsPerSec(upBps);

            // Tooltip (full)
            _tray.Text = $"↓ {downText}/s | ↑ {upText}/s";

            _band.UpdateSpeeds(upText, downText);

            // Draw a tiny icon with two lines: up & down
            var icon = CreateIcon(upText, downText);
            // Swap icons to avoid handle leak
            var old = _lastIcon;
            _tray.Icon = icon;
            _lastIcon = icon;
            old?.Dispose();
        }

        private static string FormatBitsPerSec(double bps)
        {
            // Uses SI units: K, M, G (bits per second)
            if (bps < 1000) return $"{bps:0} b";
            if (bps < 1_000_000) return $"{bps / 1_000:0.0} Kb";
            if (bps < 1_000_000_000) return $"{bps / 1_000_000:0.0} Mb";
            return $"{bps / 1_000_000_000:0.00} G";
        }

        private static Icon CreateIcon(string up, string down)
        {
            // Create a 32x32 icon for clarity (scales in tray with DPI)
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using var font = new Font("Segoe UI", 10, FontStyle.Bold, GraphicsUnit.Pixel);
            using var fontSmall = new Font("Segoe UI", 9, FontStyle.Regular, GraphicsUnit.Pixel);
            using var brushUp = new SolidBrush(Color.ForestGreen);
            using var brushDown = new SolidBrush(Color.RoyalBlue);

            // Up arrow + text
            g.DrawString("↑", font, brushUp, new PointF(1, 2));
            g.DrawString(up, fontSmall, brushUp, new PointF(10, 2));

            // Down arrow + text
            g.DrawString("↓", font, brushDown, new PointF(1, 16));
            g.DrawString(down, fontSmall, brushDown, new PointF(10, 16));

            // Convert bitmap to icon
            var hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }
    }
}
