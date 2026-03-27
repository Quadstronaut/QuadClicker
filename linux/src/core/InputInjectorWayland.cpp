#include "InputInjector.h"

#include <linux/uinput.h>
#include <fcntl.h>
#include <unistd.h>
#include <cstring>
#include <stdexcept>
#include <thread>
#include <chrono>
#include <sys/ioctl.h>

namespace QuadClicker {

class InputInjectorWayland : public InputInjector {
public:
    InputInjectorWayland()
    {
        m_fd = open("/dev/uinput", O_WRONLY | O_NONBLOCK);
        if (m_fd < 0)
            throw std::runtime_error("InputInjectorWayland: cannot open /dev/uinput. "
                                     "Ensure the uinput kernel module is loaded and "
                                     "you have write permission to /dev/uinput.");

        // Enable key events and sync events
        if (ioctl(m_fd, UI_SET_EVBIT, EV_KEY) < 0 ||
            ioctl(m_fd, UI_SET_EVBIT, EV_SYN) < 0 ||
            ioctl(m_fd, UI_SET_KEYBIT, BTN_LEFT)   < 0 ||
            ioctl(m_fd, UI_SET_KEYBIT, BTN_RIGHT)  < 0 ||
            ioctl(m_fd, UI_SET_KEYBIT, BTN_MIDDLE) < 0)
        {
            close(m_fd);
            throw std::runtime_error("InputInjectorWayland: ioctl setup failed.");
        }

        // Enable relative axis for position reporting (needed by some compositors)
        if (ioctl(m_fd, UI_SET_EVBIT, EV_REL) < 0) {
            // Non-fatal — not all compositors require this
        }

        struct uinput_setup usetup{};
        std::memset(&usetup, 0, sizeof(usetup));
        usetup.id.bustype = BUS_USB;
        usetup.id.vendor  = 0x1234;
        usetup.id.product = 0x5678;
        std::strncpy(usetup.name, "QuadClicker Virtual Mouse",
                     UINPUT_MAX_NAME_SIZE - 1);

        if (ioctl(m_fd, UI_DEV_SETUP, &usetup) < 0 ||
            ioctl(m_fd, UI_DEV_CREATE) < 0)
        {
            close(m_fd);
            throw std::runtime_error("InputInjectorWayland: uinput device creation failed.");
        }

        // Small delay to let the kernel register the device
        std::this_thread::sleep_for(std::chrono::milliseconds(50));
    }

    ~InputInjectorWayland() override
    {
        if (m_fd >= 0) {
            ioctl(m_fd, UI_DEV_DESTROY);
            close(m_fd);
            m_fd = -1;
        }
    }

    void click(MouseButton button, ClickType clickType,
               std::atomic<bool>& cancel) override
    {
        sendClick(button);

        if (clickType == ClickType::Double && !cancel.load()) {
            // 500ms double-click interval for Wayland (no compositor API to query this)
            auto deadline = std::chrono::steady_clock::now()
                          + std::chrono::milliseconds(500);
            while (std::chrono::steady_clock::now() < deadline) {
                if (cancel.load()) return;
                std::this_thread::sleep_for(std::chrono::milliseconds(5));
            }
            if (!cancel.load())
                sendClick(button);
        }
    }

    void moveCursor(int /*x*/, int /*y*/) override
    {
        // uinput absolute positioning requires ABS_X/ABS_Y setup and screen dimensions.
        // Relative moves are unreliable without knowing current position.
        // The ClickEngine passes coordinates to this method; on Wayland the compositing
        // server controls cursor positioning and there is no portable "warp" API.
        // This is a known Wayland limitation — users should use "Current position" mode
        // or a compositor-specific solution (e.g. wlr-virtual-pointer protocol).
    }

private:
    int m_fd{-1};

    static __u16 uinputButton(MouseButton b)
    {
        switch (b) {
        case MouseButton::Right:  return BTN_RIGHT;
        case MouseButton::Middle: return BTN_MIDDLE;
        default:                  return BTN_LEFT;
        }
    }

    void writeEvent(__u16 type, __u16 code, __s32 value)
    {
        struct input_event ev{};
        std::memset(&ev, 0, sizeof(ev));
        ev.type  = type;
        ev.code  = code;
        ev.value = value;
        // Ignore write errors — non-fatal
        (void)write(m_fd, &ev, sizeof(ev));
    }

    void sendClick(MouseButton button)
    {
        __u16 btn = uinputButton(button);
        // Press
        writeEvent(EV_KEY, btn, 1);
        writeEvent(EV_SYN, SYN_REPORT, 0);
        // Release
        writeEvent(EV_KEY, btn, 0);
        writeEvent(EV_SYN, SYN_REPORT, 0);
    }
};

// Factory function — defined here so InputInjectorFactory.cpp can use it
InputInjector* createWaylandInjector()
{
    return new InputInjectorWayland();
}

} // namespace QuadClicker
