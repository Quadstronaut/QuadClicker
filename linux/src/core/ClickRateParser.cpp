#include "ClickRateParser.h"

#include <cmath>

namespace QuadClicker {

bool ClickRateParser::tryParsePositive(const QString& s, double& value)
{
    bool ok = false;
    value = s.trimmed().toDouble(&ok);
    return ok && value > 0.0;
}

bool ClickRateParser::buildDelay(double ms,
                                  std::chrono::milliseconds& delay,
                                  QString& error)
{
    if (!std::isfinite(ms)) {
        error = QStringLiteral("Rate is not a finite number.");
        return false;
    }
    if (ms < MinDelayMs) {
        error = QStringLiteral("Rate exceeds maximum — minimum delay is 1 ms (1000 clicks/sec).");
        return false;
    }
    if (ms > MaxDelayMs) {
        error = QStringLiteral("Delay exceeds maximum of 360 minutes.");
        return false;
    }
    delay = std::chrono::milliseconds(static_cast<long long>(ms));
    error.clear();
    return true;
}

bool ClickRateParser::endsWithAny(const QString& t, std::initializer_list<const char*> suffixes)
{
    for (auto s : suffixes) {
        if (t.endsWith(QLatin1String(s))) return true;
    }
    return false;
}

QString ClickRateParser::stripFirstSuffix(const QString& t, std::initializer_list<const char*> suffixes)
{
    for (auto s : suffixes) {
        QLatin1String suf(s);
        if (t.endsWith(suf)) {
            return t.left(t.size() - suf.size()).trimmed();
        }
    }
    return t.trimmed();
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
        QString num = t.chopped(2).trimmed();
        double ms = 0.0;
        if (tryParsePositive(num, ms)) {
            return buildDelay(ms, delay, error);
        }
        error = QStringLiteral("Millisecond value must be a positive number.");
        return false;
    }

    // ── Minutes: "2m", "2min", "2minutes" ─────────────────────────────────────
    if (endsWithAny(t, {"minutes", "minute", "mins", "min", "m"})
        && !t.contains(QLatin1String("per minute"))
        && !t.endsWith(QLatin1String("/min"))
        && !t.endsWith(QLatin1String("cpm")))
    {
        QString num = stripFirstSuffix(t, {"minutes", "minute", "mins", "min", "m"});
        double mins = 0.0;
        if (tryParsePositive(num, mins)) {
            return buildDelay(mins * 60'000.0, delay, error);
        }
        error = QStringLiteral("Minutes value must be a positive number.");
        return false;
    }

    // ── Seconds: "5s", "5sec", "5seconds" ─────────────────────────────────────
    if (endsWithAny(t, {"seconds", "second", "secs", "sec", "s"})
        && !t.contains(QLatin1String("per second"))
        && !t.endsWith(QLatin1String("/s"))
        && !t.endsWith(QLatin1String("cps")))
    {
        QString num = stripFirstSuffix(t, {"seconds", "second", "secs", "sec", "s"});
        double secs = 0.0;
        if (tryParsePositive(num, secs)) {
            return buildDelay(secs * 1000.0, delay, error);
        }
        error = QStringLiteral("Seconds value must be a positive number.");
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
        double tps = 0.0;
        if (tryParsePositive(num, tps)) {
            return buildDelay(1000.0 / tps, delay, error);
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
        double tpm = 0.0;
        if (tryParsePositive(num, tpm)) {
            return buildDelay(60'000.0 / tpm, delay, error);
        }
        error = QStringLiteral("Clicks-per-minute value must be a positive number.");
        return false;
    }

    // ── Clicks/hour: "60/h", "60cph", "60 times per hour" ─────────────────────
    if (t.endsWith(QLatin1String("/h")) ||
        t.endsWith(QLatin1String("cph")) ||
        t.contains(QLatin1String("times per hour")))
    {
        QString num = t;
        num.replace(QLatin1String("times per hour"), QString())
           .replace(QLatin1String("/h"),             QString())
           .replace(QLatin1String("cph"),            QString())
           .replace(QLatin1String(" "),              QString());
        double tph = 0.0;
        if (tryParsePositive(num, tph)) {
            return buildDelay(3'600'000.0 / tph, delay, error);
        }
        error = QStringLiteral("Clicks-per-hour value must be a positive number.");
        return false;
    }

    // ── Bare integer/decimal → milliseconds ───────────────────────────────────
    double bare = 0.0;
    if (tryParsePositive(t, bare)) {
        return buildDelay(bare, delay, error);
    }

    error = QStringLiteral("Invalid format. Examples: 100ms  |  5s  |  10/s  |  600/min  |  60/h");
    return false;
}

} // namespace QuadClicker
