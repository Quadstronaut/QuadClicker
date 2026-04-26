#include "MainWindow.h"
#include "core/ClickRateParser.h"

#include <QApplication>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QGridLayout>
#include <QFrame>
#include <QSizePolicy>
#include <QKeyEvent>
#include <QMetaObject>
#include <QTimer>

#include <cmath>

#include <X11/Xlib.h>
#include <X11/keysym.h>

namespace QuadClicker {

// ── Colour constants (Taneth palette — deep-green hull, gold HUD accent) ──────
static const char* CSS_WINDOW = R"(
QMainWindow, QWidget#centralWidget {
    background-color: #0A1410;
}
QLabel {
    color: #E8DCB0;
    font-size: 13px;
}
QLineEdit {
    background-color: #13211C;
    color: #E8DCB0;
    border: 1px solid #2D5448;
    border-radius: 3px;
    padding: 3px 6px;
    font-size: 13px;
}
QLineEdit:focus {
    border-color: #E8B547;
}
QLineEdit:disabled {
    color: #3D5048;
    background-color: #0A1410;
}
QComboBox {
    background-color: #13211C;
    color: #E8DCB0;
    border: 1px solid #2D5448;
    border-radius: 3px;
    padding: 3px 6px;
    font-size: 13px;
}
QComboBox:focus {
    border-color: #E8B547;
}
QComboBox QAbstractItemView {
    background-color: #13211C;
    color: #E8DCB0;
    selection-background-color: #E8B547;
    selection-color: #0A1410;
}
QRadioButton {
    color: #E8DCB0;
    font-size: 13px;
    spacing: 6px;
}
QRadioButton::indicator {
    width: 14px;
    height: 14px;
}
QRadioButton::indicator:checked {
    background-color: #E8B547;
    border: 2px solid #E8B547;
    border-radius: 7px;
}
QRadioButton::indicator:unchecked {
    background-color: #13211C;
    border: 2px solid #2D5448;
    border-radius: 7px;
}
QCheckBox {
    color: #E8DCB0;
    font-size: 13px;
    spacing: 6px;
}
QCheckBox::indicator {
    width: 14px;
    height: 14px;
}
QCheckBox::indicator:checked {
    background-color: #E8B547;
    border: 2px solid #E8B547;
    border-radius: 2px;
}
QCheckBox::indicator:unchecked {
    background-color: #13211C;
    border: 2px solid #2D5448;
    border-radius: 2px;
}
QPushButton#smallBtn {
    background-color: #1B2E27;
    color: #E8DCB0;
    border: 1px solid #2D5448;
    border-radius: 3px;
    padding: 3px 8px;
    font-size: 12px;
}
QPushButton#smallBtn:hover {
    background-color: #13211C;
    border-color: #E8B547;
}
QPushButton#smallBtn:pressed {
    background-color: #0A1410;
}
QPushButton#startBtn {
    background-color: #E8B547;
    color: #0A1410;
    border: none;
    border-radius: 4px;
    font-size: 14px;
    font-weight: bold;
    padding: 8px;
}
QPushButton#startBtn:hover {
    background-color: #F5C75A;
}
QPushButton#startBtn:pressed {
    background-color: #B88A2A;
}
QPushButton#stopBtn {
    background-color: #E04030;
    color: #FFFFFF;
    border: none;
    border-radius: 4px;
    font-size: 14px;
    font-weight: bold;
    padding: 8px;
}
QPushButton#stopBtn:hover {
    background-color: #C8331E;
}
QPushButton#stopBtn:pressed {
    background-color: #A8281A;
}
)";

// ── Constructor ───────────────────────────────────────────────────────────────

MainWindow::MainWindow(QWidget* parent)
    : QMainWindow(parent)
    , m_engine(new ClickEngine(this))
    , m_picker(new LocationPicker(this))
    , m_tray(new TrayManager(this))
    , m_hotkeys(new HotkeyManager(this))
    , m_settings(AppSettings::load())
{
    setWindowTitle(QStringLiteral("QuadClicker"));
    setFixedSize(420, 510);
    setStyleSheet(QString::fromUtf8(CSS_WINDOW));

    buildUi();
    loadSettings();

    // ── Engine connections ─────────────────────────────────────────────────
    connect(m_engine, &ClickEngine::clickCountUpdated,
            this,     &MainWindow::onClickCountUpdated,
            Qt::QueuedConnection);
    connect(m_engine, &ClickEngine::statusChanged,
            this,     &MainWindow::onEngineStatusChanged,
            Qt::QueuedConnection);
    connect(m_engine, &ClickEngine::finished,
            this,     &MainWindow::onEngineFinished,
            Qt::QueuedConnection);

    // ── Picker connections ─────────────────────────────────────────────────
    connect(m_picker, &LocationPicker::locationPicked,
            this,     &MainWindow::onLocationPicked);
    connect(m_picker, &LocationPicker::pickCancelled,
            this,     &MainWindow::onPickCancelled);

    // ── Tray connections ───────────────────────────────────────────────────
    connect(m_tray, &TrayManager::showWindowRequested,
            this,   &MainWindow::onTrayShowWindow);
    connect(m_tray, &TrayManager::toggleClickingRequested,
            this,   &MainWindow::onTrayToggleClicking);
    connect(m_tray, &TrayManager::quitRequested,
            this,   &MainWindow::onTrayQuit);

    m_tray->show();
}

MainWindow::~MainWindow()
{
    // Ensure engine is stopped before destruction
    m_engine->stop();
}

// ── Build UI ──────────────────────────────────────────────────────────────────

