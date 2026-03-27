#include "TrayManager.h"

#include <QAction>
#include <QApplication>

namespace QuadClicker {

TrayManager::TrayManager(QObject* parent)
    : QObject(parent)
{
    m_available = QSystemTrayIcon::isSystemTrayAvailable();
    if (!m_available) return;

    // Build the context menu
    m_menu = std::make_unique<QMenu>();

    QAction* showAction = m_menu->addAction(QStringLiteral("Show Window"));
    connect(showAction, &QAction::triggered, this, &TrayManager::showWindowRequested);

    QAction* toggleAction = m_menu->addAction(QStringLiteral("Start / Stop"));
    connect(toggleAction, &QAction::triggered, this, &TrayManager::toggleClickingRequested);

    m_menu->addSeparator();

    QAction* quitAction = m_menu->addAction(QStringLiteral("Quit"));
    connect(quitAction, &QAction::triggered, this, &TrayManager::quitRequested);

    // Build the tray icon
    m_icon = std::make_unique<QSystemTrayIcon>(this);
    m_icon->setContextMenu(m_menu.get());
    m_icon->setToolTip(QStringLiteral("QuadClicker"));

    // Use a built-in Qt icon as placeholder (replace with real asset when available)
    m_icon->setIcon(QApplication::windowIcon().isNull()
                    ? QIcon::fromTheme(QStringLiteral("input-mouse"),
                                       QIcon::fromTheme(QStringLiteral("preferences-desktop")))
                    : QApplication::windowIcon());

    connect(m_icon.get(), &QSystemTrayIcon::activated,
            this, [this](QSystemTrayIcon::ActivationReason reason) {
                if (reason == QSystemTrayIcon::DoubleClick ||
                    reason == QSystemTrayIcon::Trigger)
                {
                    emit showWindowRequested();
                }
            });
}

TrayManager::~TrayManager()
{
    hide();
}

void TrayManager::show()
{
    if (m_available && m_icon) m_icon->show();
}

void TrayManager::hide()
{
    if (m_available && m_icon) m_icon->hide();
}

void TrayManager::setActiveState(bool isClicking)
{
    if (!m_available || !m_icon) return;
    m_icon->setToolTip(isClicking
                       ? QStringLiteral("QuadClicker \u2014 Clicking")
                       : QStringLiteral("QuadClicker"));
}

} // namespace QuadClicker
