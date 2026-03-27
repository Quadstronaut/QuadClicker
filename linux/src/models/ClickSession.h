#pragma once

#include "MouseButton.h"
#include "ClickType.h"
#include <chrono>

namespace QuadClicker {

/// Immutable configuration for a single clicking session.
struct ClickSession {
    std::chrono::milliseconds clickRate{100};   ///< Delay between clicks
    MouseButton               button{MouseButton::Left};
    ClickType                 clickType{ClickType::Single};
    bool                      useCurrentPosition{true};
    int                       x{0};
    int                       y{0};
    int                       stopAfterClicks{0};    ///< 0 = unlimited
    double                    stopAfterSeconds{0.0}; ///< 0 = unlimited
    double                    idleWaitSeconds{0.0};  ///< 0 = disabled

    ClickSession() = default;

    ClickSession(std::chrono::milliseconds clickRate_,
                 MouseButton               button_,
                 ClickType                 clickType_,
                 bool                      useCurrentPosition_,
                 int                       x_,
                 int                       y_,
                 int                       stopAfterClicks_,
                 double                    stopAfterSeconds_,
                 double                    idleWaitSeconds_)
        : clickRate(clickRate_)
        , button(button_)
        , clickType(clickType_)
        , useCurrentPosition(useCurrentPosition_)
        , x(x_)
        , y(y_)
        , stopAfterClicks(stopAfterClicks_)
        , stopAfterSeconds(stopAfterSeconds_)
        , idleWaitSeconds(idleWaitSeconds_)
    {}
};

} // namespace QuadClicker
