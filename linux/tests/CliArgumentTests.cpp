#include <QtTest/QtTest>
#include "../src/core/ClickRateParser.h"
#include "../src/models/ClickSession.h"
#include "../src/models/MouseButton.h"
#include "../src/models/ClickType.h"

using namespace QuadClicker;

/// Tests for CLI argument parsing logic.
/// These exercise the same public parsing surface that the CLI uses
/// (ClickRateParser::tryParse + ClickSession construction).
class CliArgumentTests : public QObject {
    Q_OBJECT

private slots:
    // ── Rate format round-trips ────────────────────────────────────────────────

    void rate_100ms_parsesCorrectly()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("100ms"), delay, err));
        QCOMPARE(delay.count(), 100LL);
    }

    void rate_10perS_parsesCorrectly()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("10/s"), delay, err));
        QCOMPARE(delay.count(), 100LL);
    }

    void rate_600perMin_parsesCorrectly()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(ClickRateParser::tryParse(QStringLiteral("600/min"), delay, err));
        QCOMPARE(delay.count(), 100LL);
    }

    void rate_missing_returnsError()
    {
        std::chrono::milliseconds delay;
        QString err;
        QVERIFY(!ClickRateParser::tryParse(QStringLiteral(""), delay, err));
        QVERIFY(!err.isEmpty());
    }

    // ── Session field round-trips ──────────────────────────────────────────────

    void clickSession_allFields_roundTrip()
    {
        ClickSession session(
            std::chrono::milliseconds(200),
            MouseButton::Right,
            ClickType::Double,
            false,
            640, 480,
            100,
            30.0,
            5.0);

        QCOMPARE(session.clickRate.count(),   200LL);
        QCOMPARE(session.button,              MouseButton::Right);
        QCOMPARE(session.clickType,           ClickType::Double);
        QVERIFY(!session.useCurrentPosition);
        QCOMPARE(session.x,                   640);
        QCOMPARE(session.y,                   480);
        QCOMPARE(session.stopAfterClicks,     100);
        QCOMPARE(session.stopAfterSeconds,    30.0);
        QCOMPARE(session.idleWaitSeconds,     5.0);
    }

    // ── Mouse button enum coverage ────────────────────────────────────────────

    void mouseButton_left_valid()
    {
        ClickSession s(std::chrono::milliseconds(100), MouseButton::Left,
                       ClickType::Single, true, 0, 0, 0, 0.0, 0.0);
        QCOMPARE(s.button, MouseButton::Left);
    }

    void mouseButton_right_valid()
    {
        ClickSession s(std::chrono::milliseconds(100), MouseButton::Right,
                       ClickType::Single, true, 0, 0, 0, 0.0, 0.0);
        QCOMPARE(s.button, MouseButton::Right);
    }

    void mouseButton_middle_valid()
    {
        ClickSession s(std::chrono::milliseconds(100), MouseButton::Middle,
                       ClickType::Single, true, 0, 0, 0, 0.0, 0.0);
        QCOMPARE(s.button, MouseButton::Middle);
    }

    // ── Stop condition defaults ────────────────────────────────────────────────

    void stopConditions_defaultUnlimited()
    {
        ClickSession s(std::chrono::milliseconds(100), MouseButton::Left,
                       ClickType::Single, true, 0, 0, 0, 0.0, 0.0);
        QCOMPARE(s.stopAfterClicks,  0);
        QCOMPARE(s.stopAfterSeconds, 0.0);
        QCOMPARE(s.idleWaitSeconds,  0.0);
    }

    void stopConditions_nonZero_storeCorrectly()
    {
        ClickSession s(std::chrono::milliseconds(100), MouseButton::Left,
                       ClickType::Single, true, 0, 0, 50, 10.0, 3.0);
        QCOMPARE(s.stopAfterClicks,  50);
        QCOMPARE(s.stopAfterSeconds, 10.0);
        QCOMPARE(s.idleWaitSeconds,  3.0);
    }

    // ── Click type ────────────────────────────────────────────────────────────

    void clickType_single_isDefault()
    {
        ClickSession s(std::chrono::milliseconds(100), MouseButton::Left,
                       ClickType::Single, true, 0, 0, 0, 0.0, 0.0);
        QCOMPARE(s.clickType, ClickType::Single);
    }

    void clickType_double_storesCorrectly()
    {
        ClickSession s(std::chrono::milliseconds(100), MouseButton::Left,
                       ClickType::Double, true, 0, 0, 0, 0.0, 0.0);
        QCOMPARE(s.clickType, ClickType::Double);
    }
};

QTEST_MAIN(CliArgumentTests)
#include "CliArgumentTests.moc"
