#pragma once

#include "InputInjector.h"
#include <memory>

namespace QuadClicker {

/// Creates the correct InputInjector implementation at runtime.
/// Detects whether we are running under Wayland or X11 and returns
/// the appropriate implementation.
///
/// Throws std::runtime_error if neither backend can be initialised.
class InputInjectorFactory {
public:
    /// Returns a heap-allocated InputInjector for the current display server.
    static std::unique_ptr<InputInjector> create();
};

} // namespace QuadClicker
