#pragma once

#include "../models/ClickSession.h"
#include <QFuture>
#include <QObject>
#include <QString>
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
    /// Cancels and WAITS for the worker: it captures `this`, so the object must
    /// outlive it.
    ~ClickEngine() override;

    /// Start clicking asynchronously. If a previous run is still winding down
    /// (or running), it is cancelled and waited for first, so two loops never
    /// overlap and a fast Stop->Start is never dropped.
    void start(const ClickSession& session);

    /// Request the click loop to stop. Returns immediately; the loop may
    /// finish one more click before actually stopping.
    void stop();

    /// True if the engine is currently running (Clicking or WaitingForIdle).
    bool isRunning() const { return m_running.load(); }

    /// Id of the most recent start(). finished(runId) from an older run is stale.
    quint64 currentRunId() const { return m_runId; }

signals:
    void clickCountUpdated(int count);
    void statusChanged(QuadClicker::EngineStatus status);
    void finished(quint64 runId);
    /// The run could not proceed (no injector, idle time unreadable).
    void errorOccurred(const QString& message);

private:
    void loop(const ClickSession session, quint64 runId);

    std::atomic<bool> m_cancel{false};
    std::atomic<bool> m_running{false};
    QFuture<void>     m_future;   ///< the worker; joined before restart/destruction
    quint64           m_runId{0}; ///< touched only on the owning (UI) thread
};

} // namespace QuadClicker

// Make EngineStatus known to the Qt meta-object system for queued connections
Q_DECLARE_METATYPE(QuadClicker::EngineStatus)
