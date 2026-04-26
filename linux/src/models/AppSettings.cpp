#include "AppSettings.h"

#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QJsonDocument>
#include <QJsonObject>
#include <QStandardPaths>

namespace QuadClicker {

static MouseButton mouseButtonFromString(const QString& s)
{
    if (s == QLatin1String("Right"))  return MouseButton::Right;
    if (s == QLatin1String("Middle")) return MouseButton::Middle;
    return MouseButton::Left;
}

static QString mouseButtonToString(MouseButton b)
{
    switch (b) {
    case MouseButton::Right:  return QStringLiteral("Right");
    case MouseButton::Middle: return QStringLiteral("Middle");
    default:                  return QStringLiteral("Left");
    }
}

static ClickType clickTypeFromString(const QString& s)
{
    if (s == QLatin1String("Double")) return ClickType::Double;
    return ClickType::Single;
}

static QString clickTypeToString(ClickType t)
{
    return t == ClickType::Double ? QStringLiteral("Double") : QStringLiteral("Single");
}

QString AppSettings::settingsPath()
{
    // ~/.config/quadclicker/settings.json
    QString configDir = QStandardPaths::writableLocation(QStandardPaths::AppConfigLocation);
    // AppConfigLocation typically gives ~/.config/<AppName>
    // Fall back to manual construction if needed
    if (configDir.isEmpty()) {
        configDir = QDir::homePath() + QLatin1String("/.config/quadclicker");
    }
    return configDir + QLatin1String("/settings.json");
}

AppSettings AppSettings::load()
{
    AppSettings s;
    try {
        QFile f(settingsPath());
        if (!f.open(QIODevice::ReadOnly)) return s;

        QByteArray data = f.readAll();
        f.close();

        QJsonParseError err;
        QJsonDocument doc = QJsonDocument::fromJson(data, &err);
        if (err.error != QJsonParseError::NoError || !doc.isObject()) return s;

        QJsonObject obj = doc.object();

        if (obj.contains(QLatin1String("ClickRateValue")))
            s.clickRateValue = obj[QLatin1String("ClickRateValue")].toString(s.clickRateValue);
        if (obj.contains(QLatin1String("ClickRateUnit")))
            s.clickRateUnit = obj[QLatin1String("ClickRateUnit")].toString(s.clickRateUnit);
        if (obj.contains(QLatin1String("ClickRateMode"))) {
            int v = obj[QLatin1String("ClickRateMode")].toInt(0);
            s.clickRateMode = (v == 1) ? ClickRateMode::Frequency : ClickRateMode::Delay;
        }
        if (obj.contains(QLatin1String("Button")))
            s.button = mouseButtonFromString(obj[QLatin1String("Button")].toString());
        if (obj.contains(QLatin1String("ClickType")))
            s.clickType = clickTypeFromString(obj[QLatin1String("ClickType")].toString());
        if (obj.contains(QLatin1String("UseCurrentPosition")))
            s.useCurrentPosition = obj[QLatin1String("UseCurrentPosition")].toBool(s.useCurrentPosition);
        if (obj.contains(QLatin1String("X")))
            s.x = obj[QLatin1String("X")].toInt(s.x);
        if (obj.contains(QLatin1String("Y")))
            s.y = obj[QLatin1String("Y")].toInt(s.y);
        if (obj.contains(QLatin1String("StopAfterClicks")))
            s.stopAfterClicks = obj[QLatin1String("StopAfterClicks")].toInt(s.stopAfterClicks);
        if (obj.contains(QLatin1String("StopAfterSeconds")))
            s.stopAfterSeconds = obj[QLatin1String("StopAfterSeconds")].toDouble(s.stopAfterSeconds);
        if (obj.contains(QLatin1String("IdleWaitSeconds")))
            s.idleWaitSeconds = obj[QLatin1String("IdleWaitSeconds")].toDouble(s.idleWaitSeconds);
        if (obj.contains(QLatin1String("AlwaysOnTop")))
            s.alwaysOnTop = obj[QLatin1String("AlwaysOnTop")].toBool(s.alwaysOnTop);
        if (obj.contains(QLatin1String("StartHotkeyText")))
            s.startHotkeyText = obj[QLatin1String("StartHotkeyText")].toString(s.startHotkeyText);
        if (obj.contains(QLatin1String("StopHotkeyText")))
            s.stopHotkeyText = obj[QLatin1String("StopHotkeyText")].toString(s.stopHotkeyText);
    } catch (...) {
        // Corrupt or unreadable — fall back to defaults
    }

    // Legacy migration: pre-redesign settings used "/s" and "/min" as unit values
    // with no ClickRateMode field. Translate them to the new canonical shape.
    if (s.clickRateUnit == QLatin1String("/s")) {
        s.clickRateMode = ClickRateMode::Frequency;
        s.clickRateUnit = QStringLiteral("per_sec");
    } else if (s.clickRateUnit == QLatin1String("/min")) {
        s.clickRateMode = ClickRateMode::Frequency;
        s.clickRateUnit = QStringLiteral("per_min");
    } else if (s.clickRateUnit == QLatin1String("ms")) {
        s.clickRateMode = ClickRateMode::Delay;
    }
    return s;
}

void AppSettings::save() const
{
    try {
        QString path = settingsPath();
        QDir dir = QFileInfo(path).dir();
        if (!dir.exists()) dir.mkpath(dir.absolutePath());

        QJsonObject obj;
        obj[QLatin1String("ClickRateMode")]      = static_cast<int>(clickRateMode);
        obj[QLatin1String("ClickRateValue")]     = clickRateValue;
        obj[QLatin1String("ClickRateUnit")]      = clickRateUnit;
        obj[QLatin1String("Button")]             = mouseButtonToString(button);
        obj[QLatin1String("ClickType")]          = clickTypeToString(clickType);
        obj[QLatin1String("UseCurrentPosition")] = useCurrentPosition;
        obj[QLatin1String("X")]                  = x;
        obj[QLatin1String("Y")]                  = y;
        obj[QLatin1String("StopAfterClicks")]    = stopAfterClicks;
        obj[QLatin1String("StopAfterSeconds")]   = stopAfterSeconds;
        obj[QLatin1String("IdleWaitSeconds")]    = idleWaitSeconds;
        obj[QLatin1String("AlwaysOnTop")]        = alwaysOnTop;
        obj[QLatin1String("StartHotkeyText")]    = startHotkeyText;
        obj[QLatin1String("StopHotkeyText")]     = stopHotkeyText;

        QJsonDocument doc(obj);
        QFile f(path);
        if (f.open(QIODevice::WriteOnly | QIODevice::Truncate)) {
            f.write(doc.toJson(QJsonDocument::Indented));
            f.close();
        }
    } catch (...) {
        // Non-fatal — settings loss is recoverable
    }
}

} // namespace QuadClicker
