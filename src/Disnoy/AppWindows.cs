using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Disnoy
{
    // The windows that get a taskbar button: visible, unowned, not tool windows, not cloaked.
    internal static class AppWindows
    {
        public static List<IntPtr> GetAll()
        {
            var windows = new List<IntPtr>();
            Native.EnumWindows((hwnd, data) =>
            {
                if (IsAppWindow(hwnd))
                {
                    windows.Add(hwnd);
                }
                return true;
            }, IntPtr.Zero);
            return windows;
        }

        public static bool IsAppWindow(IntPtr hwnd)
        {
            if (!Native.IsWindowVisible(hwnd))
            {
                return false;
            }
            if (Native.GetWindow(hwnd, Native.GW_OWNER) != IntPtr.Zero)
            {
                return false;
            }
            if ((Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE) & Native.WS_EX_TOOLWINDOW) != 0)
            {
                return false;
            }
            Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_CLOAKED, out int cloaked, sizeof(int));
            if (cloaked != 0)
            {
                return false;
            }
            return Title(hwnd).Length > 0;
        }

        public static string Title(IntPtr hwnd)
        {
            var text = new StringBuilder(256);
            Native.InternalGetWindowText(hwnd, text, text.Capacity);
            return text.ToString();
        }

        public static string ShortTitle(IntPtr hwnd)
        {
            string title = Title(hwnd);
            return title.Length > 50 ? title.Substring(0, 50) + "..." : title;
        }

        public static uint ProcessId(IntPtr hwnd)
        {
            Native.GetWindowThreadProcessId(hwnd, out uint processId);
            return processId;
        }

        public static string Describe(IntPtr hwnd)
        {
            string process = "?";
            try
            {
                process = Process.GetProcessById((int)ProcessId(hwnd)).ProcessName;
            }
            catch (ArgumentException)
            {
                // The process has exited.
            }
            return "[" + process + "] " + ShortTitle(hwnd);
        }

        public static bool TryGetPlacement(IntPtr hwnd, out Native.WINDOWPLACEMENT placement)
        {
            placement = new Native.WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(typeof(Native.WINDOWPLACEMENT));
            return Native.GetWindowPlacement(hwnd, ref placement);
        }
    }
}
