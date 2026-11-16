#pragma once

#include <QObject>
#include <QString>
#include <functional>
#include <atomic>
#include <thread>
#include <mutex>
#include <map>

namespace QuadClicker {

/// Registers global hotkeys and dispatches them via callbacks.
///
/// X11:     Uses XGrabKey on the root window. A background thread polls XNextEvent.
/// Wayland: Global hotkeys are not supported by the Wayland protocol. Registration
///          is silently skipped and a note is surfaced in the UI.
///
/// Thread safety: register() and unregisterAll() are called from the UI thread
/// while the polling thread uses the same Display. That is only safe because
/// main() calls XInitThreads() before any other Xlib call (libX11 >= 1.8 does
/// this automatically; Ubuntu 22.04 ships 1.7).
///
/// registerHotkey() returns -1 when the chord is already grabbed by another
/// client (BadAccess), instead of claiming success for a hotkey that never fires.
class HotkeyManager : public QObject {
    Q_OBJECT

public:
    struct HotkeyDef {
        unsigned int modifiers; ///< X11 modifier mask (e.g. 0 for bare F-key)
        unsigned int keycode;   ///< X11 keycode (from XKeysymToKeycode)
        std::function<void()> callback;
    };

    explicit HotkeyManager(QObject* parent = nullptr);
    ~HotkeyManager() override;

    /// Register a hotkey. Returns a handle >= 0 on success, -1 on failure.
    /// On Wayland this always returns -1.
    int registerHotkey(unsigned int modifiers, unsigned int keysym,
                       std::function<void()> callback);

    /// Unregister a hotkey by handle returned from registerHotkey().
    void unregister(int handle);

    /// Unregister all hotkeys.
    void unregisterAll();

    /// Returns true if hotkeys are supported on the current platform.
    bool isSupported() const { return m_supported; }

    /// Human-readable note to display when hotkeys are unsupported.
    static QString unsupportedNote();

signals:
    void hotkeyFired(int handle);

private:
    void startPolling();
    void stopPolling();
    void pollLoop();

    bool                         m_supported{false};
    std::atomic<bool>            m_running{false};
    std::thread                  m_thread;
    std::mutex                   m_mutex;
    std::map<int, HotkeyDef>     m_hotkeys;
    int                          m_nextHandle{1};

    // X11 display shared by the poll thread (events) and the UI thread (grabs).
    // Grabs must be made on the same connection that receives the KeyPress events.
    void* m_display{nullptr}; // Display* stored as void* to avoid X11 include in header
};

} // namespace QuadClicker
