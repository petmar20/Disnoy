using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Disnoy
{
    // A hidden top-level window that receives display, session and screen power notifications.
    internal sealed class SystemWatcher : NativeWindow, IDisposable
    {
        public static readonly int ShowMessage = Native.RegisterWindowMessage("Disnoy.Show");

        public event EventHandler DisplaysChanged;
        public event EventHandler<bool> ScreensChanged;
        public event EventHandler<string> SessionChanged;
        public event EventHandler ShowRequested;

        private readonly IntPtr powerNotification;

        public SystemWatcher()
        {
            CreateHandle(new CreateParams { Caption = "Disnoy" });
            Native.WTSRegisterSessionNotification(Handle, 0);
            powerNotification = Native.RegisterPowerSettingNotification(Handle, ref Native.GUID_CONSOLE_DISPLAY_STATE, 0);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_DISPLAYCHANGE)
            {
                DisplaysChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (m.Msg == Native.WM_WTSSESSION_CHANGE)
            {
                SessionChanged?.Invoke(this, SessionEventName(m.WParam.ToInt32()));
            }
            else if (m.Msg == Native.WM_POWERBROADCAST && m.WParam.ToInt32() == Native.PBT_POWERSETTINGCHANGE)
            {
                // POWERBROADCAST_SETTING: a GUID (16 bytes), the data length (4 bytes), then the data.
                int state = Marshal.ReadInt32(m.LParam, 20);
                ScreensChanged?.Invoke(this, state != 0);
            }
            else if (m.Msg == ShowMessage)
            {
                ShowRequested?.Invoke(this, EventArgs.Empty);
            }
            base.WndProc(ref m);
        }

        private static string SessionEventName(int code)
        {
            switch (code)
            {
                case 1: return "console connect";
                case 2: return "console disconnect";
                case 3: return "remote connect";
                case 4: return "remote disconnect";
                case 5: return "logon";
                case 6: return "logoff";
                case 7: return "lock";
                case 8: return "unlock";
                default: return "event " + code;
            }
        }

        public void Dispose()
        {
            Native.WTSUnRegisterSessionNotification(Handle);
            if (powerNotification != IntPtr.Zero)
            {
                Native.UnregisterPowerSettingNotification(powerNotification);
            }
            DestroyHandle();
        }
    }
}
