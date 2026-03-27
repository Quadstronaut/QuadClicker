#include "ClickEngine.h"
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

    auto startTime = std::chrono::steady_clock::now();

    while (!m_cancel.load()) {
        // ── Stop conditions ────────────────────────────────────────────────────
        if (session.stopAfterClicks > 0 && clicks >= session.stopAfterClicks) break;

        if (session.stopAfterSeconds > 0.0) {
            auto elapsed = std::chrono::steady_clock::now() - startTime;
            auto elapsedSec = std::chrono::duration<double>(elapsed).count();
            if (elapsedSec >= session.stopAfterSeconds) break;
        }

        // ── Position ──────────────────────────────────────────────────────────
        if (!session.useCurrentPosition) {
            injector->moveCursor(session.x, session.y);
        }

        // ── Click ─────────────────────────────────────────────────────────────
        injector->click(session.button, session.clickType, m_cancel);
        emit clickCountUpdated(++clicks);

        // ── Delay ─────────────────────────────────────────────────────────────
        if (session.clickRate.count() >= 1 && !m_cancel.load()) {
            auto deadline = std::chrono::steady_clock::now() + session.clickRate;
            while (!m_cancel.load() && std::chrono::steady_clock::now() < deadline)
                std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
    }

    emit statusChanged(EngineStatus::Stopped);
    m_running = false;
    emit finished();
}

} // namespace QuadClicker
