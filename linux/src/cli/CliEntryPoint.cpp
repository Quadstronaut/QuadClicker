#include "CliEntryPoint.h"

#include "../core/ClickEngine.h"
#include "../core/ClickRateParser.h"
#include "../core/ClickTiming.h"
#include "../core/InputInjectorFactory.h"
#include "../core/IdleDetector.h"
#include "../models/ClickSession.h"
#include "../models/MouseButton.h"
#include "../models/ClickType.h"

// QGuiApplication is needed so QGuiApplication::platformName() works in
// InputInjectorFactory. In CLI mode there is no display connection needed
// for the app object itself, but the injector will open its own display.
#include <QGuiApplication>

#include <iostream>
#include <csignal>
#include <atomic>
#include <thread>
#include <chrono>
#include <string>
#include <cstring>
#include <cmath>

namespace QuadClicker {

// Global cancel flag — set by the SIGINT handler
static std::atomic<bool> g_sigintReceived{false};

static void sigintHandler(int)
{
    g_sigintReceived.store(true);
}

// Whole string must be a number (std::stod/stoi alone accept "10abc" and read
// "1,5" as 1). Non-negative and finite: NaN, inf and negatives would make the
// engine's "> 0" checks silently disable the limit.
static bool parseNonNegativeDouble(const std::string& s, double& out)
{
    try {
        size_t used = 0;
        double v = std::stod(s, &used);
        if (used != s.size() || !std::isfinite(v) || v < 0.0) return false;
        out = v;
        return true;
    } catch (...) {
        return false;
    }
}

static bool parseNonNegativeInt(const std::string& s, int& out)
{
    try {
        size_t used = 0;
        int v = std::stoi(s, &used);
        if (used != s.size() || v < 0) return false;
        out = v;
        return true;
    } catch (...) {
        return false;
    }
}

bool CliEntryPoint::parseArgs(int argc, char* argv[],
                              ClickSession& session, std::string& error)
{
    std::chrono::microseconds rate{0};   // full precision, see ClickSession::clickRate
    MouseButton button        = MouseButton::Left;
    ClickType   clickType     = ClickType::Single;
    bool        useCurrentPos = true;
    int         x = 0, y     = 0;
    int         stopClicks    = 0;
    double      stopSeconds   = 0.0;
    double      idleWait      = 0.0;
    bool        hasRate       = false;

    for (int i = 1; i < argc; ++i) {
        std::string arg(argv[i]);

        auto requireNext = [&](std::string& val) -> bool {
            if (i + 1 < argc) { val = argv[++i]; return true; }
            error = "Missing value after '" + arg + "'";
            return false;
        };

        if (arg == "--rate") {
            std::string val;
            if (!requireNext(val)) return false;
            QString qerr;
            if (!ClickRateParser::tryParse(QString::fromStdString(val), rate, qerr)) {
                error = qerr.toStdString();
                return false;
            }
            hasRate = true;
        } else if (arg == "--button") {
            std::string val;
            if (!requireNext(val)) return false;
            if (val == "left")        button = MouseButton::Left;
            else if (val == "right")  button = MouseButton::Right;
            else if (val == "middle") button = MouseButton::Middle;
            else { error = "Unknown button '" + val + "'. Use: left, right, middle."; return false; }
        } else if (arg == "--type") {
            std::string val;
            if (!requireNext(val)) return false;
            std::string lower = QString::fromStdString(val).toLower().toStdString();
            if (lower == "single")      clickType = ClickType::Single;
            else if (lower == "double") clickType = ClickType::Double;
            else { error = "Unknown click type '" + val + "'. Use: single, double."; return false; }
        } else if (arg == "--location") {
            std::string val;
            if (!requireNext(val)) return false;
            auto comma = val.find(',');
            if (comma == std::string::npos) {
                error = "Location must be 'X,Y' (e.g. 500,300).";
                return false;
            }
            try {
                x = std::stoi(val.substr(0, comma));
                y = std::stoi(val.substr(comma + 1));
            } catch (...) {
                error = "Location must be 'X,Y' (e.g. 500,300).";
                return false;
            }
            useCurrentPos = false;
        } else if (arg == "--stop-after-clicks") {
            std::string val;
            if (!requireNext(val)) return false;
            if (!parseNonNegativeInt(val, stopClicks))
            { error = "--stop-after-clicks must be a non-negative integer."; return false; }
        } else if (arg == "--stop-after-seconds") {
            std::string val;
            if (!requireNext(val)) return false;
            if (!parseNonNegativeDouble(val, stopSeconds))
            { error = "--stop-after-seconds must be a non-negative number."; return false; }
        } else if (arg == "--idle-wait") {
            std::string val;
            if (!requireNext(val)) return false;
            if (!parseNonNegativeDouble(val, idleWait))
            { error = "--idle-wait must be a non-negative number."; return false; }
        } else if (arg == "--no-gui" || arg == "--minimized") {
            // Handled at the program entry level
        } else if (arg == "--help" || arg == "-h" || arg == "--version" || arg == "-v") {
            // Handled before this function is called
        } else {
            error = "Unknown argument: '" + arg + "'";
            return false;
        }
    }

    if (!hasRate) { error = "--rate is required in CLI mode."; return false; }

    session = ClickSession(rate, button, clickType, useCurrentPos, x, y,
                           stopClicks, stopSeconds, idleWait);
    return true;
}

static void printHelp()
{
    std::cout <<
        "Usage: quadclicker [OPTIONS]\n"
        "\n"
        "When run without arguments, launches the GUI.\n"
        "\n"
        "Options:\n"
        "  --rate <value>               Click rate. Formats: 100ms | 10/s | 600/min  [required in CLI mode]\n"
        "  --button <left|right|middle> Mouse button to click (default: left)\n"
        "  --type <single|double>       Click type (default: single)\n"
        "  --location <x,y>             Fixed screen coordinate (default: current cursor)\n"
        "  --stop-after-clicks <n>      Stop after N clicks, N >= 0 (0 = unlimited)\n"
        "  --stop-after-seconds <n>     Stop after N seconds, N >= 0 (0 = unlimited)\n"
        "  --idle-wait <n>              Wait for N seconds of system idle before starting (N >= 0)\n"
        "  --no-gui                     Force headless mode\n"
        "  --minimized                  Launch GUI minimized to tray\n"
        "  --version                    Print version and exit 0\n"
        "  --help                       Print this help and exit 0\n"
        "\n"
        "Exit codes:\n"
        "  0   Success / clean stop\n"
        "  1   Invalid argument\n"
        "  2   Runtime error\n"
        "  130 Ctrl+C / interrupted\n"
        "\n"
        "Examples:\n"
        "  quadclicker --rate 10/s\n"
        "  quadclicker --rate 10/s --location 500,300 --button right --stop-after-clicks 100\n"
        "  quadclicker --rate 500ms --type double --stop-after-seconds 30\n"
        "  quadclicker --rate 1ms --button middle --stop-after-clicks 50\n";
}

int CliEntryPoint::run(int argc, char* argv[])
{
    // Handle --help and --version before anything else
    for (int i = 1; i < argc; ++i) {
        std::string arg(argv[i]);
        if (arg == "--help" || arg == "-h") { printHelp(); return 0; }
        if (arg == "--version" || arg == "-v") {
            std::cout << "QuadClicker " QUADCLICKER_VERSION "\n";
            return 0;
        }
    }

    ClickSession session;
    std::string parseError;
    if (!parseArgs(argc, argv, session, parseError)) {
        std::cerr << "Error: " << parseError << "\n";
        std::cerr << "Run 'quadclicker --help' for usage.\n";
        return 1;
    }

    // Print session summary
    std::string buttonStr =
        session.button == MouseButton::Right  ? "Right"  :
        session.button == MouseButton::Middle ? "Middle" : "Left";
    std::string typeStr =
        session.clickType == ClickType::Double ? "Double" : "Single";
    std::string locStr = session.useCurrentPosition
        ? "cursor position"
        : ("(" + std::to_string(session.x) + "," + std::to_string(session.y) + ")");

    std::cout << "QuadClicker | "
              << std::chrono::duration<double, std::milli>(session.clickRate).count() << "ms delay | "
              << buttonStr << " " << typeStr << " click | "
              << locStr << "\n";

    if (session.stopAfterClicks  > 0)
        std::cout << "  Stop after " << session.stopAfterClicks << " clicks\n";
    if (session.stopAfterSeconds > 0.0)
        std::cout << "  Stop after " << session.stopAfterSeconds << "s\n";
    if (session.idleWaitSeconds  > 0.0)
        std::cout << "  Wait for " << session.idleWaitSeconds << "s idle\n";
    std::cout << "Press Ctrl+C to stop.\n" << std::flush;

    // Register SIGINT handler
    std::signal(SIGINT, sigintHandler);
    g_sigintReceived = false;

    // Create a minimal QCoreApplication so Qt's event loop machinery works
    // (QtConcurrent requires it)
    int fakeArgc = 1;
    char appName[] = "quadclicker";
    char* fakeArgv[] = {appName, nullptr};
    QGuiApplication app(fakeArgc, fakeArgv);

    // The platform is only known once the app object exists.
    if (!session.useCurrentPosition && !InputInjectorFactory::canMoveCursor()) {
        std::cerr << "Error: " << InputInjectorFactory::fixedLocationUnsupportedMessage() << "\n";
        return 1;
    }

    // Create injector early to fail fast if the display isn't available
    std::unique_ptr<InputInjector> injector;
    try {
        injector = InputInjectorFactory::create();
    } catch (const std::exception& ex) {
        std::cerr << "Error: " << ex.what() << "\n";
        return 2;
    }

    // Run the click loop directly on this thread (no UI thread needed in CLI mode)
    int clicks = 0;
    int exitCode = 0;

    try {
        // ── Idle wait ──────────────────────────────────────────────────────
        // Ctrl+C here falls through: the click loop below never starts and the
        // run ends with "Stopped" / exit 130 (it used to throw and exit 2).
        if (session.idleWaitSeconds > 0.0) {
            std::cout << "\rWaiting for idle...   " << std::flush;
            auto threshold = std::chrono::milliseconds(
                static_cast<long long>(session.idleWaitSeconds * 1000.0));
            while (!g_sigintReceived.load()) {
                auto idle = IdleDetector::getIdleTime();
                if (idle == IdleDetector::Unknown)   // used to read as 0: wait forever
                    throw std::runtime_error(IdleDetector::unavailableMessage().toStdString());
                if (idle >= threshold) break;
                std::this_thread::sleep_for(std::chrono::milliseconds(100));
            }
        }

        if (!g_sigintReceived.load())
            std::cout << "\rClicking...           " << std::flush;

        // The stop-after-seconds clock starts AFTER the idle wait, like the GUI
        // engine. Starting it before meant --idle-wait 20 --stop-after-seconds 10
        // finished with zero clicks.
        const auto startTime = std::chrono::steady_clock::now();
        auto elapsedSec = [&startTime] {
            return std::chrono::duration<double>(std::chrono::steady_clock::now() - startTime).count();
        };

        while (!g_sigintReceived.load()) {
            // Stop conditions
            if (ClickTiming::limitReached(clicks, session.stopAfterClicks,
                                          elapsedSec(), session.stopAfterSeconds)) break;

            // Position
            if (!session.useCurrentPosition)
                injector->moveCursor(session.x, session.y);

            // Click. Pass the SIGINT flag so Ctrl+C also cancels the double-click
            // gap (a local, never-set flag was passed before).
            injector->click(session.button, session.clickType, g_sigintReceived);
            ++clicks;

            if (clicks % 50 == 0)
                std::cout << "\rClicks: " << clicks << "   " << std::flush;

            // Finish right after the last allowed click, not one full period later.
            if (ClickTiming::limitReached(clicks, session.stopAfterClicks,
                                          elapsedSec(), session.stopAfterSeconds)) break;

            // Delay: one period, but never past the stop-after-seconds limit.
            if (session.clickRate.count() >= 1) {
                auto deadline = ClickTiming::nextWake(std::chrono::steady_clock::now(), session.clickRate,
                                                      startTime, session.stopAfterSeconds);
                // Sleep in slices of at most 50 ms (was 1 ms): Ctrl+C is still
                // noticed quickly, but a long gap no longer wakes the CPU 1000x/s.
                for (auto now = std::chrono::steady_clock::now();
                     !g_sigintReceived.load() && now < deadline;
                     now = std::chrono::steady_clock::now())
                    std::this_thread::sleep_for(ClickTiming::sleepSlice(now, deadline));
            }
        }

        if (g_sigintReceived.load()) {
            std::cout << "\nStopped. Total clicks: " << clicks << "\n";
            exitCode = 130;
        } else {
            std::cout << "\nDone. Total clicks: " << clicks << "\n";
            exitCode = 0;
        }
    } catch (const std::exception& ex) {
        std::cerr << "\nRuntime error: " << ex.what() << "\n";
        exitCode = 2;
    }

    return exitCode;
}

} // namespace QuadClicker
