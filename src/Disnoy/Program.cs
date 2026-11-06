using System;
using System.Threading;
using System.Windows.Forms;

namespace Disnoy
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--write-icon")
            {
                AppIcon.WriteIcoFile(args[1]);
                return;
            }

            using (var mutex = new Mutex(false, @"Local\Disnoy"))
            {
                if (!mutex.WaitOne(0, false))
                {
                    // Already running: ask that copy to show its window.
                    Native.PostMessage(Native.HWND_BROADCAST, SystemWatcher.ShowMessage, IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                Application.ThreadException += (s, e) => Log.Write("error: " + e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write("fatal: " + e.ExceptionObject);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }
    }
}
