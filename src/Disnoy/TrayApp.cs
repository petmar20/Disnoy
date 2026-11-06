using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Disnoy
{
    internal sealed class TrayApp : ApplicationContext
    {
        private readonly LayoutKeeper keeper = new LayoutKeeper();
        private readonly SystemWatcher watcher = new SystemWatcher();
        private readonly NotifyIcon trayIcon = new NotifyIcon();
        private readonly Timer pollTimer = new Timer { Interval = 1000 };
        private readonly ToolStripMenuItem pauseItem = new ToolStripMenuItem("Pause");
        private readonly ToolStripMenuItem startupItem = new ToolStripMenuItem("Start with Windows");
        private MainForm window;

        public TrayApp()
        {
            Log.Write("Disnoy started");
            keeper.RefreshTaskbarButtons = Settings.RefreshTaskbarButtons;
            keeper.Changed += (s, e) => UpdateTrayIcon();

            watcher.DisplaysChanged += (s, e) => keeper.Poll();
            watcher.ScreensChanged += (s, on) =>
            {
                keeper.ScreensOn = on;
                Log.Write(on ? "screens on" : "screens off");
            };
            watcher.SessionChanged += (s, name) => Log.Write("session: " + name);
            watcher.ShowRequested += (s, e) => ShowWindow();

            trayIcon.ContextMenuStrip = BuildMenu();
            trayIcon.DoubleClick += (s, e) => ShowWindow();
            trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ShowWindow();
                }
            };

            keeper.Poll();
            UpdateTrayIcon();
            trayIcon.Visible = true;

            pollTimer.Tick += (s, e) => Poll();
            pollTimer.Start();

            if (Settings.IsFirstRun)
            {
                Settings.StartWithWindows = true;
                Settings.MarkInitialized();
                ShowWindow();
            }
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();

            var openItem = new ToolStripMenuItem("Open Disnoy");
            openItem.Font = new Font(openItem.Font, FontStyle.Bold);
            openItem.Click += (s, e) => ShowWindow();

            var fixItem = new ToolStripMenuItem("Fix taskbar buttons");
            fixItem.Click += (s, e) => keeper.FixTaskbarButtons();

            pauseItem.Click += (s, e) => keeper.Paused = !keeper.Paused;
            startupItem.Click += (s, e) => Settings.StartWithWindows = !Settings.StartWithWindows;

            var logItem = new ToolStripMenuItem("Open log");
            logItem.Click += (s, e) =>
            {
                if (File.Exists(Log.FilePath))
                {
                    Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true });
                }
            };

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitThread();

            menu.Items.Add(openItem);
            menu.Items.Add(fixItem);
            menu.Items.Add(pauseItem);
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(logItem);
            menu.Items.Add(exitItem);

            menu.Opening += (s, e) =>
            {
                pauseItem.Checked = keeper.Paused;
                startupItem.Checked = Settings.StartWithWindows;
            };
            return menu;
        }

        private void Poll()
        {
            try
            {
                keeper.Poll();
            }
            catch (Exception ex)
            {
                Log.Write("error: " + ex);
            }
        }

        private void UpdateTrayIcon()
        {
            KeeperState state = keeper.State;
            trayIcon.Icon = AppIcon.IconFor(state, SystemInformation.SmallIconSize.Width);

            int count = keeper.CurrentDisplays.Count;
            switch (state)
            {
                case KeeperState.Paused:
                    trayIcon.Text = "Disnoy: paused";
                    break;
                case KeeperState.DisplayMissing:
                    trayIcon.Text = "Disnoy: a display is missing";
                    break;
                default:
                    trayIcon.Text = string.Format("Disnoy: watching {0} {1}", count, count == 1 ? "display" : "displays");
                    break;
            }
        }

        private void ShowWindow()
        {
            if (window == null || window.IsDisposed)
            {
                window = new MainForm(keeper);
            }
            window.Show();
            if (window.WindowState == FormWindowState.Minimized)
            {
                window.WindowState = FormWindowState.Normal;
            }
            window.Activate();
        }

        protected override void ExitThreadCore()
        {
            Log.Write("Disnoy stopped");
            pollTimer.Stop();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            watcher.Dispose();
            window?.Dispose();
            base.ExitThreadCore();
        }
    }
}
