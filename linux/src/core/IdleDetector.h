#pragma once

#include <chrono>

namespace QuadClicker {

/// Reports how long the system has been idle (no user input).
///
/// X11:     Uses XScreenSaverQueryInfo.
/// Wayland: Queries org.freedesktop.ScreenSaver via D-Bus.
/// On failure: returns 0ms (treats system as active — safe default).
class IdleDetector {
public:
    static std::chrono::milliseconds getIdleTime();
};

} // namespace QuadClicker
