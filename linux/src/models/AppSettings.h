#pragma once

#include "MouseButton.h"
#include "ClickType.h"
#include "ClickRateMode.h"
#include <QString>

namespace QuadClicker {

/// User preferences — persisted to ~/.config/quadclicker/settings.json.
/// JSON keys match the Windows implementation for cross-platform compatibility.
class AppSettings {
public:
    ClickRateMode clickRateMode{ClickRateMode::Delay};
    QString     clickRateValue{QStringLiteral("100")};
    QString     clickRateUnit{QStringLiteral("ms")};
    MouseButton button{MouseButton::Left};
    ClickType   clickType{ClickType::Single};
    bool        useCurrentPosition{true};
    int         x{0};
    int         y{0};
    int         stopAfterClicks{0};
    double      stopAfterSeconds{0.0};
    double      idleWaitSeconds{0.0};
    bool        alwaysOnTop{false};
    QString     startHotkeyText;
    QString     stopHotkeyText{QStringLiteral("F10")};

    static AppSettings load();
    void save() const;

private:
    static QString settingsPath();
};

} // namespace QuadClicker
