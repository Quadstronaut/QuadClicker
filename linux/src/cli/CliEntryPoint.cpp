#include "CliEntryPoint.h"

#include "../core/ClickEngine.h"
#include "../core/ClickRateParser.h"
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

namespace QuadClicker {

// Global cancel flag — set by the SIGINT handler
static std::atomic<bool> g_sigintReceived{false};

static void sigintHandler(int)
{
    g_sigintReceived.store(true);
}

static bool tryParseArgs(int argc, char* argv[],
                          ClickSession& session, std::string& error)
{
    std::chrono::milliseconds rate{0};
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
            clickType = (val == "double") ? ClickType::Double : ClickType::Single;
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
            try { stopClicks = std::stoi(val); }
            catch (...) { error = "--stop-after-clicks must be an integer."; return false; }
        } else if (arg == "--stop-after-seconds") {
            std::string val;
            if (!requireNext(val)) return false;
            try { stopSeconds = std::stod(val); }
            catch (...) { error = "--stop-after-seconds must be a number."; return false; }
        } else if (arg == "--idle-wait") {
            std::string val;
            if (!requireNext(val)) return false;
            try { idleWait = std::stod(val); }
            catch (...) { error = "--idle-wait must be a number."; return false; }
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
        "  --stop-after-clicks <n>      Stop after N clicks (0 = unlimited)\n"
        "  --stop-after-seconds <n>     Stop after N seconds (0 = unlimited)\n"
        "  --idle-wait <n>              Wait for N seconds of system idle before starting\n"
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
            std::cout << "QuadClicker 1.0.0\n";
            return 0;
        }
    }

    ClickSession session;
    std::string parseError;
    if (!tryParseArgs(argc, argv, session, parseError)) {
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
              << session.clickRate.count() << "ms delay | "
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

    auto startTime = std::chrono::steady_clock::now();

    try {
        // ── Idle wait ──────────────────────────────────────────────────────
        if (session.idleWaitSeconds > 0.0) {
            std::cout << "\rWaiting for idle...   " << std::flush;
            auto threshold = std::chrono::milliseconds(
                static_cast<long long>(session.idleWaitSeconds * 1000.0));
            while (!g_sigintReceived.load()) {
                if (IdleDetector::getIdleTime() >= threshold) break;
                std::this_thread::sleep_for(std::chrono::milliseconds(100));
            }
            if (g_sigintReceived.load()) throw std::runtime_error("interrupted");
        }

        std::cout << "\rClicking...           " << std::flush;

        while (!g_sigintReceived.load()) {
            // Stop conditions
            if (session.stopAfterClicks > 0 && clicks >= session.stopAfterClicks) break;
            if (session.stopAfterSeconds > 0.0) {
                auto elapsed = std::chrono::steady_clock::now() - startTime;
                if (std::chrono::duration<double>(elapsed).count() >= session.stopAfterSeconds)
                    break;
            }

            // Position
            if (!session.useCurrentPosition)
                injector->moveCursor(session.x, session.y);

            // Click
            std::atomic<bool> cancel{false};
            // Wire g_sigintReceived into cancel for double-click inter-click delay
            injector->click(session.button, session.clickType, cancel);
            ++clicks;

            if (clicks % 50 == 0)
                std::cout << "\rClicks: " << clicks << "   " << std::flush;

            // Delay
            if (session.clickRate.count() >= 1) {
                auto deadline = std::chrono::steady_clock::now() + session.clickRate;
                while (!g_sigintReceived.load() && std::chrono::steady_clock::now() < deadline)
                    std::this_thread::sleep_for(std::chrono::milliseconds(1));
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
