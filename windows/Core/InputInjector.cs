using QuadClicker.Models;
using QuadClicker.PInvoke;
using System.Runtime.InteropServices;

namespace QuadClicker.Core;

/// <summary>Injects mouse click events via SendInput. Supports L/R/M buttons and single/double clicks.</summary>
internal static class InputInjector
{
    internal static void Click(MouseButton button, ClickType clickType, CancellationToken token = default)
    {
        SendClick(button);

        if (clickType == ClickType.Double && !token.IsCancellationRequested)
        {
            uint interval = NativeMethods.GetDoubleClickTime();
            token.WaitHandle.WaitOne((int)interval);
            if (!token.IsCancellationRequested)
                SendClick(button);
        }
    }

    private static void SendClick(MouseButton button)
    {
        (uint down, uint up) = button switch
        {
            MouseButton.Right  => (NativeMethods.MOUSEEVENTF_RIGHTDOWN,  NativeMethods.MOUSEEVENTF_RIGHTUP),
            MouseButton.Middle => (NativeMethods.MOUSEEVENTF_MIDDLEDOWN, NativeMethods.MOUSEEVENTF_MIDDLEUP),
            _                  => (NativeMethods.MOUSEEVENTF_LEFTDOWN,   NativeMethods.MOUSEEVENTF_LEFTUP),
        };

        var inputs = new NativeMethods.INPUT[]
        {
            new() { Type = NativeMethods.INPUT_MOUSE, Data = new() { Mouse = new() { Flags = down } } },
            new() { Type = NativeMethods.INPUT_MOUSE, Data = new() { Mouse = new() { Flags = up   } } },
        };

        uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent == 0)
            throw new InvalidOperationException(
                $"SendInput failed (error {Marshal.GetLastWin32Error()}). Input may be blocked by UIPI.");
    }
}
