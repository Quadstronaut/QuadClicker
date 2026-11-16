#include <QtTest/QtTest>
#include "../src/core/ClickTiming.h"

using namespace QuadClicker;
using namespace std::chrono_literals;
using Clock = ClickTiming::Clock;

/// Pure timing rules shared by the GUI engine and the CLI loop. No sleeping, no input.
class ClickTimingTests : public QObject {
    Q_OBJECT

private slots:
    // ── Double-click gap (F2) ─────────────────────────────────────────────────

    void doubleClickGap_isAFractionOfTheSystemWindow()
    {
        QCOMPARE(ClickTiming::doubleClickGapMs(500), 50);  // X11 fallback: was a 500 ms wait
        QCOMPARE(ClickTiming::doubleClickGapMs(400), 50);  // GTK/Qt default
        QCOMPARE(ClickTiming::doubleClickGapMs(100), 25);
        QCOMPARE(ClickTiming::doubleClickGapMs(2),    1);  // never 0
        QCOMPARE(ClickTiming::doubleClickGapMs(0),    1);
    }

    // ── Stop conditions (F8) ──────────────────────────────────────────────────

    void limitReached_clickCount_trueRightAfterLastClick()
    {
        QVERIFY(!ClickTiming::limitReached(0, 1, 0.0, 0.0));
        QVERIFY( ClickTiming::limitReached(1, 1, 0.0, 0.0));
    }

    void limitReached_seconds()
    {
        QVERIFY(!ClickTiming::limitReached(5, 0, 0.999, 1.0));
        QVERIFY( ClickTiming::limitReached(5, 0, 1.0,   1.0));
    }

    void limitReached_zeroMeansUnlimited()
    {
        QVERIFY(!ClickTiming::limitReached(1'000'000, 0, 1e9, 0.0));
    }

    void nextWake_cappedAtStopLimit()
    {
        // --rate 5min --stop-after-seconds 10, first click 1 ms after start:
        // wake at the 10 s limit, not 5 minutes later.
        auto start = Clock::time_point{} + 1h;
        auto now   = start + 1ms;
        auto wake  = ClickTiming::nextWake(now, 5min, start, 10.0);
        QVERIFY(wake == start + 10s);
    }

    void nextWake_noLimit_isOnePeriod()
    {
        auto start = Clock::time_point{} + 1h;
        auto now   = start + 3s;
        QVERIFY(ClickTiming::nextWake(now, 250ms, start, 0.0) == now + 250ms);
        QVERIFY(ClickTiming::nextWake(now, 250ms, start, 60.0) == now + 250ms); // limit far away
    }

    void nextWake_limitPassed_isNow()
    {
        auto start = Clock::time_point{} + 1h;
        auto now   = start + 20s;
        QVERIFY(ClickTiming::nextWake(now, 1s, start, 10.0) == now);
    }

    void nextWake_hugeLimit_doesNotOverflow()
    {
        auto start = Clock::time_point{} + 1h;
        auto now   = start + 1s;
        QVERIFY(ClickTiming::nextWake(now, 100ms, start, 1e300) == now + 100ms);
    }

    void nextWake_keepsSubMillisecondPeriod()
    {
        // 600/s: 1666 us must survive into the wake time (F23).
        auto start = Clock::time_point{} + 1h;
        QVERIFY(ClickTiming::nextWake(start, 1667us, start, 0.0) == start + 1667us);
    }
};

QTEST_MAIN(ClickTimingTests)
#include "ClickTimingTests.moc"
