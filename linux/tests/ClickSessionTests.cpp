#include <QtTest/QtTest>
#include "../src/models/ClickSession.h"
#include "../src/models/MouseButton.h"
#include "../src/models/ClickType.h"

using namespace QuadClicker;

class ClickSessionTests : public QObject {
    Q_OBJECT

private:
    static ClickSession defaultSession()
    {
        return ClickSession(
            std::chrono::milliseconds(100),
            MouseButton::Left,
            ClickType::Single,
            true,  // useCurrentPosition
            0, 0,
            0,     // stopAfterClicks
            0.0,   // stopAfterSeconds
            0.0    // idleWaitSeconds
        );
    }

private slots:
    void defaultSession_isValid()
    {
        auto s = defaultSession();
        QCOMPARE(s.clickRate.count(), 100LL);
        QCOMPARE(s.button,  MouseButton::Left);
        QCOMPARE(s.clickType, ClickType::Single);
        QVERIFY(s.useCurrentPosition);
        QCOMPARE(s.stopAfterClicks, 0);
    }

    void session_copySemantics_areIndependent()
    {
        auto s1 = defaultSession();
        ClickSession s2 = s1;  // copy
        s2.button = MouseButton::Right;

        // s1 must not be affected
        QCOMPARE(s1.button, MouseButton::Left);
        QCOMPARE(s2.button, MouseButton::Right);
    }

    void session_allButtons_storeCorrectly()
    {
        for (auto btn : {MouseButton::Left, MouseButton::Right, MouseButton::Middle}) {
            auto s = defaultSession();
            s.button = btn;
            QCOMPARE(s.button, btn);
        }
    }

    void session_doubleClick_storesCorrectly()
    {
        auto s = defaultSession();
        s.clickType = ClickType::Double;
        QCOMPARE(s.clickType, ClickType::Double);
    }

    void session_fixedPosition_storesCoordinates()
    {
        auto s = defaultSession();
        s.useCurrentPosition = false;
        s.x = 500;
        s.y = 300;

        QVERIFY(!s.useCurrentPosition);
        QCOMPARE(s.x, 500);
        QCOMPARE(s.y, 300);
    }

    void session_stopConditions_storeCorrectly()
    {
        auto s = defaultSession();
        s.stopAfterClicks  = 100;
        s.stopAfterSeconds = 30.0;
        s.idleWaitSeconds  = 5.0;

        QCOMPARE(s.stopAfterClicks,  100);
        QCOMPARE(s.stopAfterSeconds, 30.0);
        QCOMPARE(s.idleWaitSeconds,  5.0);
    }

    void session_allFieldsRoundTrip()
    {
        ClickSession s(
            std::chrono::milliseconds(200),
            MouseButton::Right,
            ClickType::Double,
            false,
            640, 480,
            100,
            30.0,
            5.0);

        QCOMPARE(s.clickRate.count(),   200LL);
        QCOMPARE(s.button,              MouseButton::Right);
        QCOMPARE(s.clickType,           ClickType::Double);
        QVERIFY(!s.useCurrentPosition);
        QCOMPARE(s.x,                   640);
        QCOMPARE(s.y,                   480);
        QCOMPARE(s.stopAfterClicks,     100);
        QCOMPARE(s.stopAfterSeconds,    30.0);
        QCOMPARE(s.idleWaitSeconds,     5.0);
    }
};

QTEST_MAIN(ClickSessionTests)
#include "ClickSessionTests.moc"
