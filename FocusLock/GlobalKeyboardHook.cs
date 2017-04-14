using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace FocusLock
{
    public class GlobalKeyboardHook : IDisposable
    {
        private delegate IntPtr LowLevelKeyboardProc(int nCode, int wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private readonly Action callback;
        private readonly List<Key> downKeys = new List<Key>();

        private LowLevelKeyboardProc hookProc;
        private WindowHookHandle hook;

        public ModifierKeys ModifierKeys { get; set; }
        public List<Key> Keys { get; } = new List<Key>();
        // Copy distinct items from Keys into actualKeys on start, to stop them modifying mid-way and prevent duplicates
        private List<Key> actualKeys;


        public GlobalKeyboardHook(Action callback, params Key[] keys)
            : this(callback)
        {
            this.Keys.AddRange(keys);
        }

        public GlobalKeyboardHook(Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            this.callback = callback;
        }

        public void Start()
        {
            if (this.Keys.Count == 0)
                throw new InvalidOperationException("At least one key to listen for must be set");

            this.Stop();

            this.actualKeys = this.Keys.Distinct().ToList();

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

            this.actualKeys = null;
            this.downKeys.Clear();
        }

        private IntPtr HookCallback(int nCode, int wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_KEYUP))
            {
                var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey(Marshal.ReadInt32(lParam));

                // Shortcut for the single-key case
                if (this.actualKeys.Count == 1 && this.actualKeys[0] == key)
                {
                    if (wParam == WM_KEYDOWN && this.downKeys.Count == 0 && this.HasAllModifiers())
                    {
                        this.downKeys.Add(key);

                        this.callback();
                    }
                    else // wParam == WM_KEYUP
                    {
                        this.downKeys.Clear();
                    }
                }
                else if (this.actualKeys.Contains(key))
                {
                    if (wParam == WM_KEYDOWN)
                    {
                        if (!this.downKeys.Contains(key))
                        {
                            this.downKeys.Add(key);

                            if (this.actualKeys.Count == this.downKeys.Count && this.HasAllModifiers())
                            {
                                this.callback();
                            }
                        }
                    }
                    else // wParam == WM_KEYUP
                    {
                        this.downKeys.Remove(key);
                    }
                }
            }

            return NativeMethods.CallNextHookEx(this.hook, nCode, wParam, lParam);
        }

        private bool HasAllModifiers()
        {
            if (((this.ModifierKeys & ModifierKeys.Shift) > 0) != ((NativeMethods.GetKeyState(VK_SHIFT) & 0x8000) > 0))
                return false;

            if (((this.ModifierKeys & ModifierKeys.Control) > 0) != ((NativeMethods.GetKeyState(VK_CONTROL) & 0x8000) > 0))
                return false;

            if (((this.ModifierKeys & ModifierKeys.Alt) > 0) != ((NativeMethods.GetKeyState(VK_MENU) & 0x8000) > 0))
                return false;

            if (((this.ModifierKeys & ModifierKeys.Windows) > 0) !=
                ((NativeMethods.GetKeyState(VK_LWIN) & 0x8000) > 0 || (NativeMethods.GetKeyState(VK_RWIN) & 0x8000) > 0))
                return false;

            return true;
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
