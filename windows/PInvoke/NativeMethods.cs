using System.Runtime.InteropServices;

namespace QuadClicker.PInvoke;

internal static partial class NativeMethods
{
    // ── Console ───────────────────────────────────────────────────────────────
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FreeConsole();

    // ── Input injection ───────────────────────────────────────────────────────
    // Kept as [DllImport]: INPUT[] array marshaling with source-gen requires unsafe pointers.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // ── Cursor ────────────────────────────────────────────────────────────────
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out POINT lpPoint);

    // ── Idle detection ────────────────────────────────────────────────────────
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetLastInputInfo(ref LASTINPUTINFO plii);

    // ── Low-level mouse hook ──────────────────────────────────────────────────
    // SetWindowsHookEx kept as [DllImport]: [LibraryImport] does not support managed delegate parameters.
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnhookWindowsHookEx(IntPtr hhk);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    // ── Hotkeys ───────────────────────────────────────────────────────────────
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    // ── Double-click timing ───────────────────────────────────────────────────
    [LibraryImport("user32.dll")]
    internal static partial uint GetDoubleClickTime();

    // ── Delegate ──────────────────────────────────────────────────────────────
    internal delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    // ── Structs ───────────────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint Type;
        public MOUSEKEYBDHARDWAREINPUT Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct MOUSEKEYBDHARDWAREINPUT
    {
        [FieldOffset(0)] public MOUSEINPUT Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    // ── Constants ─────────────────────────────────────────────────────────────
    internal const int WH_MOUSE_LL  = 14;
    internal const int WM_HOTKEY    = 0x0312;
    internal const int WM_LBUTTONUP = 0x0202;

    internal const uint INPUT_MOUSE = 0;

    internal const uint MOUSEEVENTF_LEFTDOWN   = 0x0002;
    internal const uint MOUSEEVENTF_LEFTUP     = 0x0004;
    internal const uint MOUSEEVENTF_RIGHTDOWN  = 0x0008;
    internal const uint MOUSEEVENTF_RIGHTUP    = 0x0010;
    internal const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    internal const uint MOUSEEVENTF_MIDDLEUP   = 0x0040;
}
