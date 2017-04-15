using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FocusLock
{
    public class GlobalKeyboardHook : IDisposable
    {
        private delegate IntPtr LowLevelKeyboardProc(int nCode, int wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        private readonly Action callback;

        private LowLevelKeyboardProc hookProc;
        private WindowHookHandle hook;

        public GlobalKeyboardHook(Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            this.callback = callback;
        }

        public void Start()
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                this.hookProc = this.HookCallback;

                var moduleHandle = NativeMethods.GetModuleHandle(curModule.ModuleName);
                this.hook = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, this.hookProc, moduleHandle, 0);
            }
        }

        public void Stop()
        {
            if (this.hook != null)
                this.hook.Dispose();

            this.hook = null;
        }

        private IntPtr HookCallback(int nCode, int wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_KEYUP))
                this.callback();
            
            return NativeMethods.CallNextHookEx(this.hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            this.Stop();
        }

        private class WindowHookHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            private WindowHookHandle() : base(true)
            {
            }

            public WindowHookHandle(IntPtr preexistingHandle, bool ownsHandle)
                : base(ownsHandle)
            {
                this.SetHandle(preexistingHandle);
            }

            protected override bool ReleaseHandle()
            {
                return NativeMethods.UnhookWindowsHookEx(this.handle);
            }
        }

        private class NativeMethods
        {
            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern WindowHookHandle SetWindowsHookEx(int idHook,
                LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr CallNextHookEx(WindowHookHandle hhk, int nCode, int wParam, IntPtr lParam);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern short GetKeyState(int keyCode);
        }
    }
}
