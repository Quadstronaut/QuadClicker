#include "MainWindow.h"
#include "cli/CliEntryPoint.h"

#include <QApplication>
#include <QStyle>
#include <QPalette>
#include <QString>

/// Apply the dark Fusion palette matching the QuadClicker design system.
static void applyDarkPalette(QApplication& app)
{
    app.setStyle(QStringLiteral("Fusion"));

    QPalette p;
    // Window / dialog backgrounds
    p.setColor(QPalette::Window,          QColor(0x1A, 0x1A, 0x1A));
    p.setColor(QPalette::WindowText,      QColor(0xF0, 0xF0, 0xF0));
    p.setColor(QPalette::Base,            QColor(0x24, 0x24, 0x24));
    p.setColor(QPalette::AlternateBase,   QColor(0x2E, 0x2E, 0x2E));
    p.setColor(QPalette::ToolTipBase,     QColor(0x24, 0x24, 0x24));
    p.setColor(QPalette::ToolTipText,     QColor(0xF0, 0xF0, 0xF0));

    // Text
    p.setColor(QPalette::Text,            QColor(0xF0, 0xF0, 0xF0));
    p.setColor(QPalette::BrightText,      QColor(0xFF, 0xFF, 0xFF));
    p.setColor(QPalette::PlaceholderText, QColor(0x55, 0x55, 0x55));

    // Buttons
    p.setColor(QPalette::Button,          QColor(0x24, 0x24, 0x24));
    p.setColor(QPalette::ButtonText,      QColor(0xF0, 0xF0, 0xF0));

    // Highlight (#50C878 = accent green)
    p.setColor(QPalette::Highlight,       QColor(0x50, 0xC8, 0x78));
    p.setColor(QPalette::HighlightedText, QColor(0x1A, 0x1A, 0x1A));

    // Disabled state
    p.setColor(QPalette::Disabled, QPalette::WindowText,  QColor(0x55, 0x55, 0x55));
    p.setColor(QPalette::Disabled, QPalette::Text,        QColor(0x55, 0x55, 0x55));
    p.setColor(QPalette::Disabled, QPalette::ButtonText,  QColor(0x55, 0x55, 0x55));
    p.setColor(QPalette::Disabled, QPalette::Base,        QColor(0x1E, 0x1E, 0x1E));

    // Borders / mid-tones
    p.setColor(QPalette::Mid,             QColor(0x3A, 0x3A, 0x3A));
    p.setColor(QPalette::Dark,            QColor(0x3A, 0x3A, 0x3A));
    p.setColor(QPalette::Shadow,          QColor(0x0F, 0x0F, 0x0F));
    p.setColor(QPalette::Light,           QColor(0x2E, 0x2E, 0x2E));
    p.setColor(QPalette::Midlight,        QColor(0x2A, 0x2A, 0x2A));

    app.setPalette(p);
}

int main(int argc, char* argv[])
{
    // Determine mode before constructing QApplication so we can pick the
    // right application type (QCoreApplication vs QApplication).
    bool hasCliArgs   = false;
    bool startMinimized = false;

    for (int i = 1; i < argc; ++i) {
        QString arg = QString::fromUtf8(argv[i]);
        if (arg.compare(QLatin1String("--minimized"), Qt::CaseInsensitive) == 0) {
            startMinimized = true;
        } else {
            // Any argument other than --minimized triggers CLI mode
            hasCliArgs = true;
        }
    }

    if (hasCliArgs) {
        // CLI mode — CliEntryPoint creates its own QCoreApplication internally
        return QuadClicker::CliEntryPoint::run(argc, argv);
    }

    // ── GUI mode ───────────────────────────────────────────────────────────────
    QApplication app(argc, argv);
    app.setApplicationName(QStringLiteral("QuadClicker"));
    app.setApplicationVersion(QStringLiteral("1.0.0"));
    app.setOrganizationName(QStringLiteral("quadstronaut"));
    app.setOrganizationDomain(QStringLiteral("com.quadstronaut.quadclicker"));

    applyDarkPalette(app);

    // Keep the app alive when the main window is hidden (minimised to tray)
    app.setQuitOnLastWindowClosed(false);

    QuadClicker::MainWindow window;

    if (startMinimized) {
        // Start hidden in the tray — do not call show()
    } else {
        window.show();
    }

    return app.exec();
}
