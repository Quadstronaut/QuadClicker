#include "ClickEngine.h"
#include "ClickTiming.h"
#include "IdleDetector.h"
#include "InputInjectorFactory.h"

#include <QThread>
#include <QtConcurrent/QtConcurrent>
#include <chrono>
#include <thread>

namespace QuadClicker {

ClickEngine::ClickEngine(QObject* parent)
    : QObject(parent)
{
    qRegisterMetaType<QuadClicker::EngineStatus>("QuadClicker::EngineStatus");
}

ClickEngine::~ClickEngine()
{
    stop();
}

void ClickEngine::start(const ClickSession& session)
{
    if (m_running.load()) return;

    m_cancel  = false;
    m_running = true;

    // Run the loop on a Qt thread pool thread (QtConcurrent::run)
    // so we don't block the UI event loop.
    QtConcurrent::run([this, session]() {
        loop(session);
    });
}

void ClickEngine::stop()
{
    m_cancel = true;
}

void ClickEngine::loop(const ClickSession session)
{
    int clicks = 0;

    // Create the injector on the background thread (X11 display is per-thread)
    std::unique_ptr<InputInjector> injector;
    try {
        injector = InputInjectorFactory::create();
    } catch (const std::exception& ex) {
        // Cannot inject input — stop immediately
        emit statusChanged(EngineStatus::Stopped);
        m_running = false;
        emit finished();
        return;
    }

    // ── Idle wait (once, before the click loop) ────────────────────────────────
    if (session.idleWaitSeconds > 0.0) {
        emit statusChanged(EngineStatus::WaitingForIdle);

        auto threshold = std::chrono::milliseconds(
            static_cast<long long>(session.idleWaitSeconds * 1000.0));

        while (!m_cancel.load()) {
            if (IdleDetector::getIdleTime() >= threshold) break;
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        }

        if (m_cancel.load()) {
            emit statusChanged(EngineStatus::Stopped);
            m_running = false;
            emit finished();
            return;
        }
    }

    emit statusChanged(EngineStatus::Clicking);

    const auto startTime = std::chrono::steady_clock::now();
    auto elapsedSec = [&startTime] {
        return std::chrono::duration<double>(std::chrono::steady_clock::now() - startTime).count();
    };

    while (!m_cancel.load()) {
        // ── Stop conditions ────────────────────────────────────────────────────
        if (ClickTiming::limitReached(clicks, session.stopAfterClicks,
                                      elapsedSec(), session.stopAfterSeconds)) break;

        // ── Position ──────────────────────────────────────────────────────────
        if (!session.useCurrentPosition) {
            injector->moveCursor(session.x, session.y);
        }

        // ── Click ─────────────────────────────────────────────────────────────
        injector->click(session.button, session.clickType, m_cancel);
        emit clickCountUpdated(++clicks);

        // Finish right after the last allowed click, not one full period later.
        if (ClickTiming::limitReached(clicks, session.stopAfterClicks,
                                      elapsedSec(), session.stopAfterSeconds)) break;

        // ── Delay ─────────────────────────────────────────────────────────────
        // One period, but never past the stop-after-seconds limit.
        if (session.clickRate.count() >= 1 && !m_cancel.load()) {
            auto deadline = ClickTiming::nextWake(std::chrono::steady_clock::now(), session.clickRate,
                                                  startTime, session.stopAfterSeconds);
            while (!m_cancel.load() && std::chrono::steady_clock::now() < deadline)
                std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
    }

    emit statusChanged(EngineStatus::Stopped);
    m_running = false;
    emit finished();
}

} // namespace QuadClicker