void MainWindow::buildUi()
{
    QWidget* central = new QWidget(this);
    central->setObjectName(QStringLiteral("centralWidget"));
    setCentralWidget(central);

    QVBoxLayout* root = new QVBoxLayout(central);
    root->setContentsMargins(16, 12, 16, 12);
    root->setSpacing(4);

    // ── Click Rate ─────────────────────────────────────────────────────────
    root->addWidget(buildClickRateRow());

    root->addSpacing(2);

    // ── Mouse Button ───────────────────────────────────────────────────────
    root->addWidget(buildMouseButtonRow());

    // ── Click Type ─────────────────────────────────────────────────────────
    root->addWidget(buildClickTypeRow());

    // ── Location ──────────────────────────────────────────────────────────
    root->addWidget(buildLocationRow());

    // ── XY + Pick ─────────────────────────────────────────────────────────
    root->addWidget(buildCoordinatesRow());

    root->addSpacing(2);

    // ── Section: Stop Conditions ───────────────────────────────────────────
    root->addWidget(buildSectionSeparator(QStringLiteral("Stop Conditions")));
    root->addWidget(buildStopConditionsRows());

    root->addSpacing(2);

    // ── Section: Advanced ─────────────────────────────────────────────────
    root->addWidget(buildSectionSeparator(QStringLiteral("Advanced")));
    root->addWidget(buildAdvancedRow());
    root->addWidget(buildHotkeysRow());

    root->addSpacing(2);

    // ── Error label ────────────────────────────────────────────────────────
    m_errorLabel = new QLabel(this);
    m_errorLabel->setStyleSheet(QStringLiteral("color: #E04030; font-size: 12px;"));
    m_errorLabel->setWordWrap(true);
    m_errorLabel->setVisible(false);
    root->addWidget(m_errorLabel);

    // ── Status bar ─────────────────────────────────────────────────────────
    {
        QWidget* statusRow = new QWidget(this);
        QHBoxLayout* hl = new QHBoxLayout(statusRow);
        hl->setContentsMargins(0, 0, 0, 0);
        hl->setSpacing(6);

        m_statusDot = new QLabel(this);
        m_statusDot->setFixedSize(8, 8);
        m_statusDot->setStyleSheet(
            QStringLiteral("background-color: #3D5048; border-radius: 4px;"));
        hl->addWidget(m_statusDot, 0, Qt::AlignVCenter);

        m_statusLabel = new QLabel(QStringLiteral("Stopped"), this);
        m_statusLabel->setStyleSheet(
            QStringLiteral("color: #7A9088; font-size: 12px;"));
        hl->addWidget(m_statusLabel, 0, Qt::AlignVCenter);

        m_clickCountLabel = new QLabel(this);
        m_clickCountLabel->setStyleSheet(
            QStringLiteral("color: #7A9088; font-size: 12px;"));
        m_clickCountLabel->setVisible(false);
        hl->addSpacing(10);
        hl->addWidget(m_clickCountLabel, 0, Qt::AlignVCenter);
        hl->addStretch();

        root->addWidget(statusRow);
    }

    root->addSpacing(4);

    // ── Start / Stop button ────────────────────────────────────────────────
    m_startStopBtn = new QPushButton(QStringLiteral("START"), this);
    m_startStopBtn->setObjectName(QStringLiteral("startBtn"));
    m_startStopBtn->setMinimumHeight(38);
    connect(m_startStopBtn, &QPushButton::clicked,
            this, &MainWindow::onStartStopClicked);
    root->addWidget(m_startStopBtn);

    // ── Hotkey note for Wayland ────────────────────────────────────────────
    if (!m_hotkeys->isSupported()) {
        QLabel* note = new QLabel(HotkeyManager::unsupportedNote(), this);
        note->setStyleSheet(
            QStringLiteral("color: #7A9088; font-size: 11px;"));
        note->setWordWrap(true);
        root->addWidget(note);
    }
}

QWidget* MainWindow::buildClickRateRow()
{
    auto* host = new QWidget(this);
    auto* root = new QHBoxLayout(host);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    auto* lbl = new QLabel(QStringLiteral("Click Rate:"), host);
    lbl->setFixedWidth(155);
    lbl->setAlignment(Qt::AlignTop | Qt::AlignLeft);
    root->addWidget(lbl);

    auto* col = new QVBoxLayout();
    col->setContentsMargins(0, 0, 0, 0);
    col->setSpacing(2);

    // Mode radios
    auto* modeRow = new QHBoxLayout();
    modeRow->setSpacing(16);
    modeRow->setContentsMargins(0, 0, 0, 0);
    m_modeDelay     = new QRadioButton(QStringLiteral("Delay"), host);
    m_modeFrequency = new QRadioButton(QStringLiteral("Frequency"), host);
    m_rateModeGroup = new QButtonGroup(host);
    m_rateModeGroup->addButton(m_modeDelay,     0);
    m_rateModeGroup->addButton(m_modeFrequency, 1);
    m_modeDelay->setChecked(true);
    modeRow->addWidget(m_modeDelay);
    modeRow->addWidget(m_modeFrequency);
    modeRow->addStretch();
    col->addLayout(modeRow);

    // Value + Unit
    auto* valRow = new QHBoxLayout();
    valRow->setSpacing(6);
    valRow->setContentsMargins(0, 0, 0, 0);
    m_clickRateValueEdit = new QLineEdit(QStringLiteral("100"), host);
    m_clickRateValueEdit->setFixedWidth(80);
    m_clickRateValueEdit->setToolTip(
        QStringLiteral("Enter a number. Unit selected on the right."));
    valRow->addWidget(m_clickRateValueEdit);

    m_clickRateUnitBox = new QComboBox(host);
    m_clickRateUnitBox->setFixedWidth(110);
    m_clickRateUnitBox->setToolTip(QStringLiteral("Unit for the click rate value"));
    valRow->addWidget(m_clickRateUnitBox);
    valRow->addStretch();
    col->addLayout(valRow);

    // Hint
    m_rateHintLabel = new QLabel(QString(), host);
    m_rateHintLabel->setStyleSheet(QStringLiteral("color: #7A9088; font-size: 11px;"));
    m_rateHintLabel->setWordWrap(true);
    col->addWidget(m_rateHintLabel);

    root->addLayout(col, /*stretch=*/1);

    // Wiring (deferred — populateUnitBox + signal hookups happen in loadSettings)
    connect(m_modeDelay,     &QRadioButton::toggled, this, [this](bool on) {
        if (on) onClickRateModeChanged();
    });
    connect(m_modeFrequency, &QRadioButton::toggled, this, [this](bool on) {
        if (on) onClickRateModeChanged();
    });
    connect(m_clickRateValueEdit, &QLineEdit::textChanged,
            this, &MainWindow::onClickRateInputChanged);
    connect(m_clickRateUnitBox, QOverload<int>::of(&QComboBox::currentIndexChanged),
            this, [this](int){ onClickRateInputChanged(); });

    return host;
}

