#include "InputInjector.h"

#include <X11/Xlib.h>
#include <X11/extensions/XTest.h>
#include <X11/Xutil.h>
#include <thread>
#include <chrono>
#include <stdexcept>

namespace QuadClicker {

class InputInjectorX11 : public InputInjector {
public:
    InputInjectorX11()
        : m_display(XOpenDisplay(nullptr))
    {
        if (!m_display)
            throw std::runtime_error("InputInjectorX11: cannot open X display.");

        int event_base = 0, error_base = 0;
        if (!XTestQueryExtension(m_display, &event_base, &error_base, &m_major, &m_minor))
            throw std::runtime_error("InputInjectorX11: XTest extension not available.");
    }

    ~InputInjectorX11() override
    {
        if (m_display) {
            XCloseDisplay(m_display);
            m_display = nullptr;
        }
    }

    void click(MouseButton button, ClickType clickType,
               std::atomic<bool>& cancel) override
    {
        sendClick(button);

        if (clickType == ClickType::Double && !cancel.load()) {
            // Query the system double-click interval
            int interval = doubleClickIntervalMs();
            auto deadline = std::chrono::steady_clock::now()
                          + std::chrono::milliseconds(interval);
            while (std::chrono::steady_clock::now() < deadline) {
                if (cancel.load()) return;
                std::this_thread::sleep_for(std::chrono::milliseconds(5));
            }
            if (!cancel.load())
                sendClick(button);
        }
    }

    void moveCursor(int x, int y) override
    {
        Window root = DefaultRootWindow(m_display);
        XWarpPointer(m_display, None, root, 0, 0, 0, 0, x, y);
        XFlush(m_display);
    }

private:
    Display* m_display{nullptr};
    int      m_major{0};
    int      m_minor{0};

    // XTest button numbers: left=1, middle=2, right=3
    static unsigned int xButton(MouseButton b)
    {
        switch (b) {
        case MouseButton::Right:  return Button3;
        case MouseButton::Middle: return Button2;
        default:                  return Button1;
        }
    }

    void sendClick(MouseButton button)
    {
        unsigned int btn = xButton(button);
        // Press
        XTestFakeButtonEvent(m_display, btn, True,  CurrentTime);
        XFlush(m_display);
        // Release
        XTestFakeButtonEvent(m_display, btn, False, CurrentTime);
        XFlush(m_display);
    }

    int doubleClickIntervalMs()
    {
        // XGetDefault returns the Xresource "Net/DoubleClickTime" if set
        const char* val = XGetDefault(m_display, "*", "doubleClickTime");
        if (val) {
            try { return std::stoi(val); } catch (...) {}
        }
        return 500; // fallback
    }
};

// Factory function — defined here so InputInjectorFactory.cpp can use it
InputInjector* createX11Injector()
{
    return new InputInjectorX11();
}

} // namespace QuadClicker
