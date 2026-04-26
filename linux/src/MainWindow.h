#pragma once

#include "core/ClickEngine.h"
#include "core/LocationPicker.h"
#include "core/TrayManager.h"
#include "core/HotkeyManager.h"
#include "models/AppSettings.h"
#include "models/ClickRateMode.h"

#include <QMainWindow>
#include <QLineEdit>
#include <QComboBox>
#include <QRadioButton>
#include <QCheckBox>
#include <QPushButton>
#include <QLabel>
#include <QButtonGroup>
#include <QCloseEvent>
#include <QEvent>
#include <QKeyEvent>

namespace QuadClicker {

class HotkeyEditFilter;

class MainWindow : public QMainWindow {
    Q_OBJECT

    friend class HotkeyEditFilter;

public:
    explicit MainWindow(QWidget* parent = nullptr);
    ~MainWindow() override;

protected:
    void closeEvent(QCloseEvent* ev) override;
    void changeEvent(QEvent* ev) override;

private slots:
    void onStartStopClicked();
    void onPickClicked();
    void onLocationPicked(int x, int y);
    void onPickCancelled();
    void onClickCountUpdated(int count);
    void onEngineStatusChanged(QuadClicker::EngineStatus status);
    void onAlwaysOnTopChanged(bool checked);
    void onEngineFinished();
    void onTrayShowWindow();
    void onTrayToggleClicking();
    void onTrayQuit();

private:
    // ── Build UI ──────────────────────────────────────────────────────────────
    void buildUi();
    QWidget* buildClickRateRow();
    QWidget* buildMouseButtonRow();
    QWidget* buildClickTypeRow();
    QWidget* buildLocationRow();
    QWidget* buildCoordinatesRow();
    QWidget* buildSectionSeparator(const QString& label);
    QWidget* buildStopConditionsRows();
    QWidget* buildAdvancedRow();
    QWidget* buildHotkeysRow();

    // ── Hotkey capture ────────────────────────────────────────────────────────
    void installHotkeyCaptureFilter(QLineEdit* edit);
    void onClickRateModeChanged();
    void onClickRateInputChanged();
    void populateUnitBox(const QString& desiredTag, const QString& fallbackTag);
    QString composeRateString() const;
    void updateRateHint();
    static QString formatRate(double cps);
    static QString formatDelay(double ms);
    static QString trimNumber(double v);
    bool hotkeyEditKeyPress(QLineEdit* edit, QKeyEvent* ev);
    static QString buildHotkeyText(Qt::Key key, Qt::KeyboardModifiers mods);
    bool tryParseHotkeyText(const QString& text, unsigned int& modifiers,
                             unsigned int& keysym) const;
    void reRegisterHotkeys();

    // ── Session / State ───────────────────────────────────────────────────────
    bool tryBuildSession(ClickSession& session, QString& error) const;
    void startClicking();
    void stopClicking();
    void setButtonState(bool isClicking);
    void setStatus(EngineStatus status);
    void showError(const QString& msg);
    void clearError();

    // ── Settings ──────────────────────────────────────────────────────────────
    void loadSettings();
    void saveSettings();

    // ── Restore from tray ─────────────────────────────────────────────────────
    void restoreFromTray();

    // ── Widgets ───────────────────────────────────────────────────────────────
    QLineEdit*    m_clickRateValueEdit{nullptr};
    QComboBox*    m_clickRateUnitBox{nullptr};
    QButtonGroup* m_rateModeGroup{nullptr};
    QRadioButton* m_modeDelay{nullptr};
    QRadioButton* m_modeFrequency{nullptr};
    QLabel*       m_rateHintLabel{nullptr};
    bool          m_rateUiReady{false};

    QButtonGroup* m_buttonGroup{nullptr};
    QRadioButton* m_btnLeft{nullptr};
    QRadioButton* m_btnRight{nullptr};
    QRadioButton* m_btnMiddle{nullptr};

    QButtonGroup* m_typeGroup{nullptr};
    QRadioButton* m_typeSingle{nullptr};
    QRadioButton* m_typeDouble{nullptr};

    QButtonGroup* m_locGroup{nullptr};
    QRadioButton* m_locCurrent{nullptr};
    QRadioButton* m_locFixed{nullptr};

    QLineEdit*    m_xEdit{nullptr};
    QLineEdit*    m_yEdit{nullptr};
    QPushButton*  m_pickBtn{nullptr};

    QLineEdit*    m_stopClicksEdit{nullptr};
    QLineEdit*    m_stopSecondsEdit{nullptr};

    QLineEdit*    m_idleEdit{nullptr};
    QCheckBox*    m_alwaysOnTopBox{nullptr};

    QLineEdit*    m_startHotkeyEdit{nullptr};
    QLineEdit*    m_stopHotkeyEdit{nullptr};

    QLabel*       m_errorLabel{nullptr};

    QLabel*       m_statusDot{nullptr};
    QLabel*       m_statusLabel{nullptr};
    QLabel*       m_clickCountLabel{nullptr};

    QPushButton*  m_startStopBtn{nullptr};

    // ── Services ──────────────────────────────────────────────────────────────
    ClickEngine*    m_engine{nullptr};
    LocationPicker* m_picker{nullptr};
    TrayManager*    m_tray{nullptr};
    HotkeyManager*  m_hotkeys{nullptr};

    // ── State ─────────────────────────────────────────────────────────────────
    bool            m_isClicking{false};
    int             m_startHotkeyHandle{-1};
    int             m_stopHotkeyHandle{-1};
    bool            m_quitting{false};

    // ── Settings ──────────────────────────────────────────────────────────────
    AppSettings     m_settings;
};

} // namespace QuadClicker
