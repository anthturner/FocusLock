using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FocusLock
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayContext());
        }
    }

    public class TrayContext : ApplicationContext
    {
        private GlobalKeyboardHook _hook;
        private NotifyIcon trayIcon;

        private DateTime _lastKeyDown;
        private IntPtr _lastHwnd;

        private static int THROTTLE_RELEASE_TIME_IN_MS = 500; // amount of time to allow between window switching and typing

        public TrayContext()
        {
            // Initialize Tray Icon
            trayIcon = new NotifyIcon()
            {
                Icon = Icon.FromHandle(Properties.Resources.icon.GetHicon()),
                Text = "FocusLock",
                ContextMenu = new ContextMenu(new MenuItem[] {
                new MenuItem("Exit", Exit)
            }),
                Visible = true
            };

            _hook = new GlobalKeyboardHook(() => {
                _lastKeyDown = DateTime.Now;
            });
            _hook.Start();
            IntPtr m_hhook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, new WinEventDelegate(WinEventProc), 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        void Exit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint EVENT_SYSTEM_FOREGROUND = 3;

        [DllImport("USER32.DLL")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if ((DateTime.Now - _lastKeyDown).TotalMilliseconds < THROTTLE_RELEASE_TIME_IN_MS)
            {
                if (hwnd != _lastHwnd)
                    SetForegroundWindow(_lastHwnd);
            }
            else
                _lastHwnd = hwnd;
        }
    }
}
