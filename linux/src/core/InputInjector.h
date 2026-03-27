#pragma once

#include "../models/MouseButton.h"
#include "../models/ClickType.h"
#include <atomic>

namespace QuadClicker {

/// Abstract base class for platform-specific mouse input injection.
class InputInjector {
public:
    virtual ~InputInjector() = default;

    /// Inject one click (or double-click) using the given button.
    /// \p cancel is polled between the two clicks of a double-click.
    virtual void click(MouseButton button, ClickType clickType,
                       std::atomic<bool>& cancel) = 0;

    /// Move the cursor to absolute screen coordinates.
    virtual void moveCursor(int x, int y) = 0;
};

} // namespace QuadClicker
