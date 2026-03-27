#pragma once

#include <QObject>
#include <QSystemTrayIcon>
#include <QMenu>
#include <memory>

namespace QuadClicker {

/// Manages the system tray icon lifecycle.
///
/// If QSystemTrayIcon::isSystemTrayAvailable() returns false the manager
/// degrades gracefully — no icon is shown and the tray signals are never fired.
class TrayManager : public QObject {
    Q_OBJECT

public:
    explicit TrayManager(QObject* parent = nullptr);
    ~TrayManager() override;

    /// Show the tray icon. No-op if tray is not available.
    void show();

    /// Hide the tray icon.
    void hide();

    /// Update tooltip to reflect clicking state.
    void setActiveState(bool isClicking);

    /// True if a system tray is available on this desktop.
    bool isAvailable() const { return m_available; }

signals:
    void showWindowRequested();
    void toggleClickingRequested();
    void quitRequested();

private:
    bool                               m_available{false};
    std::unique_ptr<QSystemTrayIcon>   m_icon;
    std::unique_ptr<QMenu>             m_menu;
};

} // namespace QuadClicker
