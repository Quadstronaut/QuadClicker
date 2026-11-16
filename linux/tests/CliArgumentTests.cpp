#include <QtTest/QtTest>
#include "../src/core/ClickRateParser.h"
#include "../src/models/ClickSession.h"
#include "../src/models/MouseButton.h"
#include "../src/models/ClickType.h"
#include "../src/cli/CliEntryPoint.h"

#include <string>
#include <vector>

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

        QCOMPARE(std::chrono::duration_cast<std::chrono::milliseconds>(session.clickRate).count(),   200LL);
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

    // ── CliEntryPoint::parseArgs validation (F18) ─────────────────────────────

    void parseArgs_rejectsBadLimits_data()
    {
        QTest::addColumn<QString>("flag");
        QTest::addColumn<QString>("value");
        QTest::newRow("negative clicks")   << "--stop-after-clicks"  << "-1";
        QTest::newRow("clicks trailing")   << "--stop-after-clicks"  << "10abc";
        QTest::newRow("negative seconds")  << "--stop-after-seconds" << "-10";
        QTest::newRow("nan seconds")       << "--stop-after-seconds" << "nan";
        QTest::newRow("inf seconds")       << "--stop-after-seconds" << "inf";
        QTest::newRow("comma seconds")     << "--stop-after-seconds" << "1,5";
        QTest::newRow("negative idle")     << "--idle-wait"          << "-5";
        QTest::newRow("nan idle")          << "--idle-wait"          << "NaN";
    }

    void parseArgs_rejectsBadLimits()
    {
        QFETCH(QString, flag);
        QFETCH(QString, value);
        Args a({"quadclicker", "--rate", "100ms", flag.toStdString(), value.toStdString()});
        ClickSession s;
        std::string err;
        QVERIFY(!CliEntryPoint::parseArgs(a.argc(), a.argv(), s, err));
        QVERIFY(err.find("non-negative") != std::string::npos);
    }

    void parseArgs_acceptsValidLimits()
    {
        Args a({"quadclicker", "--rate", "100ms", "--stop-after-clicks", "0",
                "--stop-after-seconds", "1.5", "--idle-wait", "0"});
        ClickSession s;
        std::string err;
        QVERIFY(CliEntryPoint::parseArgs(a.argc(), a.argv(), s, err));
        QCOMPARE(s.stopAfterClicks, 0);
        QCOMPARE(s.stopAfterSeconds, 1.5);
        QCOMPARE(s.idleWaitSeconds, 0.0);
    }

    void parseArgs_type_isCaseInsensitive()
    {
        for (const char* t : {"double", "Double", "DOUBLE"}) {
            Args a({"quadclicker", "--rate", "100ms", "--type", t});
            ClickSession s;
            std::string err;
            QVERIFY(CliEntryPoint::parseArgs(a.argc(), a.argv(), s, err));
            QCOMPARE(s.clickType, ClickType::Double);
        }
    }

    void parseArgs_type_unknownRejected()
    {
        for (const char* t : {"dbl", "doubleclick", "triple"}) {
            Args a({"quadclicker", "--rate", "100ms", "--type", t});
            ClickSession s;
            std::string err;
            QVERIFY(!CliEntryPoint::parseArgs(a.argc(), a.argv(), s, err));
            QVERIFY(err.find("single, double") != std::string::npos);
        }
    }

private:
    /// Owns argv storage for parseArgs.
    struct Args {
        std::vector<std::string> store;
        std::vector<char*>       ptrs;
        explicit Args(std::initializer_list<std::string> items) : store(items)
        {
            for (auto& s : store) ptrs.push_back(s.data());
            ptrs.push_back(nullptr);
        }
        int    argc() { return static_cast<int>(store.size()); }
        char** argv() { return ptrs.data(); }
    };
};

QTEST_MAIN(CliArgumentTests)
#include "CliArgumentTests.moc"
