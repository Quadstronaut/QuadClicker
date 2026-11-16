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
    // The worker captured `this`. Returning before it ends would leave it reading
    // freed atomics and emitting on a destroyed QObject (quit while clicking).
    m_cancel = true;
    m_future.waitForFinished();
}

void ClickEngine::start(const ClickSession& session)
{
    // A previous run may still be winding down after stop(). Join it before the
    // cancel flag is reused: the old check "if running, return" silently dropped
    // a quick Stop->Start, and nothing ever joined the worker.
    m_cancel = true;
    m_future.waitForFinished();

    m_cancel  = false;
    m_running = true;
    const quint64 runId = ++m_runId;

    // Run the loop on a Qt thread pool thread so we don't block the UI event loop.
    m_future = QtConcurrent::run([this, session, runId]() {
        loop(session, runId);
    });
}

void ClickEngine::stop()
{
    m_cancel = true;
}

void ClickEngine::loop(const ClickSession session, quint64 runId)
{
    int clicks = 0;

    auto finish = [this, runId] {
        emit statusChanged(EngineStatus::Stopped);
        m_running = false;
        emit finished(runId);
    };

    // Create the injector on the background thread (X11 display is per-thread)
    std::unique_ptr<InputInjector> injector;
    try {
        injector = InputInjectorFactory::create();
    } catch (const std::exception& ex) {
        // Cannot inject input — stop immediately
        emit errorOccurred(QString::fromUtf8(ex.what()));
        finish();
        return;
    }

    // ── Idle wait (once, before the click loop) ────────────────────────────────
    if (session.idleWaitSeconds > 0.0) {
        emit statusChanged(EngineStatus::WaitingForIdle);

        auto threshold = std::chrono::milliseconds(
            static_cast<long long>(session.idleWaitSeconds * 1000.0));

        while (!m_cancel.load()) {
            auto idle = IdleDetector::getIdleTime();
            if (idle == IdleDetector::Unknown) {
                // Unreadable idle time used to read as 0 ms: "Waiting for idle" forever.
                emit errorOccurred(IdleDetector::unavailableMessage());
                finish();
                return;
            }
            if (idle >= threshold) break;
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        }

        if (m_cancel.load()) {
            finish();
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
        // One period, but never past the stop-after-seconds limit. Sleep in slices
        // of at most 50 ms (was 1 ms) so long gaps do not wake the CPU 1000x/s
        // while Stop still takes effect quickly.
        if (session.clickRate.count() >= 1 && !m_cancel.load()) {
            auto deadline = ClickTiming::nextWake(std::chrono::steady_clock::now(), session.clickRate,
                                                  startTime, session.stopAfterSeconds);
            for (auto now = std::chrono::steady_clock::now();
                 !m_cancel.load() && now < deadline;
                 now = std::chrono::steady_clock::now())
                std::this_thread::sleep_for(ClickTiming::sleepSlice(now, deadline));
        }
    }

    finish();
}

} // namespace QuadClicker
