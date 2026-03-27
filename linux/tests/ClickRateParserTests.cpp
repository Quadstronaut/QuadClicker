#include <QtTest/QtTest>
#include "../src/core/ClickRateParser.h"

using namespace QuadClicker;

class ClickRateParserTests : public QObject {
    Q_OBJECT

private slots:
    // ── Valid millisecond inputs ───────────────────────────────────────────────

    void milliseconds_100ms()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("100ms"), delay, err));
        QCOMPARE(delay.count(), 100LL);
    }

    void milliseconds_100ms_space()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("100 ms"), delay, err));
        QCOMPARE(delay.count(), 100LL);
    }

    void milliseconds_1ms()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("1ms"), delay, err));
        QCOMPARE(delay.count(), 1LL);
    }

    void milliseconds_250ms()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("250ms"), delay, err));
        QCOMPARE(delay.count(), 250LL);
    }

    void milliseconds_1000ms()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("1000ms"), delay, err));
        QCOMPARE(delay.count(), 1000LL);
    }

    void milliseconds_0_5ms_fails()
    {
        // 0.5ms < 1ms minimum — must fail
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(!ClickRateParser::tryParse(QStringLiteral("0.5ms"), delay, err));
        QVERIFY(!err.isEmpty());
    }

    // ── Valid clicks-per-second inputs ────────────────────────────────────────

    void cps_10perS()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("10/s"), delay, err));
        QCOMPARE(delay.count(), 100LL);
    }

    void cps_1perS()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("1/s"), delay, err));
        QCOMPARE(delay.count(), 1000LL);
    }

    void cps_100perS()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("100/s"), delay, err));
        QCOMPARE(delay.count(), 10LL);
    }

    void cps_timesPerSecond()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("10 times per second"), delay, err));
        QCOMPARE(delay.count(), 100LL);
    }

    void cps_cps_suffix()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("10cps"), delay, err));
        QCOMPARE(delay.count(), 100LL);
    }

    // ── Valid clicks-per-minute inputs ────────────────────────────────────────

    void cpm_600perMin()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("600/min"), delay, err));
        QCOMPARE(delay.count(), 100LL);
    }

    void cpm_60perMin()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("60/min"), delay, err));
        QCOMPARE(delay.count(), 1000LL);
    }

    void cpm_timesPerMinute()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("600 times per minute"), delay, err));
        QCOMPARE(delay.count(), 100LL);
    }

    void cpm_cpm_suffix()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("600cpm"), delay, err));
        QCOMPARE(delay.count(), 100LL);
    }

    // ── Bare integer (treated as ms) ──────────────────────────────────────────

    void bare_100()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("100"), delay, err));
        QCOMPARE(delay.count(), 100LL);
    }

    void bare_1()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("1"), delay, err));
        QCOMPARE(delay.count(), 1LL);
    }

    void bare_500()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("500"), delay, err));
        QCOMPARE(delay.count(), 500LL);
    }

    // ── Invalid inputs ────────────────────────────────────────────────────────

    void invalid_empty()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(!ClickRateParser::tryParse(QStringLiteral(""), delay, err));
        QVERIFY(!err.isEmpty());
    }

    void invalid_whitespace()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(!ClickRateParser::tryParse(QStringLiteral("   "), delay, err));
        QVERIFY(!err.isEmpty());
    }

    void invalid_alpha()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(!ClickRateParser::tryParse(QStringLiteral("abc"), delay, err));
        QVERIFY(!err.isEmpty());
    }

    void invalid_0ms()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(!ClickRateParser::tryParse(QStringLiteral("0ms"), delay, err));
        QVERIFY(!err.isEmpty());
    }

    void invalid_negative()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(!ClickRateParser::tryParse(QStringLiteral("-1ms"), delay, err));
        QVERIFY(!err.isEmpty());
    }

    void invalid_zero_cps()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(!ClickRateParser::tryParse(QStringLiteral("0/s"), delay, err));
        QVERIFY(!err.isEmpty());
    }

    void invalid_text_cps()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(!ClickRateParser::tryParse(QStringLiteral("foo/s"), delay, err));
        QVERIFY(!err.isEmpty());
    }

    void exceedMaxRate()
    {
        // 2000/s = 0.5ms delay < 1ms minimum
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(!ClickRateParser::tryParse(QStringLiteral("2000/s"), delay, err));
        QVERIFY(!err.isEmpty());
    }
};

QTEST_MAIN(ClickRateParserTests)
#include "ClickRateParserTests.moc"