QWidget* MainWindow::buildMouseButtonRow()
{
    QWidget* row = new QWidget(this);
    QHBoxLayout* hl = new QHBoxLayout(row);
    hl->setContentsMargins(0, 0, 0, 0);

    QLabel* lbl = new QLabel(QStringLiteral("Mouse Button:"), this);
    lbl->setFixedWidth(155);
    hl->addWidget(lbl);

    m_buttonGroup = new QButtonGroup(this);
    m_btnLeft     = new QRadioButton(QStringLiteral("Left"),   this);
    m_btnRight    = new QRadioButton(QStringLiteral("Right"),  this);
    m_btnMiddle   = new QRadioButton(QStringLiteral("Middle"), this);
    m_btnLeft->setChecked(true);
    m_buttonGroup->addButton(m_btnLeft,   0);
    m_buttonGroup->addButton(m_btnRight,  1);
    m_buttonGroup->addButton(m_btnMiddle, 2);

    hl->addWidget(m_btnLeft);
    hl->addSpacing(8);
    hl->addWidget(m_btnRight);
    hl->addSpacing(8);
    hl->addWidget(m_btnMiddle);
    hl->addStretch();
    return row;
}

QWidget* MainWindow::buildClickTypeRow()
{
    QWidget* row = new QWidget(this);
    QHBoxLayout* hl = new QHBoxLayout(row);
    hl->setContentsMargins(0, 0, 0, 0);

    QLabel* lbl = new QLabel(QStringLiteral("Click Type:"), this);
    lbl->setFixedWidth(155);
    hl->addWidget(lbl);

    m_typeGroup  = new QButtonGroup(this);
    m_typeSingle = new QRadioButton(QStringLiteral("Single"), this);
    m_typeDouble = new QRadioButton(QStringLiteral("Double"), this);
    m_typeSingle->setChecked(true);
    m_typeGroup->addButton(m_typeSingle, 0);
    m_typeGroup->addButton(m_typeDouble, 1);

    hl->addWidget(m_typeSingle);
    hl->addSpacing(8);
    hl->addWidget(m_typeDouble);
    hl->addStretch();
    return row;
}

QWidget* MainWindow::buildLocationRow()
{
    QWidget* row = new QWidget(this);
    QHBoxLayout* hl = new QHBoxLayout(row);
    hl->setContentsMargins(0, 0, 0, 0);

    QLabel* lbl = new QLabel(QStringLiteral("Location:"), this);
    lbl->setFixedWidth(155);
    hl->addWidget(lbl);

    m_locGroup   = new QButtonGroup(this);
    m_locCurrent = new QRadioButton(QStringLiteral("Current"),  this);
    m_locFixed   = new QRadioButton(QStringLiteral("Fixed XY"), this);
    m_locCurrent->setChecked(true);
    m_locGroup->addButton(m_locCurrent, 0);
    m_locGroup->addButton(m_locFixed,   1);

    hl->addWidget(m_locCurrent);
    hl->addSpacing(8);
    hl->addWidget(m_locFixed);
    hl->addStretch();

    // Enable/disable XY inputs when location mode changes
    connect(m_locFixed, &QRadioButton::toggled, this, [this](bool checked) {
        m_xEdit->setEnabled(checked);
        m_yEdit->setEnabled(checked);
        m_pickBtn->setEnabled(checked);
    });

    return row;
}

QWidget* MainWindow::buildCoordinatesRow()
{
    QWidget* row = new QWidget(this);
    QHBoxLayout* hl = new QHBoxLayout(row);
    hl->setContentsMargins(0, 0, 0, 0);

    QLabel* lbl = new QLabel(QStringLiteral("Coordinates (X, Y):"), this);
    lbl->setFixedWidth(155);
    hl->addWidget(lbl);

    m_xEdit = new QLineEdit(this);
    m_xEdit->setFixedWidth(52);
    m_xEdit->setEnabled(false);
    m_xEdit->setToolTip(QStringLiteral("X coordinate (pixels from left)"));
    hl->addWidget(m_xEdit);

    hl->addSpacing(6);

    m_yEdit = new QLineEdit(this);
    m_yEdit->setFixedWidth(52);
    m_yEdit->setEnabled(false);
    m_yEdit->setToolTip(QStringLiteral("Y coordinate (pixels from top)"));
    hl->addWidget(m_yEdit);

    hl->addSpacing(8);

    m_pickBtn = new QPushButton(QStringLiteral("Pick\u2026"), this);
    m_pickBtn->setObjectName(QStringLiteral("smallBtn"));
    m_pickBtn->setFixedWidth(56);
    m_pickBtn->setEnabled(false);
    m_pickBtn->setToolTip(
        QStringLiteral("Click anywhere on screen to capture coordinates"));
    connect(m_pickBtn, &QPushButton::clicked, this, &MainWindow::onPickClicked);
    hl->addWidget(m_pickBtn);

    hl->addStretch();
    return row;
}

