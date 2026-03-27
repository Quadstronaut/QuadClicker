#include "ClickRateParser.h"

namespace QuadClicker {

bool ClickRateParser::tryParsePositive(const QString& s, double& value)
{
    bool ok = false;
    value = s.toDouble(&ok);
    return ok && value > 0.0;
}

bool ClickRateParser::tryParse(const QString& text,
                                std::chrono::milliseconds& delay,
                                QString& error)
{
    delay = std::chrono::milliseconds(100);
    error.clear();

    QString t = text.trimmed().toLower();

    if (t.isEmpty()) {
        error = QStringLiteral("Click rate is required.");
        return false;
    }

    // ── Milliseconds: "100ms" ──────────────────────────────────────────────────
    if (t.endsWith(QLatin1String("ms"))) {
        QString num = t.chopped(2).trimmed(); // remove trailing "ms"
        double ms = 0.0;
        if (tryParsePositive(num, ms) && ms >= 1.0) {
            delay = std::chrono::milliseconds(static_cast<long long>(ms));
            return true;
        }
        error = QStringLiteral("Millisecond value must be a number \u2265 1.");
        return false;
    }

    // ── Clicks/second: "10/s", "10cps", "10 times per second" ─────────────────
    if (t.endsWith(QLatin1String("/s")) ||
        t.endsWith(QLatin1String("cps")) ||
        t.contains(QLatin1String("times per second")))
    {
        QString num = t;
        num.replace(QLatin1String("times per second"), QString())
           .replace(QLatin1String("/s"),               QString())
           .replace(QLatin1String("cps"),              QString())
           .replace(QLatin1String(" "),                QString());
        num = num.trimmed();

        double tps = 0.0;
        if (tryParsePositive(num, tps)) {
            double ms = 1000.0 / tps;
            if (ms < 1.0) {
                error = QStringLiteral("Rate exceeds maximum \u2014 minimum delay is 1ms.");
                return false;
            }
            delay = std::chrono::milliseconds(static_cast<long long>(ms));
            return true;
        }
        error = QStringLiteral("Clicks-per-second value must be a positive number.");
        return false;
    }

    // ── Clicks/minute: "600/min", "600cpm", "600 times per minute" ────────────
    if (t.endsWith(QLatin1String("/min")) ||
        t.endsWith(QLatin1String("cpm")) ||
        t.contains(QLatin1String("times per minute")))
    {
        QString num = t;
        num.replace(QLatin1String("times per minute"), QString())
           .replace(QLatin1String("/min"),             QString())
           .replace(QLatin1String("cpm"),              QString())
           .replace(QLatin1String(" "),                QString());
        num = num.trimmed();

        double tpm = 0.0;
        if (tryParsePositive(num, tpm)) {
            double ms = 60000.0 / tpm;
            if (ms < 1.0) {
                error = QStringLiteral("Rate exceeds maximum \u2014 minimum delay is 1ms.");
                return false;
            }
            delay = std::chrono::milliseconds(static_cast<long long>(ms));
            return true;
        }
        error = QStringLiteral("Clicks-per-minute value must be a positive number.");
        return false;
    }

    // ── Bare integer/decimal → milliseconds ───────────────────────────────────
    double bare = 0.0;
    if (tryParsePositive(t, bare) && bare >= 1.0) {
        delay = std::chrono::milliseconds(static_cast<long long>(bare));
        return true;
    }

    error = QStringLiteral("Invalid format. Examples: 100ms  |  10/s  |  600/min");
    return false;
}

} // namespace QuadClicker
