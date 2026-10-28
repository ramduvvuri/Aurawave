using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using AuraWave.Models;
using AuraWave.Utilities;

namespace AuraWave.Windows
{
    public static class NativeWindowInterop
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WM_HOTKEY = 0x0312;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_NOREPEAT = 0x4000;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowW(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowExW(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessageTimeoutW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        public static void ApplyOverlayWindowStyles(Window window, bool clickThrough, bool alwaysOnTop, PresentationMode mode)
        {
            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;

            if (clickThrough)
                exStyle |= WS_EX_TRANSPARENT;
            else
                exStyle &= ~WS_EX_TRANSPARENT;

            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            if (mode == PresentationMode.DesktopBackgroundWorkerW)
            {
                if (!TryAttachToWorkerW(hwnd))
                {
                    SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
            else
            {
                SetParent(hwnd, IntPtr.Zero);
                IntPtr insertAfter = (mode == PresentationMode.AlwaysOnTopOverlay && alwaysOnTop)
                    ? HWND_TOPMOST
                    : HWND_NOTOPMOST;
                SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }

        private static bool TryAttachToWorkerW(IntPtr hwnd)
        {
            try
            {
                IntPtr progman = FindWindowW("Progman", null);
                if (progman == IntPtr.Zero) return false;

                SendMessageTimeoutW(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0x0000, 1000, out _);
                IntPtr workerW = IntPtr.Zero;
                IntPtr cur = IntPtr.Zero;

                while ((cur = FindWindowExW(IntPtr.Zero, cur, "WorkerW", null)) != IntPtr.Zero)
                {
                    IntPtr shellView = FindWindowExW(cur, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (shellView != IntPtr.Zero)
                    {
                        workerW = FindWindowExW(IntPtr.Zero, cur, "WorkerW", null);
                        break;
                    }
                }

                if (workerW != IntPtr.Zero)
                {
                    SetParent(hwnd, workerW);
                    return true;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("WorkerW desktop attachment fallback triggered: " + ex.Message);
            }
            return false;
        }
    }
}