QWidget* MainWindow::buildSectionSeparator(const QString& label)
{
    QWidget* row = new QWidget(this);
    QHBoxLayout* hl = new QHBoxLayout(row);
    hl->setContentsMargins(0, 4, 0, 2);
    hl->setSpacing(6);

    QFrame* line1 = new QFrame(this);
    line1->setFrameShape(QFrame::HLine);
    line1->setFixedWidth(8);
    line1->setStyleSheet(QStringLiteral("color: #2D5448;"));
    hl->addWidget(line1, 0, Qt::AlignVCenter);

    QLabel* lbl = new QLabel(label, this);
    lbl->setStyleSheet(
        QStringLiteral("color: #7A9088; font-size: 11px; background: transparent;"));
    hl->addWidget(lbl, 0, Qt::AlignVCenter);

    QFrame* line2 = new QFrame(this);
    line2->setFrameShape(QFrame::HLine);
    line2->setStyleSheet(QStringLiteral("color: #2D5448;"));
    hl->addWidget(line2, 1, Qt::AlignVCenter);

    return row;
}

QWidget* MainWindow::buildStopConditionsRows()
{
    QWidget* w = new QWidget(this);
    QVBoxLayout* vl = new QVBoxLayout(w);
    vl->setContentsMargins(0, 0, 0, 0);
    vl->setSpacing(4);

    // After clicks
    {
        QWidget* row = new QWidget(this);
        QHBoxLayout* hl = new QHBoxLayout(row);
        hl->setContentsMargins(0, 0, 0, 0);
        QLabel* lbl = new QLabel(QStringLiteral("After clicks:"), this);
        lbl->setFixedWidth(155);
        hl->addWidget(lbl);
        m_stopClicksEdit = new QLineEdit(QStringLiteral("0"), this);
        m_stopClicksEdit->setToolTip(
            QStringLiteral("Stop after this many clicks (0 = unlimited)"));
        hl->addWidget(m_stopClicksEdit, 1);
        vl->addWidget(row);
    }

    // After seconds
    {
        QWidget* row = new QWidget(this);
        QHBoxLayout* hl = new QHBoxLayout(row);
        hl->setContentsMargins(0, 0, 0, 0);
        QLabel* lbl = new QLabel(QStringLiteral("After seconds:"), this);
        lbl->setFixedWidth(155);
        hl->addWidget(lbl);
        m_stopSecondsEdit = new QLineEdit(QStringLiteral("0"), this);
        m_stopSecondsEdit->setToolTip(
            QStringLiteral("Stop after this many seconds (0 = unlimited)"));
        hl->addWidget(m_stopSecondsEdit, 1);
        vl->addWidget(row);
    }

    return w;
}

QWidget* MainWindow::buildAdvancedRow()
{
    QWidget* row = new QWidget(this);
    QHBoxLayout* hl = new QHBoxLayout(row);
    hl->setContentsMargins(0, 0, 0, 0);

    QLabel* lbl = new QLabel(QStringLiteral("Idle wait (seconds):"), this);
    lbl->setFixedWidth(155);
    hl->addWidget(lbl);

    m_idleEdit = new QLineEdit(QStringLiteral("0"), this);
    m_idleEdit->setFixedWidth(60);
    m_idleEdit->setToolTip(
        QStringLiteral("Wait this many seconds of system idle before starting (0 = disabled)"));
    hl->addWidget(m_idleEdit);

    hl->addSpacing(14);

    m_alwaysOnTopBox = new QCheckBox(QStringLiteral("Always on top"), this);
    connect(m_alwaysOnTopBox, &QCheckBox::toggled,
            this, &MainWindow::onAlwaysOnTopChanged);
    hl->addWidget(m_alwaysOnTopBox);

    hl->addStretch();
    return row;
}

QWidget* MainWindow::buildHotkeysRow()
{
    QWidget* row = new QWidget(this);
    QHBoxLayout* hl = new QHBoxLayout(row);
    hl->setContentsMargins(0, 0, 0, 0);
    hl->setSpacing(4);

    QLabel* lbl = new QLabel(QStringLiteral("Hotkeys:"), this);
    lbl->setFixedWidth(155);
    hl->addWidget(lbl);

    QLabel* startLbl = new QLabel(QStringLiteral("Start:"), this);
    startLbl->setStyleSheet(QStringLiteral("color: #7A9088; font-size: 12px;"));
    hl->addWidget(startLbl);

    m_startHotkeyEdit = new QLineEdit(this);
    m_startHotkeyEdit->setReadOnly(true);
    m_startHotkeyEdit->setFixedWidth(60);
    m_startHotkeyEdit->setToolTip(
        QStringLiteral("Click then press a key to set the start hotkey"));
    installHotkeyCaptureFilter(m_startHotkeyEdit);
    hl->addWidget(m_startHotkeyEdit);

    hl->addSpacing(10);

    QLabel* stopLbl = new QLabel(QStringLiteral("Stop:"), this);
    stopLbl->setStyleSheet(QStringLiteral("color: #7A9088; font-size: 12px;"));
    hl->addWidget(stopLbl);

    m_stopHotkeyEdit = new QLineEdit(QStringLiteral("F10"), this);
    m_stopHotkeyEdit->setReadOnly(true);
    m_stopHotkeyEdit->setFixedWidth(60);
    m_stopHotkeyEdit->setToolTip(
        QStringLiteral("Click then press a key to set the stop hotkey"));
    installHotkeyCaptureFilter(m_stopHotkeyEdit);
    hl->addWidget(m_stopHotkeyEdit);

    hl->addStretch();
    return row;
}

