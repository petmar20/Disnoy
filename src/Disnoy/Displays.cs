using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

namespace Disnoy
{
    internal sealed class DisplayInfo
    {
        public string Device;
        public Rectangle Bounds;
        public bool Primary;

        // "\\.\DISPLAY14" becomes "Display 14".
        public string Name => "Display " + Device.Replace(@"\\.\DISPLAY", "");

        public string Size => Bounds.Width + " x " + Bounds.Height;

        public string Position => Bounds.X + ", " + Bounds.Y;
    }

    internal static class Displays
    {
        public static List<DisplayInfo> GetAll()
        {
            var displays = new List<DisplayInfo>();
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, hdc, rect, data) =>
            {
                Native.MONITORINFOEX info = NewMonitorInfo();
                Native.GetMonitorInfo(monitor, ref info);
                Native.RECT r = info.rcMonitor;
                displays.Add(new DisplayInfo
                {
                    Device = info.szDevice ?? "?",
                    Bounds = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom),
                    Primary = (info.dwFlags & Native.MONITORINFOF_PRIMARY) != 0,
                });
                return true;
            }, IntPtr.Zero);

            return displays.OrderBy(d => d.Bounds.X).ThenBy(d => d.Bounds.Y).ToList();
        }

        // Identifies an arrangement by size and position only. Windows renumbers a display
        // every time it reconnects, so device names can't be part of it.
        public static string ArrangementKey(IEnumerable<DisplayInfo> displays)
        {
            var parts = displays
                .Select(d => d.Bounds.Width + "x" + d.Bounds.Height + "@" + d.Bounds.X + "," + d.Bounds.Y)
                .OrderBy(s => s, StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        public static string Describe(IEnumerable<DisplayInfo> displays)
        {
            var parts = displays.Select(d => d.Name + " " + d.Size + " at " + d.Position + (d.Primary ? " (primary)" : ""));
            return string.Join("; ", parts);
        }

        public static string NameOfWindowDisplay(IntPtr hwnd)
        {
            Native.MONITORINFOEX info = NewMonitorInfo();
            Native.GetMonitorInfo(Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST), ref info);
            return "Display " + (info.szDevice ?? "?").Replace(@"\\.\DISPLAY", "");
        }

        private static Native.MONITORINFOEX NewMonitorInfo()
        {
            var info = new Native.MONITORINFOEX();
            info.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFOEX));
            return info;
        }
    }
}
