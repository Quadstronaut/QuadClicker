#pragma once

#include <QString>
#include <chrono>

namespace QuadClicker {

/// Reports how long the system has been idle (no user input).
///
/// X11:     Uses XScreenSaverQueryInfo.
/// Wayland: Queries org.freedesktop.ScreenSaver via D-Bus.
/// On failure: returns Unknown. Callers must not treat that as "0 ms idle":
/// an idle wait would then never end.
class IdleDetector {
public:
    static constexpr std::chrono::milliseconds Unknown{-1};

    static std::chrono::milliseconds getIdleTime();

    /// User-facing explanation when getIdleTime() returns Unknown.
    static QString unavailableMessage();
};

} // namespace QuadClicker