// ── Hotkey capture ─────────────────────────────────────────────────────────────

class HotkeyEditFilter : public QObject {
public:
    HotkeyEditFilter(QLineEdit* edit, MainWindow* window)
        : QObject(edit), m_edit(edit), m_window(window) {}

protected:
    bool eventFilter(QObject* watched, QEvent* event) override
    {
        if (watched == m_edit && event->type() == QEvent::KeyPress) {
            auto* kev = static_cast<QKeyEvent*>(event);
            return m_window->hotkeyEditKeyPress(m_edit, kev);
        }
        if (watched == m_edit && event->type() == QEvent::FocusIn) {
            m_edit->setStyleSheet(
                QStringLiteral("QLineEdit { border: 1px solid #E8B547; "
                                "background-color: #13211C; color: #E8DCB0; "
                                "border-radius: 3px; padding: 3px 6px; }"));
        }
        if (watched == m_edit && event->type() == QEvent::FocusOut) {
            m_edit->setStyleSheet(QString()); // Revert to global stylesheet
        }
        return false;
    }
private:
    QLineEdit*  m_edit{nullptr};
    MainWindow* m_window{nullptr};
};

void MainWindow::installHotkeyCaptureFilter(QLineEdit* edit)
{
    edit->installEventFilter(new HotkeyEditFilter(edit, this));
}

bool MainWindow::hotkeyEditKeyPress(QLineEdit* edit, QKeyEvent* ev)
{
    ev->accept();

    if (ev->key() == Qt::Key_Escape) {
        edit->clear();
        edit->clearFocus();
        reRegisterHotkeys();
        return true;
    }

    // Ignore lone modifier keys
    Qt::Key k = static_cast<Qt::Key>(ev->key());
    if (k == Qt::Key_Shift   || k == Qt::Key_Control ||
        k == Qt::Key_Alt     || k == Qt::Key_Meta    ||
        k == Qt::Key_AltGr   || k == Qt::Key_CapsLock)
        return true;

    Qt::KeyboardModifiers mods = ev->modifiers();
    QString text = buildHotkeyText(k, mods);

    // Prevent both boxes from having the same hotkey
    bool sameAsOther = (edit == m_startHotkeyEdit)
                       ? (text == m_stopHotkeyEdit->text())
                       : (text == m_startHotkeyEdit->text());
    if (sameAsOther) {
        showError(QStringLiteral("Start and stop hotkeys cannot be the same."));
        return true;
    }

    clearError();
    edit->setText(text);
    edit->clearFocus();
    reRegisterHotkeys();
    return true;
}

QString MainWindow::buildHotkeyText(Qt::Key key, Qt::KeyboardModifiers mods)
{
    QStringList parts;
    if (mods & Qt::ControlModifier) parts << QStringLiteral("Ctrl");
    if (mods & Qt::ShiftModifier)   parts << QStringLiteral("Shift");
    if (mods & Qt::AltModifier)     parts << QStringLiteral("Alt");
    parts << QKeySequence(key).toString();
    return parts.join(QLatin1Char('+'));
}

bool MainWindow::tryParseHotkeyText(const QString& text,
                                     unsigned int& modifiers,
                                     unsigned int& keysym) const
{
    modifiers = 0;
    keysym    = 0;

    if (text.trimmed().isEmpty()) return false;

    QStringList parts = text.split(QLatin1Char('+'));
    QString keyPart = parts.last();

    for (int i = 0; i < parts.size() - 1; ++i) {
        QString mod = parts[i].trimmed().toUpper();
        if (mod == QLatin1String("CTRL"))  modifiers |= ControlMask;
        else if (mod == QLatin1String("SHIFT")) modifiers |= ShiftMask;
        else if (mod == QLatin1String("ALT"))   modifiers |= Mod1Mask;
    }

    // Convert Qt key name to X11 KeySym
    QKeySequence seq(keyPart);
    if (seq.isEmpty()) return false;

    int qtKey = seq[0].key();
    // Map common Qt keys to X11 keysyms
    // Qt::Key values for F-keys and letter/number match X11 keysyms for those ranges
    if (qtKey >= Qt::Key_F1 && qtKey <= Qt::Key_F35) {
        keysym = static_cast<unsigned int>(XK_F1 + (qtKey - Qt::Key_F1));
    } else if (qtKey >= Qt::Key_A && qtKey <= Qt::Key_Z) {
        // X11 keysym for lowercase letters
        keysym = static_cast<unsigned int>(XK_a + (qtKey - Qt::Key_A));
    } else if (qtKey >= Qt::Key_0 && qtKey <= Qt::Key_9) {
        keysym = static_cast<unsigned int>(XK_0 + (qtKey - Qt::Key_0));
    } else {
        // Fallback: try using the Qt key value directly (works for many keysyms)
        keysym = static_cast<unsigned int>(qtKey);
    }

    return keysym != 0;
}

