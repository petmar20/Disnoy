using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Disnoy
{
    internal sealed class MainForm : Form
    {
        private readonly LayoutKeeper keeper;

        private readonly PictureBox statusIcon = new PictureBox();
        private readonly Label statusTitle = new Label();
        private readonly Label statusDetail = new Label();
        private readonly ListView displayList = new ListView();
        private readonly ListView activityList = new ListView();
        private readonly CheckBox startWithWindows = new CheckBox();
        private readonly CheckBox refreshButtons = new CheckBox();
        private readonly Button pauseButton = new Button();
        private readonly Button fixButton = new Button();
        private readonly Button logButton = new Button();
        private readonly ToolTip toolTip = new ToolTip();

        private KeeperState shownState = (KeeperState)(-1);

        public MainForm(LayoutKeeper keeper)
        {
            this.keeper = keeper;

            Text = "Disnoy";
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            ClientSize = new Size(640, 520);
            MinimumSize = new Size(480, 420);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = AppIcon.IconFor(KeeperState.Watching, 32);

            BuildLayout();

            keeper.Changed += (s, e) => RefreshStatus();
            keeper.ActivityAdded += (s, entry) => AddActivityRow(entry, true);
            foreach (ActivityEntry entry in keeper.Activity)
            {
                AddActivityRow(entry, false);
            }
            ScrollActivityToEnd();
            RefreshStatus();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14),
                ColumnCount = 1,
                RowCount = 7,
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // status
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // "Displays"
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));     // display list
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // "Recent activity"
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));      // activity list
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // options
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // buttons

            root.Controls.Add(BuildStatusPanel(), 0, 0);
            root.Controls.Add(SectionLabel("Displays"), 0, 1);
            root.Controls.Add(BuildDisplayList(), 0, 2);
            root.Controls.Add(SectionLabel("Recent activity"), 0, 3);
            root.Controls.Add(BuildActivityList(), 0, 4);
            root.Controls.Add(BuildOptions(), 0, 5);
            root.Controls.Add(BuildButtons(), 0, 6);
            Controls.Add(root);
        }

        private Control BuildStatusPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 8),
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            statusIcon.Size = new Size(40, 40);
            statusIcon.SizeMode = PictureBoxSizeMode.Zoom;
            statusIcon.Margin = new Padding(0, 0, 10, 0);
            panel.Controls.Add(statusIcon, 0, 0);
            panel.SetRowSpan(statusIcon, 2);

            statusTitle.AutoSize = true;
            statusTitle.Font = new Font("Segoe UI Semibold", 12f);
            statusTitle.Margin = new Padding(0, 0, 0, 2);
            panel.Controls.Add(statusTitle, 1, 0);

            statusDetail.AutoSize = true;
            statusDetail.ForeColor = SystemColors.GrayText;
            statusDetail.Margin = new Padding(0);
            panel.Controls.Add(statusDetail, 1, 1);
            return panel;
        }

        private Control BuildDisplayList()
        {
            SetUpList(displayList);
            displayList.Columns.Add("Display", 120);
            displayList.Columns.Add("Size", 120);
            displayList.Columns.Add("Position", 120);
            displayList.Columns.Add("", 100);
            return displayList;
        }

        private Control BuildActivityList()
        {
            SetUpList(activityList);
            activityList.Columns.Add("Time", 120);
            activityList.Columns.Add("What happened", 400);
            activityList.ShowItemToolTips = true;
            activityList.Resize += (s, e) => FitLastColumn(activityList);
            return activityList;
        }

        private Control BuildOptions()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                Margin = new Padding(0, 8, 0, 0),
            };

            startWithWindows.Text = "Start with Windows";
            startWithWindows.AutoSize = true;
            startWithWindows.Checked = Settings.StartWithWindows;
            startWithWindows.CheckedChanged += (s, e) => Settings.StartWithWindows = startWithWindows.Checked;

            refreshButtons.Text = "Fix taskbar buttons too (minimized windows flicker for a moment)";
            refreshButtons.AutoSize = true;
            refreshButtons.Checked = keeper.RefreshTaskbarButtons;
            refreshButtons.CheckedChanged += (s, e) =>
            {
                keeper.RefreshTaskbarButtons = refreshButtons.Checked;
                Settings.RefreshTaskbarButtons = refreshButtons.Checked;
            };

            panel.Controls.Add(startWithWindows);
            panel.Controls.Add(refreshButtons);
            return panel;
        }

        private Control BuildButtons()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Padding(0, 8, 0, 0),
            };

            SetUpButton(pauseButton, "Pause");
            pauseButton.Click += (s, e) => keeper.Paused = !keeper.Paused;

            SetUpButton(fixButton, "Fix taskbar buttons");
            toolTip.SetToolTip(fixButton, "Moves each minimized window's button to the taskbar on its display.");
            fixButton.Click += (s, e) => keeper.FixTaskbarButtons();

            SetUpButton(logButton, "Open log");
            logButton.Click += (s, e) => OpenLog();

            panel.Controls.Add(pauseButton);
            panel.Controls.Add(fixButton);
            panel.Controls.Add(logButton);
            return panel;
        }

        private void RefreshStatus()
        {
            if (IsDisposed)
            {
                return;
            }

            KeeperState state = keeper.State;
            if (state != shownState)
            {
                statusIcon.Image?.Dispose();
                statusIcon.Image = AppIcon.Draw(128, AppIcon.ColorFor(state));
                shownState = state;
            }

            int displayCount = keeper.CurrentDisplays.Count;
            switch (state)
            {
                case KeeperState.Paused:
                    statusTitle.Text = "Paused";
                    statusDetail.Text = "Windows won't be put back until you resume.";
                    break;
                case KeeperState.DisplayMissing:
                    statusTitle.Text = "A display is missing";
                    statusDetail.Text = string.Format(
                        "Holding the layout from {0:T}. Windows go back when the display returns.", keeper.SavedAt);
                    break;
                default:
                    statusTitle.Text = string.Format("Watching {0} {1}", displayCount, displayCount == 1 ? "display" : "displays");
                    statusDetail.Text = string.Format("Remembering where {0} windows are.", keeper.RememberedWindowCount);
                    break;
            }
            if (keeper.LastRestoreAt.HasValue)
            {
                statusDetail.Text += string.Format(" Last fix at {0:t}: {1} put back.",
                    keeper.LastRestoreAt.Value, keeper.LastRestoreCount);
            }

            pauseButton.Text = keeper.Paused ? "Resume" : "Pause";
            startWithWindows.Checked = Settings.StartWithWindows;
            RefreshDisplayList();
        }

        private void RefreshDisplayList()
        {
            displayList.BeginUpdate();
            displayList.Items.Clear();
            foreach (DisplayInfo display in keeper.CurrentDisplays)
            {
                var item = new ListViewItem(display.Name);
                item.SubItems.Add(display.Size);
                item.SubItems.Add(display.Position);
                item.SubItems.Add(display.Primary ? "Primary" : "");
                displayList.Items.Add(item);
            }
            displayList.EndUpdate();
        }

        private void AddActivityRow(ActivityEntry entry, bool scroll)
        {
            if (IsDisposed)
            {
                return;
            }
            var item = new ListViewItem(entry.Time.ToString("MMM d, h:mm:ss tt"));
            item.SubItems.Add(entry.Text);
            item.ToolTipText = entry.Text;
            activityList.Items.Add(item);
            if (scroll)
            {
                ScrollActivityToEnd();
            }
        }

        private void ScrollActivityToEnd()
        {
            if (activityList.Items.Count > 0)
            {
                activityList.EnsureVisible(activityList.Items.Count - 1);
            }
        }

        private static void OpenLog()
        {
            Log.Write("log opened");
            if (File.Exists(Log.FilePath))
            {
                Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true });
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Closing the window keeps Disnoy running in the tray.
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        private static Label SectionLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Margin = new Padding(0, 10, 0, 4),
            };
        }

        private static void SetUpList(ListView list)
        {
            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            list.MultiSelect = false;
            list.HideSelection = true;
            list.Margin = new Padding(0);
        }

        private static void SetUpButton(Button button, string text)
        {
            button.Text = text;
            button.AutoSize = true;
            button.Padding = new Padding(8, 2, 8, 2);
            button.Margin = new Padding(8, 0, 0, 0);
        }

        private static void FitLastColumn(ListView list)
        {
            int others = 0;
            for (int i = 0; i < list.Columns.Count - 1; i++)
            {
                others += list.Columns[i].Width;
            }
            int width = list.ClientSize.Width - others;
            if (width > 50)
            {
                list.Columns[list.Columns.Count - 1].Width = width;
            }
        }
    }
}
