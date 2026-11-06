using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Disnoy
{
    internal enum KeeperState
    {
        Watching,
        DisplayMissing,
        Paused,
    }

    internal sealed class ActivityEntry
    {
        public DateTime Time;
        public string Text;
    }

    // Remembers where every window is while all displays are connected. When a display drops out
    // and comes back, it puts the windows back once Windows has finished moving them around.
    internal sealed class LayoutKeeper
    {
        // Windows keeps moving windows for a couple of seconds after a display returns.
        private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan MaxSettleWait = TimeSpan.FromSeconds(20);
        // A display missing this long while the screens are on was most likely unplugged on purpose.
        private static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(2);
        private const int MaxActivityEntries = 200;

        private sealed class SavedWindow
        {
            public uint ProcessId;
            public Native.WINDOWPLACEMENT Placement;
            public string Display;
        }

        public event EventHandler Changed;
        public event EventHandler<ActivityEntry> ActivityAdded;

        private readonly List<ActivityEntry> activity = new List<ActivityEntry>();
        private List<DisplayInfo> displays = new List<DisplayInfo>();
        private string displaysDescription = "";
        private Dictionary<IntPtr, string> lastWindowDisplay = new Dictionary<IntPtr, string>();
        private DateTime lastWindowMove = DateTime.MinValue;
        private DateTime lastPoll = DateTime.Now;
        private bool paused;

        // The full arrangement: the most displays seen, and the window layout saved while it was in place.
        private int fullDisplayCount;
        private string fullArrangement;
        private Dictionary<IntPtr, SavedWindow> savedWindows = new Dictionary<IntPtr, SavedWindow>();
        private DateTime savedAt = DateTime.MinValue;

        private bool displayMissing;
        private TimeSpan missingWhileScreensOn;
        private DateTime? displaysBackAt;

        public bool ScreensOn { get; set; } = true;
        public bool RefreshTaskbarButtons { get; set; } = true;

        public KeeperState State => paused ? KeeperState.Paused
            : displayMissing ? KeeperState.DisplayMissing
            : KeeperState.Watching;

        public IReadOnlyList<DisplayInfo> CurrentDisplays => displays;
        public IReadOnlyList<ActivityEntry> Activity => activity;
        public int RememberedWindowCount => savedWindows.Count;
        public DateTime SavedAt => savedAt;
        public DateTime? LastRestoreAt { get; private set; }
        public int LastRestoreCount { get; private set; }

        public bool Paused
        {
            get => paused;
            set
            {
                if (paused == value)
                {
                    return;
                }
                paused = value;
                // Start from the current arrangement when resuming.
                displayMissing = false;
                displaysBackAt = null;
                fullDisplayCount = 0;
                fullArrangement = null;
                AddActivity(paused ? "Paused." : "Resumed.");
                RaiseChanged();
            }
        }

        public void Poll()
        {
            DateTime now = DateTime.Now;
            TimeSpan sinceLastPoll = now - lastPoll;
            lastPoll = now;

            List<DisplayInfo> current = UpdateDisplays();
            string arrangement = Displays.ArrangementKey(current);
            TrackWindowMoves();

            if (paused)
            {
                return;
            }

            if (!displayMissing)
            {
                if (current.Count >= fullDisplayCount)
                {
                    if (fullArrangement != null && arrangement != fullArrangement)
                    {
                        AddActivity("Displays changed. Using the new arrangement.");
                    }
                    fullDisplayCount = current.Count;
                    fullArrangement = arrangement;
                    SaveWindows(arrangement);
                }
                else
                {
                    displayMissing = true;
                    missingWhileScreensOn = TimeSpan.Zero;
                    displaysBackAt = null;
                    AddActivity(string.Format("A display dropped out. Holding the layout from {0:T}.", savedAt));
                    RaiseChanged();
                }
                return;
            }

            if (arrangement == fullArrangement)
            {
                if (displaysBackAt == null)
                {
                    displaysBackAt = now;
                    Log.Write("all displays back; waiting for windows to settle");
                }
                bool settled = now - lastWindowMove >= SettleTime && now - displaysBackAt.Value >= SettleTime;
                if (settled || now - displaysBackAt.Value >= MaxSettleWait)
                {
                    displayMissing = false;
                    displaysBackAt = null;
                    RestoreWindows();
                    RaiseChanged();
                }
                return;
            }

            displaysBackAt = null;
            if (current.Count >= fullDisplayCount)
            {
                AddActivity("Displays came back in a different arrangement, so windows were left where they are.");
                displayMissing = false;
                fullArrangement = arrangement;
                RaiseChanged();
                return;
            }

            if (ScreensOn)
            {
                missingWhileScreensOn += sinceLastPoll;
            }
            if (missingWhileScreensOn >= GiveUpAfter)
            {
                AddActivity("A display has been gone for 2 minutes. Using the displays that are left.");
                displayMissing = false;
                fullDisplayCount = current.Count;
                fullArrangement = arrangement;
                RaiseChanged();
            }
        }

        // Briefly shows each minimized window and minimizes it again, which makes Explorer
        // move its taskbar button to the taskbar of the display the window is on.
        public void FixTaskbarButtons()
        {
            var minimized = AppWindows.GetAll().Where(Native.IsIconic).ToList();
            AddActivity(string.Format("Fixing taskbar buttons for {0} minimized windows.", minimized.Count));
            RefreshButtonsInBackground(minimized, TimeSpan.Zero);
        }

        public void AddActivity(string text)
        {
            var entry = new ActivityEntry { Time = DateTime.Now, Text = text };
            activity.Add(entry);
            if (activity.Count > MaxActivityEntries)
            {
                activity.RemoveAt(0);
            }
            Log.Write(text);
            ActivityAdded?.Invoke(this, entry);
        }

        private List<DisplayInfo> UpdateDisplays()
        {
            List<DisplayInfo> current = Displays.GetAll();
            string description = Displays.Describe(current);
            if (description != displaysDescription)
            {
                displaysDescription = description;
                displays = current;
                Log.Write("displays (" + current.Count + "): " + description);
                RaiseChanged();
            }
            return current;
        }

        // Logs every window that changes display, and notes when the last move happened.
        private void TrackWindowMoves()
        {
            bool firstRun = lastWindowDisplay.Count == 0;
            var current = new Dictionary<IntPtr, string>();
            foreach (IntPtr hwnd in AppWindows.GetAll())
            {
                string display = Displays.NameOfWindowDisplay(hwnd);
                current[hwnd] = display;
                if (lastWindowDisplay.TryGetValue(hwnd, out string before) && before != display)
                {
                    lastWindowMove = DateTime.Now;
                    Log.Write(string.Format("  moved {0} -> {1}{2}  {3}",
                        before, display, Native.IsIconic(hwnd) ? " (minimized)" : "", AppWindows.Describe(hwnd)));
                }
                else if (firstRun)
                {
                    Log.Write(string.Format("  window on {0}{1}  {2}",
                        display, Native.IsIconic(hwnd) ? " (minimized)" : "", AppWindows.Describe(hwnd)));
                }
            }
            lastWindowDisplay = current;
        }

        private void SaveWindows(string arrangement)
        {
            var saved = new Dictionary<IntPtr, SavedWindow>();
            foreach (IntPtr hwnd in AppWindows.GetAll())
            {
                if (!AppWindows.TryGetPlacement(hwnd, out Native.WINDOWPLACEMENT placement))
                {
                    continue;
                }
                saved[hwnd] = new SavedWindow
                {
                    ProcessId = AppWindows.ProcessId(hwnd),
                    Placement = placement,
                    Display = Displays.NameOfWindowDisplay(hwnd),
                };
            }

            // Only keep it if no display changed while it was being taken.
            if (Displays.ArrangementKey(Displays.GetAll()) != arrangement)
            {
                return;
            }

            bool countChanged = saved.Count != savedWindows.Count;
            savedWindows = saved;
            savedAt = DateTime.Now;
            if (countChanged)
            {
                RaiseChanged();
            }
        }

        private void RestoreWindows()
        {
            var restoredTitles = new List<string>();
            var minimizedToRefresh = new List<IntPtr>();

            foreach (KeyValuePair<IntPtr, SavedWindow> entry in savedWindows)
            {
                IntPtr hwnd = entry.Key;
                SavedWindow saved = entry.Value;
                if (!Native.IsWindow(hwnd) || AppWindows.ProcessId(hwnd) != saved.ProcessId)
                {
                    continue;
                }
                if (!AppWindows.TryGetPlacement(hwnd, out Native.WINDOWPLACEMENT now))
                {
                    continue;
                }

                string savedState = StateOf(saved.Placement.showCmd);
                bool unchanged = StateOf(now.showCmd) == savedState
                    && SameRect(now.rcNormalPosition, saved.Placement.rcNormalPosition);
                if (unchanged)
                {
                    continue;
                }

                Native.WINDOWPLACEMENT target = saved.Placement;
                target.flags |= Native.WPF_ASYNCWINDOWPLACEMENT;
                if (savedState == "minimized")
                {
                    target.showCmd = Native.SW_SHOWMINNOACTIVE;
                }
                else if (savedState == "maximized")
                {
                    target.showCmd = Native.SW_SHOWMAXIMIZED;
                }
                else
                {
                    target.showCmd = Native.SW_SHOWNOACTIVATE;
                }

                if (Native.SetWindowPlacement(hwnd, ref target))
                {
                    restoredTitles.Add(AppWindows.ShortTitle(hwnd));
                    Log.Write(string.Format("  put back on {0}{1}  {2}",
                        saved.Display, savedState == "normal" ? "" : " (" + savedState + ")", AppWindows.Describe(hwnd)));
                    if (savedState == "minimized")
                    {
                        minimizedToRefresh.Add(hwnd);
                    }
                }
                else
                {
                    Log.Write("  could not put back  " + AppWindows.Describe(hwnd));
                }
            }

            LastRestoreAt = DateTime.Now;
            LastRestoreCount = restoredTitles.Count;
            if (restoredTitles.Count == 0)
            {
                AddActivity("All displays are back. Windows were already in place.");
            }
            else
            {
                AddActivity(string.Format("All displays are back. Put {0} {1} back: {2}",
                    restoredTitles.Count, restoredTitles.Count == 1 ? "window" : "windows", string.Join(", ", restoredTitles)));
            }

            if (RefreshTaskbarButtons && minimizedToRefresh.Count > 0)
            {
                // Give the async placements a moment to land first.
                RefreshButtonsInBackground(minimizedToRefresh, TimeSpan.FromMilliseconds(500));
            }
        }

        private static void RefreshButtonsInBackground(List<IntPtr> windows, TimeSpan delay)
        {
            Task.Run(() =>
            {
                Thread.Sleep(delay);
                foreach (IntPtr hwnd in windows)
                {
                    Native.ShowWindowAsync(hwnd, Native.SW_SHOWNOACTIVATE);
                    Thread.Sleep(100);
                    Native.ShowWindowAsync(hwnd, Native.SW_SHOWMINNOACTIVE);
                }
            });
        }

        private static string StateOf(int showCmd)
        {
            if (showCmd == Native.SW_SHOWMINIMIZED || showCmd == Native.SW_MINIMIZE || showCmd == Native.SW_SHOWMINNOACTIVE)
            {
                return "minimized";
            }
            if (showCmd == Native.SW_SHOWMAXIMIZED)
            {
                return "maximized";
            }
            return "normal";
        }

        private static bool SameRect(Native.RECT a, Native.RECT b)
        {
            return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
        }

        private void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