void MainWindow::reRegisterHotkeys()
{
    if (!m_hotkeys) return;

    if (m_startHotkeyHandle >= 0) {
        m_hotkeys->unregister(m_startHotkeyHandle);
        m_startHotkeyHandle = -1;
    }
    if (m_stopHotkeyHandle >= 0) {
        m_hotkeys->unregister(m_stopHotkeyHandle);
        m_stopHotkeyHandle = -1;
    }

    unsigned int smods = 0, skeysym = 0;
    if (tryParseHotkeyText(m_startHotkeyEdit->text(), smods, skeysym)) {
        m_startHotkeyHandle = m_hotkeys->registerHotkey(smods, skeysym, [this]() {
            QMetaObject::invokeMethod(this, [this]() {
                if (!m_isClicking) startClicking();
            }, Qt::QueuedConnection);
        });
    }

    unsigned int emods = 0, ekeysym = 0;
    if (tryParseHotkeyText(m_stopHotkeyEdit->text(), emods, ekeysym)) {
        m_stopHotkeyHandle = m_hotkeys->registerHotkey(emods, ekeysym, [this]() {
            QMetaObject::invokeMethod(this, [this]() {
                if (m_isClicking) stopClicking();
            }, Qt::QueuedConnection);
        });
    }
}

// ── Slots ──────────────────────────────────────────────────────────────────────

void MainWindow::onStartStopClicked()
{
    if (m_isClicking) stopClicking();
    else              startClicking();
}

void MainWindow::onPickClicked()
{
    m_picker->beginPick(this);
}

void MainWindow::onLocationPicked(int x, int y)
{
    m_xEdit->setText(QString::number(x));
    m_yEdit->setText(QString::number(y));
    showNormal();
    activateWindow();
    raise();
}

void MainWindow::onPickCancelled()
{
    showNormal();
    activateWindow();
    raise();
}

void MainWindow::onClickCountUpdated(int count)
{
    m_clickCountLabel->setVisible(true);
    m_clickCountLabel->setText(QStringLiteral("Clicks: ") + QString::number(count));
}

void MainWindow::onEngineStatusChanged(EngineStatus status)
{
    setStatus(status);
}

void MainWindow::onEngineFinished()
{
    stopClicking();
    setStatus(EngineStatus::Stopped);
}

void MainWindow::onAlwaysOnTopChanged(bool checked)
{
    setWindowFlag(Qt::WindowStaysOnTopHint, checked);
    show(); // Must re-show after changing window flags
}

void MainWindow::onTrayShowWindow()   { restoreFromTray(); }
void MainWindow::onTrayToggleClicking() { onStartStopClicked(); }
void MainWindow::onTrayQuit()
{
    m_quitting = true;
    close();
}

// ── Window lifecycle ───────────────────────────────────────────────────────────

void MainWindow::closeEvent(QCloseEvent* ev)
{
    if (m_quitting) {
        saveSettings();
        m_hotkeys->unregisterAll();
        m_engine->stop();
        m_tray->hide();
        ev->accept();
        QApplication::quit();
    } else {
        // Minimize to tray instead of closing
        hide();
        ev->ignore();
    }
}

void MainWindow::changeEvent(QEvent* ev)
{
    QMainWindow::changeEvent(ev);
    if (ev->type() == QEvent::WindowStateChange) {
        if (isMinimized()) hide();
    }
}

void MainWindow::restoreFromTray()
{
    showNormal();
    activateWindow();
    raise();
}

// ── Session / State ────────────────────────────────────────────────────────────

bool MainWindow::tryBuildSession(ClickSession& session, QString& error) const
{
    // Click rate
    std::chrono::milliseconds delay;
    if (!ClickRateParser::tryParse(composeRateString(), delay, error)) return false;

    // Stop after clicks
    bool ok = false;
    int stopClicks = m_stopClicksEdit->text().toInt(&ok);
    if (!ok || stopClicks < 0) {
        error = QStringLiteral("Stop after (clicks) must be a non-negative integer.");
        return false;
    }

    // Stop after seconds
    double stopSeconds = m_stopSecondsEdit->text().toDouble(&ok);
    if (!ok || stopSeconds < 0) {
        error = QStringLiteral("Stop after (seconds) must be a non-negative number.");
        return false;
    }

    // Idle wait
    double idleWait = m_idleEdit->text().toDouble(&ok);
    if (!ok || idleWait < 0) {
        error = QStringLiteral("Idle wait must be a non-negative number.");
        return false;
    }

    bool useCurrent = m_locCurrent->isChecked();
    int x = 0, y = 0;
    if (!useCurrent) {
        x = m_xEdit->text().toInt(&ok);
        if (!ok) { error = QStringLiteral("X and Y coordinates must be valid integers."); return false; }
        y = m_yEdit->text().toInt(&ok);
        if (!ok) { error = QStringLiteral("X and Y coordinates must be valid integers."); return false; }
    }

    MouseButton button = m_btnRight->isChecked()  ? MouseButton::Right
                       : m_btnMiddle->isChecked() ? MouseButton::Middle
                       : MouseButton::Left;

    ClickType clickType = m_typeDouble->isChecked() ? ClickType::Double
                                                    : ClickType::Single;

    session = ClickSession(delay, button, clickType, useCurrent, x, y,
                           stopClicks, stopSeconds, idleWait);
    return true;
}

void MainWindow::startClicking()
{
    clearError();
    ClickSession session;
    QString error;
    if (!tryBuildSession(session, error)) {
        showError(error);
        return;
    }

    m_isClicking = true;
    setButtonState(true);
    m_tray->setActiveState(true);
    m_engine->start(session);
}

void MainWindow::stopClicking()
{
    m_engine->stop();
    m_isClicking = false;
    setButtonState(false);
    m_tray->setActiveState(false);
}

