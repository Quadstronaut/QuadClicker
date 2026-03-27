#include "HotkeyManager.h"

#include <QGuiApplication>

#include <X11/Xlib.h>
#include <X11/keysym.h>

#include <stdexcept>

namespace QuadClicker {

HotkeyManager::HotkeyManager(QObject* parent)
    : QObject(parent)
{
    QString platform = QGuiApplication::platformName();
    if (platform == QLatin1String("wayland")) {
        m_supported = false;
        return;
    }

    // X11 path
    Display* dpy = XOpenDisplay(nullptr);
    if (!dpy) {
        m_supported = false;
        return;
    }
    m_display   = dpy;
    m_supported = true;
    startPolling();
}

HotkeyManager::~HotkeyManager()
{
    stopPolling();
    if (m_display) {
        XCloseDisplay(static_cast<Display*>(m_display));
        m_display = nullptr;
    }
}

int HotkeyManager::registerHotkey(unsigned int modifiers, unsigned int keysym,
                                   std::function<void()> callback)
{
    if (!m_supported || !m_display) return -1;

    Display* dpy   = static_cast<Display*>(m_display);
    Window   root  = DefaultRootWindow(dpy);
    KeyCode  code  = XKeysymToKeycode(dpy, static_cast<KeySym>(keysym));
    if (code == 0) return -1;

    // X11 grabs fire for each lock-key combination permutation too.
    // Grab all four combinations (no locks, CapsLock, NumLock, both).
    const unsigned int lockMasks[] = {0, LockMask, Mod2Mask, LockMask | Mod2Mask};

    std::lock_guard<std::mutex> lock(m_mutex);
    int handle = m_nextHandle++;

    for (auto extra : lockMasks) {
        XGrabKey(dpy, code, modifiers | extra, root, True,
                 GrabModeAsync, GrabModeAsync);
    }
    XFlush(dpy);

    m_hotkeys[handle] = HotkeyDef{modifiers, static_cast<unsigned int>(code), std::move(callback)};
    return handle;
}

void HotkeyManager::unregister(int handle)
{
    if (!m_supported || !m_display) return;

    std::lock_guard<std::mutex> lock(m_mutex);
    auto it = m_hotkeys.find(handle);
    if (it == m_hotkeys.end()) return;

    Display* dpy  = static_cast<Display*>(m_display);
    Window   root = DefaultRootWindow(dpy);
    KeyCode  code = static_cast<KeyCode>(it->second.keycode);

    const unsigned int lockMasks[] = {0, LockMask, Mod2Mask, LockMask | Mod2Mask};
    for (auto extra : lockMasks)
        XUngrabKey(dpy, code, it->second.modifiers | extra, root);

    XFlush(dpy);
    m_hotkeys.erase(it);
}

void HotkeyManager::unregisterAll()
{
    if (!m_supported || !m_display) return;

    std::lock_guard<std::mutex> lock(m_mutex);
    Display* dpy  = static_cast<Display*>(m_display);
    Window   root = DefaultRootWindow(dpy);

    const unsigned int lockMasks[] = {0, LockMask, Mod2Mask, LockMask | Mod2Mask};
    for (auto& [handle, def] : m_hotkeys) {
        KeyCode code = static_cast<KeyCode>(def.keycode);
        for (auto extra : lockMasks)
            XUngrabKey(dpy, code, def.modifiers | extra, root);
    }
    XFlush(dpy);
    m_hotkeys.clear();
}

QString HotkeyManager::unsupportedNote()
{
    return QStringLiteral(
        "Global hotkeys are not supported under Wayland due to protocol restrictions. "
        "Use the Start/Stop button or the system tray instead.");
}

void HotkeyManager::startPolling()
{
    m_running = true;
    m_thread  = std::thread(&HotkeyManager::pollLoop, this);
}

void HotkeyManager::stopPolling()
{
    if (!m_running.exchange(false)) return;

    // Wake up XNextEvent by sending a synthetic ClientMessage to the root window
    if (m_display) {
        Display* dpy  = static_cast<Display*>(m_display);
        Window   root = DefaultRootWindow(dpy);

        XClientMessageEvent ev{};
        ev.type         = ClientMessage;
        ev.window       = root;
        ev.message_type = XInternAtom(dpy, "_QUADCLICKER_WAKEUP", False);
        ev.format       = 32;
        XSendEvent(dpy, root, False, SubstructureNotifyMask,
                   reinterpret_cast<XEvent*>(&ev));
        XFlush(dpy);
    }

    if (m_thread.joinable()) m_thread.join();
}

void HotkeyManager::pollLoop()
{
    Display* dpy = static_cast<Display*>(m_display);
    if (!dpy) return;

    // Select KeyPress events on the root window
    Window root = DefaultRootWindow(dpy);
    XSelectInput(dpy, root, KeyPressMask);

    while (m_running.load()) {
        if (!XPending(dpy)) {
            // No events available — sleep briefly to avoid busy-wait
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
            continue;
        }

        XEvent ev;
        XNextEvent(dpy, &ev);

        if (ev.type == KeyPress) {
            unsigned int keycode = ev.xkey.keycode;
            // Strip lock masks for comparison
            unsigned int mods = ev.xkey.state & ~(LockMask | Mod2Mask);

            std::function<void()> callback;
            int matchedHandle = -1;

            {
                std::lock_guard<std::mutex> lock(m_mutex);
                for (auto& [handle, def] : m_hotkeys) {
                    if (def.keycode == keycode && def.modifiers == mods) {
                        callback      = def.callback;
                        matchedHandle = handle;
                        break;
                    }
                }
            }

            if (callback) {
                // Fire callback — it is the callback's responsibility to marshal
                // to the UI thread (e.g. via QMetaObject::invokeMethod)
                callback();
                emit hotkeyFired(matchedHandle);
            }
        }
        // Ignore the synthetic wakeup ClientMessage (it just unblocks XNextEvent)
    }
}

} // namespace QuadClicker
