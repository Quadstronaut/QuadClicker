using QuadClicker.PInvoke;

namespace QuadClicker.Core;

/// <summary>Registers and dispatches Win32 hotkeys for a given window handle.</summary>
internal sealed class HotkeyManager : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 0x2000;
    private bool _disposed;

    internal HotkeyManager(IntPtr hwnd) => _hwnd = hwnd;

    /// <summary>Registers a hotkey. Returns the hotkey ID on success, -1 on failure.</summary>
    internal int Register(uint modifiers, uint vk, Action handler)
    {
        int id = _nextId++;
        if (NativeMethods.RegisterHotKey(_hwnd, id, modifiers, vk))
        {
            _handlers[id] = handler;
            return id;
        }
        _nextId--; // Reclaim the unused ID
        return -1;
    }

    internal void Unregister(int id)
    {
        if (_handlers.Remove(id))
            NativeMethods.UnregisterHotKey(_hwnd, id);
    }

    internal void UnregisterAll()
    {
        foreach (var id in _handlers.Keys.ToList())
            NativeMethods.UnregisterHotKey(_hwnd, id);
        _handlers.Clear();
    }

    /// <summary>Call from WndProc on WM_HOTKEY. Returns true if handled.</summary>
    internal bool HandleMessage(int hotkeyId)
    {
        if (_handlers.TryGetValue(hotkeyId, out var action))
        {
            action();
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (!_disposed) { UnregisterAll(); _disposed = true; }
    }
}
