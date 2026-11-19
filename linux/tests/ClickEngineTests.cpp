#include <QtTest/QtTest>
#include <QGuiApplication>
#include "../src/core/ClickEngine.h"
#include "../src/models/ClickSession.h"

#include <atomic>
#include <chrono>

using namespace QuadClicker;

/// Runs the REAL engine with the REAL X11 injector (XTest) against an X server.
/// CI provides one with Xvfb, so no real desktop is clicked. Skipped when the
/// Qt platform is not xcb (e.g. a developer machine on Wayland or offscreen).
/// Covers QCLK-7 F7: restart joins the old worker, stale finished ids, destroy while running.
class ClickEngineTests : public QObject {
    Q_OBJECT

    static ClickSession fast(int stopAfterClicks = 0)
    {
        return ClickSession(std::chrono::milliseconds(5), MouseButton::Left, ClickType::Single,
                            true, 0, 0, stopAfterClicks, 0.0, 0.0);
    }

    bool x11() const { return QGuiApplication::platformName() == QLatin1String("xcb"); }

private slots:
    void init()
    {
        if (!x11()) QSKIP("needs the xcb platform (run under Xvfb with QT_QPA_PLATFORM=xcb)");
    }

    void stopAfterClicks_deliversThatManyClicksThenFinishes()
    {
        ClickEngine engine;
        std::atomic<int> count{0};
        std::atomic<quint64> finishedId{0};
        QObject::connect(&engine, &ClickEngine::clickCountUpdated, &engine,
                         [&](int c) { count = c; }, Qt::DirectConnection);
        QObject::connect(&engine, &ClickEngine::finished, &engine,
                         [&](quint64 id) { finishedId = id; }, Qt::DirectConnection);

        engine.start(fast(5));
        QTRY_COMPARE_WITH_TIMEOUT(finishedId.load(), engine.currentRunId(), 5000);
        QCOMPARE(count.load(), 5);
        QVERIFY(!engine.isRunning());
    }

    void quickStopThenStart_runsTheNewSession_andNeverDropsIt()
    {
        ClickEngine engine;
        std::atomic<int> count{0};
        QObject::connect(&engine, &ClickEngine::clickCountUpdated, &engine,
                         [&](int c) { count = c; }, Qt::DirectConnection);

        engine.start(fast());
        QTRY_VERIFY_WITH_TIMEOUT(count.load() > 2, 5000);
        const quint64 first = engine.currentRunId();

        engine.stop();
        engine.start(fast(3));              // immediate restart while the old worker winds down
        QVERIFY(engine.currentRunId() > first);

        QTRY_VERIFY_WITH_TIMEOUT(!engine.isRunning(), 5000);
        QCOMPARE(count.load(), 3);          // the new run's own count, not the old run's
    }

    void repeatedRestarts_neverOverlapWorkers()
    {
        ClickEngine engine;
        for (int i = 0; i < 20; ++i) {
            engine.start(fast());
            QTest::qWait(3);
            engine.stop();
        }
        engine.start(fast(2));
        QTRY_VERIFY_WITH_TIMEOUT(!engine.isRunning(), 5000);
    }

    void destroyWhileClicking_joinsTheWorker()
    {
        auto* engine = new ClickEngine;
        std::atomic<int> count{0};
        QObject::connect(engine, &ClickEngine::clickCountUpdated, engine,
                         [&](int c) { count = c; }, Qt::DirectConnection);
        engine->start(fast());
        QTRY_VERIFY_WITH_TIMEOUT(count.load() > 0, 5000);
        delete engine;                      // crashed or hung before F7
        QVERIFY(true);
    }
};

QTEST_MAIN(ClickEngineTests)
#include "ClickEngineTests.moc"