void MainWindow::setButtonState(bool isClicking)
{
    if (isClicking) {
        m_startStopBtn->setText(QStringLiteral("STOP"));
        m_startStopBtn->setObjectName(QStringLiteral("stopBtn"));
    } else {
        m_startStopBtn->setText(QStringLiteral("START"));
        m_startStopBtn->setObjectName(QStringLiteral("startBtn"));
        m_clickCountLabel->setVisible(false);
    }
    // Re-apply stylesheet after objectName change (Qt requires polish/unpolish)
    m_startStopBtn->style()->unpolish(m_startStopBtn);
    m_startStopBtn->style()->polish(m_startStopBtn);
}

void MainWindow::setStatus(EngineStatus status)
{
    switch (status) {
    case EngineStatus::Clicking:
        m_statusDot->setStyleSheet(
            QStringLiteral("background-color: #E8B547; border-radius: 4px;"));
        m_statusLabel->setText(QStringLiteral("Clicking"));
        m_statusLabel->setStyleSheet(
            QStringLiteral("color: #E8B547; font-size: 12px;"));
        break;
    case EngineStatus::WaitingForIdle:
        m_statusDot->setStyleSheet(
            QStringLiteral("background-color: #5BA89A; border-radius: 4px;"));
        m_statusLabel->setText(QStringLiteral("Waiting for idle\u2026"));
        m_statusLabel->setStyleSheet(
            QStringLiteral("color: #5BA89A; font-size: 12px;"));
        break;
    default:
        m_statusDot->setStyleSheet(
            QStringLiteral("background-color: #3D5048; border-radius: 4px;"));
        m_statusLabel->setText(QStringLiteral("Stopped"));
        m_statusLabel->setStyleSheet(
            QStringLiteral("color: #7A9088; font-size: 12px;"));
        break;
    }
}

void MainWindow::showError(const QString& msg)
{
    m_errorLabel->setText(msg);
    m_errorLabel->setVisible(true);
}

void MainWindow::clearError()
{
    m_errorLabel->clear();
    m_errorLabel->setVisible(false);
}

// ── Settings ───────────────────────────────────────────────────────────────────

void MainWindow::loadSettings()
{
    const AppSettings& s = m_settings;

    m_rateUiReady = false;
    if (s.clickRateMode == ClickRateMode::Frequency) {
        m_modeFrequency->setChecked(true);
        populateUnitBox(s.clickRateUnit, QStringLiteral("per_sec"));
    } else {
        m_modeDelay->setChecked(true);
        populateUnitBox(s.clickRateUnit, QStringLiteral("ms"));
    }
    m_clickRateValueEdit->setText(s.clickRateValue);
    m_rateUiReady = true;
    updateRateHint();

    m_btnLeft->setChecked(s.button   == MouseButton::Left);
    m_btnRight->setChecked(s.button  == MouseButton::Right);
    m_btnMiddle->setChecked(s.button == MouseButton::Middle);

    m_typeSingle->setChecked(s.clickType == ClickType::Single);
    m_typeDouble->setChecked(s.clickType == ClickType::Double);

    m_locCurrent->setChecked(s.useCurrentPosition);
    m_locFixed->setChecked(!s.useCurrentPosition);
    m_xEdit->setText(QString::number(s.x));
    m_yEdit->setText(QString::number(s.y));
    m_xEdit->setEnabled(!s.useCurrentPosition);
    m_yEdit->setEnabled(!s.useCurrentPosition);
    m_pickBtn->setEnabled(!s.useCurrentPosition);

    m_stopClicksEdit->setText(QString::number(s.stopAfterClicks));
    m_stopSecondsEdit->setText(QString::number(s.stopAfterSeconds));
    m_idleEdit->setText(QString::number(s.idleWaitSeconds));

    m_alwaysOnTopBox->setChecked(s.alwaysOnTop);
    if (s.alwaysOnTop) {
        setWindowFlag(Qt::WindowStaysOnTopHint, true);
        show();
    }

    m_startHotkeyEdit->setText(s.startHotkeyText);
    m_stopHotkeyEdit->setText(s.stopHotkeyText);

    reRegisterHotkeys();
}

void MainWindow::saveSettings()
{
    AppSettings& s = m_settings;

    s.clickRateMode  = m_modeFrequency->isChecked()
                         ? ClickRateMode::Frequency
                         : ClickRateMode::Delay;
    s.clickRateValue = m_clickRateValueEdit->text();
    s.clickRateUnit  = m_clickRateUnitBox->currentData().toString();

    s.button = m_btnRight->isChecked()  ? MouseButton::Right
             : m_btnMiddle->isChecked() ? MouseButton::Middle
             : MouseButton::Left;
    s.clickType = m_typeDouble->isChecked() ? ClickType::Double : ClickType::Single;

    s.useCurrentPosition = m_locCurrent->isChecked();

    bool ok = false;
    s.x = m_xEdit->text().toInt(&ok);     if (!ok) s.x = 0;
    s.y = m_yEdit->text().toInt(&ok);     if (!ok) s.y = 0;

    s.stopAfterClicks   = m_stopClicksEdit->text().toInt(&ok);   if (!ok) s.stopAfterClicks = 0;
    s.stopAfterSeconds  = m_stopSecondsEdit->text().toDouble(&ok); if (!ok) s.stopAfterSeconds = 0;
    s.idleWaitSeconds   = m_idleEdit->text().toDouble(&ok);      if (!ok) s.idleWaitSeconds = 0;

    s.alwaysOnTop      = m_alwaysOnTopBox->isChecked();
    s.startHotkeyText  = m_startHotkeyEdit->text();
    s.stopHotkeyText   = m_stopHotkeyEdit->text();

    s.save();
}

