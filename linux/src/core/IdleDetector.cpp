#include "IdleDetector.h"

#include <QGuiApplication>

// X11 headers (only used in the X11 code path)
#ifdef QT_DBUS_LIB
#include <QDBusInterface>
#include <QDBusReply>
#endif

#include <X11/Xlib.h>
#include <X11/extensions/scrnsaver.h>

namespace QuadClicker {

static std::chrono::milliseconds getIdleTimeX11()
{
    Display* display = XOpenDisplay(nullptr);
    if (!display) return std::chrono::milliseconds(0);

    XScreenSaverInfo* info = XScreenSaverAllocInfo();
    if (!info) {
        XCloseDisplay(display);
        return std::chrono::milliseconds(0);
    }

    Window root = DefaultRootWindow(display);
    Status status = XScreenSaverQueryInfo(display, root, info);

    std::chrono::milliseconds result(0);
    if (status) {
        result = std::chrono::milliseconds(static_cast<long long>(info->idle));
    }

    XFree(info);
    XCloseDisplay(display);
    return result;
}

#ifdef QT_DBUS_LIB
static std::chrono::milliseconds getIdleTimeWayland()
{
    // org.freedesktop.ScreenSaver GetSessionIdleTime returns idle time in seconds
    QDBusInterface iface(
        QStringLiteral("org.freedesktop.ScreenSaver"),
        QStringLiteral("/org/freedesktop/ScreenSaver"),
        QStringLiteral("org.freedesktop.ScreenSaver"),
        QDBusConnection::sessionBus());

    if (!iface.isValid()) {
        // Try the KDE variant
        QDBusInterface kdeIface(
            QStringLiteral("org.kde.screensaver"),
            QStringLiteral("/ScreenSaver"),
            QStringLiteral("org.freedesktop.ScreenSaver"),
            QDBusConnection::sessionBus());

        if (kdeIface.isValid()) {
            QDBusReply<uint> reply = kdeIface.call(QStringLiteral("GetSessionIdleTime"));
            if (reply.isValid())
                return std::chrono::milliseconds(
                    static_cast<long long>(reply.value()) * 1000LL);
        }
        return std::chrono::milliseconds(0);
    }

    QDBusReply<uint> reply = iface.call(QStringLiteral("GetSessionIdleTime"));
    if (reply.isValid())
        return std::chrono::milliseconds(
            static_cast<long long>(reply.value()) * 1000LL);

    return std::chrono::milliseconds(0);
}
#endif // QT_DBUS_LIB

std::chrono::milliseconds IdleDetector::getIdleTime()
{
    QString platform = QGuiApplication::platformName();

#ifdef QT_DBUS_LIB
    if (platform == QLatin1String("wayland")) {
        return getIdleTimeWayland();
    }
#endif

    // Default: X11
    return getIdleTimeX11();
}

} // namespace QuadClicker
