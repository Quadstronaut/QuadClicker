#include "LocationPicker.h"

#include <QApplication>
#include <QGuiApplication>
#include <QScreen>
#include <QLabel>
#include <QPainter>
#include <QMouseEvent>
#include <QKeyEvent>
#include <QCursor>
#include <QVBoxLayout>

namespace QuadClicker {

// ── Overlay widget ─────────────────────────────────────────────────────────────

class PickerOverlay : public QWidget {
public:
    explicit PickerOverlay(LocationPicker* picker)
        : QWidget(nullptr,
                  Qt::FramelessWindowHint | Qt::WindowStaysOnTopHint | Qt::Tool)
        , m_picker(picker)
    {
        setAttribute(Qt::WA_TranslucentBackground);
        setAttribute(Qt::WA_DeleteOnClose);
        setMouseTracking(true);
        setCursor(Qt::CrossCursor);

        // Cover all screens via virtual geometry
        QRect virt = QGuiApplication::primaryScreen()->virtualGeometry();
        setGeometry(virt);

        // Instruction label at top-centre
        m_label = new QLabel(QStringLiteral("Click to select location  |  ESC to cancel"), this);
        m_label->setStyleSheet(
            QStringLiteral("QLabel {"
                           " color: white;"
                           " background-color: rgba(20,20,20,200);"
                           " font-size: 14px;"
                           " padding: 8px 14px;"
                           "}"));
        m_label->adjustSize();
        int labelX = (width() - m_label->width()) / 2;
        m_label->move(labelX, 40);
    }

protected:
    void paintEvent(QPaintEvent*) override
    {
        // Nearly transparent dark overlay (alpha=1 keeps Qt from treating it
        // as fully transparent on some compositors)
        QPainter p(this);
        p.fillRect(rect(), QColor(0, 0, 0, 1));
    }

    void mousePressEvent(QMouseEvent* ev) override
    {
        if (ev->button() == Qt::LeftButton) {
            QPoint global = ev->globalPosition().toPoint();
            // Close overlay before emitting so the signal handler sees a clean state
            close();
            emit m_picker->locationPicked(global.x(), global.y());
        }
    }

    void keyPressEvent(QKeyEvent* ev) override
    {
        if (ev->key() == Qt::Key_Escape) {
            close();
            emit m_picker->pickCancelled();
        }
    }

private:
    LocationPicker* m_picker;
    QLabel*         m_label{nullptr};
};

// ── LocationPicker ──────────────────────────────────────────────────────────────

LocationPicker::LocationPicker(QObject* parent)
    : QObject(parent)
    , m_delayTimer(new QTimer(this))
{
    m_delayTimer->setSingleShot(true);
    connect(m_delayTimer, &QTimer::timeout, this, &LocationPicker::showOverlay);
}

LocationPicker::~LocationPicker()
{
    cancelPick();
}

void LocationPicker::beginPick(QWidget* owner)
{
    // Cancel any previous in-progress pick
    cancelPick();

    m_owner = owner;

    if (owner) owner->showMinimized();

    // Wait 300ms for the window to actually minimise before showing the overlay
    m_delayTimer->start(300);
}

void LocationPicker::cancelPick()
{
    m_delayTimer->stop();

    if (m_overlay) {
        m_overlay->close();
        m_overlay = nullptr;
    }
}

void LocationPicker::showOverlay()
{
    m_overlay = new PickerOverlay(this);

    // When the overlay is destroyed (via Qt parent/child ownership + WA_DeleteOnClose),
    // clear our pointer so we don't double-delete
    connect(m_overlay, &QWidget::destroyed, this, [this]() {
        m_overlay = nullptr;
    });

    // After pick or cancel the owner should be restored — connect to our own signals
    auto restore = [this]() {
        if (m_owner) {
            m_owner->showNormal();
            m_owner->activateWindow();
            m_owner->raise();
        }
    };
    connect(this, &LocationPicker::locationPicked, this, restore, Qt::SingleShotConnection);
    connect(this, &LocationPicker::pickCancelled,  this, restore, Qt::SingleShotConnection);

    m_overlay->showFullScreen();
    m_overlay->activateWindow();
    m_overlay->raise();
}

} // namespace QuadClicker