// ── Click Rate row helpers ────────────────────────────────────────────────────

void MainWindow::onClickRateModeChanged()
{
    if (!m_modeFrequency || !m_modeDelay) return;
    bool isFreq = m_modeFrequency->isChecked();
    QString prevTag = m_clickRateUnitBox->currentData().toString();
    QString fallback = isFreq ? QStringLiteral("per_sec") : QStringLiteral("ms");
    populateUnitBox(prevTag, fallback);
    if (m_rateUiReady) updateRateHint();
}

void MainWindow::onClickRateInputChanged()
{
    if (m_rateUiReady) updateRateHint();
}

void MainWindow::populateUnitBox(const QString& desiredTag, const QString& fallbackTag)
{
    bool wasReady = m_rateUiReady;
    m_rateUiReady = false;          // suppress hint rebuild during repopulation

    m_clickRateUnitBox->clear();
    bool isFreq = m_modeFrequency && m_modeFrequency->isChecked();
    if (isFreq) {
        m_clickRateUnitBox->addItem(QStringLiteral("per second"), QStringLiteral("per_sec"));
        m_clickRateUnitBox->addItem(QStringLiteral("per minute"), QStringLiteral("per_min"));
        m_clickRateUnitBox->addItem(QStringLiteral("per hour"),   QStringLiteral("per_hour"));
    } else {
        m_clickRateUnitBox->addItem(QStringLiteral("ms"),       QStringLiteral("ms"));
        m_clickRateUnitBox->addItem(QStringLiteral("seconds"),  QStringLiteral("sec"));
        m_clickRateUnitBox->addItem(QStringLiteral("minutes"),  QStringLiteral("min"));
    }
    int idx = m_clickRateUnitBox->findData(desiredTag);
    if (idx < 0) idx = m_clickRateUnitBox->findData(fallbackTag);
    if (idx < 0) idx = 0;
    m_clickRateUnitBox->setCurrentIndex(idx);

    m_rateUiReady = wasReady;
}

QString MainWindow::composeRateString() const
{
    QString v = m_clickRateValueEdit->text().trimmed();
    QString u = m_clickRateUnitBox->currentData().toString();
    if (u == QLatin1String("ms"))       return v + QLatin1String("ms");
    if (u == QLatin1String("sec"))      return v + QLatin1String("s");
    if (u == QLatin1String("min"))      return v + QLatin1String("min");
    if (u == QLatin1String("per_sec"))  return v + QLatin1String("/s");
    if (u == QLatin1String("per_min"))  return v + QLatin1String("/min");
    if (u == QLatin1String("per_hour")) return v + QLatin1String("/h");
    return v + QLatin1String("ms");
}

void MainWindow::updateRateHint()
{
    if (!m_rateHintLabel) return;

    QString v = m_clickRateValueEdit->text().trimmed();
    if (v.isEmpty()) {
        m_rateHintLabel->setText(QString());
        m_rateHintLabel->setStyleSheet(
            QStringLiteral("color: #7A9088; font-size: 11px;"));
        return;
    }

    std::chrono::milliseconds delay{};
    QString err;
    if (!ClickRateParser::tryParse(composeRateString(), delay, err)) {
        m_rateHintLabel->setText(QString());
        m_rateHintLabel->setStyleSheet(
            QStringLiteral("color: #7A9088; font-size: 11px;"));
        return;
    }

    double ms = static_cast<double>(delay.count());
    double cps = 1000.0 / ms;
    bool isDelay = m_modeDelay && m_modeDelay->isChecked();
    bool veryFast = cps > 100.0;
    QString conv = isDelay ? formatRate(cps) : formatDelay(ms);

    if (veryFast) {
        m_rateHintLabel->setText(
            QStringLiteral("⚠ Very fast — input may not register reliably  (≈ %1)")
                .arg(conv));
        m_rateHintLabel->setStyleSheet(
            QStringLiteral("color: #E04030; font-size: 11px;"));
    } else {
        m_rateHintLabel->setText(QStringLiteral("≈ %1").arg(conv));
        m_rateHintLabel->setStyleSheet(
            QStringLiteral("color: #7A9088; font-size: 11px;"));
    }
}

QString MainWindow::formatRate(double cps)
{
    if (cps >= 1.0) return QStringLiteral("%1 clicks/sec").arg(trimNumber(cps));
    double cpm = cps * 60.0;
    if (cpm >= 1.0) return QStringLiteral("%1 clicks/min").arg(trimNumber(cpm));
    return QStringLiteral("%1 clicks/hour").arg(trimNumber(cps * 3600.0));
}

QString MainWindow::formatDelay(double ms)
{
    if (ms < 1000.0)      return QStringLiteral("%1 ms between clicks").arg(trimNumber(ms));
    if (ms < 60'000.0)    return QStringLiteral("%1 sec between clicks").arg(trimNumber(ms / 1000.0));
    if (ms < 3'600'000.0) return QStringLiteral("%1 min between clicks").arg(trimNumber(ms / 60'000.0));
    return QStringLiteral("%1 hours between clicks").arg(trimNumber(ms / 3'600'000.0));
}

QString MainWindow::trimNumber(double v)
{
    if (std::abs(v - std::round(v)) < 0.005) {
        return QString::number(static_cast<long long>(std::round(v)));
    }
    QString s = QString::number(v, 'f', 2);
    while (s.endsWith(QLatin1Char('0'))) s.chop(1);
    if (s.endsWith(QLatin1Char('.')))    s.chop(1);
    return s;
}

} // namespace QuadClicker
