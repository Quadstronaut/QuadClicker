#include "InputInjectorFactory.h"

#include <QGuiApplication>
#include <stdexcept>
#include <memory>

namespace QuadClicker {

// Forward declarations from the implementation files
InputInjector* createX11Injector();
InputInjector* createWaylandInjector();

std::unique_ptr<InputInjector> InputInjectorFactory::create()
{
    // QGuiApplication::platformName() returns "xcb" for X11, "wayland" for Wayland
    QString platform = QGuiApplication::platformName();

    if (platform == QLatin1String("wayland")) {
        return std::unique_ptr<InputInjector>(createWaylandInjector());
    }

    // Default: X11 / xcb
    return std::unique_ptr<InputInjector>(createX11Injector());
}

bool InputInjectorFactory::canMoveCursor()
{
    return QGuiApplication::platformName() != QLatin1String("wayland");
}

const char* InputInjectorFactory::fixedLocationUnsupportedMessage()
{
    return "Fixed location is not supported on Wayland (the pointer cannot be moved). "
           "Use the current cursor position.";
}

} // namespace QuadClicker
