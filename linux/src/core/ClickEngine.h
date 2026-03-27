#pragma once

#include "../models/ClickSession.h"
#include <QObject>
#include <atomic>
#include <functional>

namespace QuadClicker {

enum class EngineStatus {
    Stopped,
    WaitingForIdle,
    Clicking
};

/// Runs the click loop on a background thread.
///
/// All signals are emitted from the background thread — connect with
/// Qt::QueuedConnection (the default for cross-thread signals) so that
/// slots run on the UI thread.
class ClickEngine : public QObject {
    Q_OBJECT

public:
    explicit ClickEngine(QObject* parent = nullptr);
    ~ClickEngine() override;

    /// Start clicking asynchronously. Does nothing if already running.
    void start(const ClickSession& session);

    /// Request the click loop to stop. Returns immediately; the loop may
    /// finish one more click before actually stopping.
    void stop();

    /// True if the engine is currently running (Clicking or WaitingForIdle).
    bool isRunning() const { return m_running.load(); }

signals:
    void clickCountUpdated(int count);
    void statusChanged(QuadClicker::EngineStatus status);
    void finished();

private:
    void loop(const ClickSession session);

    std::atomic<bool> m_cancel{false};
    std::atomic<bool> m_running{false};
};

} // namespace QuadClicker

// Make EngineStatus known to the Qt meta-object system for queued connections
Q_DECLARE_METATYPE(QuadClicker::EngineStatus)
