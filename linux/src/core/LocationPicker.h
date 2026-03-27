#pragma once

#include <QObject>
#include <QWidget>
#include <QTimer>
#include <functional>
#include <memory>

namespace QuadClicker {

/// Full-screen transparent overlay that captures a mouse click position.
///
/// Usage:
///   picker.beginPick(ownerWidget);
///   // Connect signals before calling beginPick
///
/// The ownerWidget is minimised before showing the overlay. After capture
/// or cancellation the owner is restored.
class LocationPicker : public QObject {
    Q_OBJECT

public:
    explicit LocationPicker(QObject* parent = nullptr);
    ~LocationPicker() override;

    /// Start the pick interaction. Minimises \p owner first, then after a
    /// 300ms delay shows the fullscreen overlay.
    void beginPick(QWidget* owner);

    /// Cancel any in-progress pick.
    void cancelPick();

signals:
    /// Emitted when the user clicks a point. x and y are in screen coordinates.
    void locationPicked(int x, int y);

    /// Emitted when the user presses ESC to cancel.
    void pickCancelled();

private:
    void showOverlay();
    void cleanup();

    QWidget* m_owner{nullptr};
    QTimer*  m_delayTimer{nullptr};

    // The overlay widget is heap-allocated and self-deleting via Qt ownership
    QWidget* m_overlay{nullptr};
};

} // namespace QuadClicker
