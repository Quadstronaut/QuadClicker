// Core/TrayManager.swift
// QuadClicker — macOS
//
// Manages the NSStatusItem (menu bar icon) lifecycle.
// Port of Windows TrayManager.cs.

import AppKit
import Foundation

final class TrayManager {

    // ── Callbacks ─────────────────────────────────────────────────────────────
    var onShowWindow: (() -> Void)?
    var onToggleClicking: (() -> Void)?
    var onQuit: (() -> Void)?

    // ── Private state ─────────────────────────────────────────────────────────
    private let statusItem: NSStatusItem
    private var menu: NSMenu!
    private var startStopMenuItem: NSMenuItem!

    // ── Init / Deinit ─────────────────────────────────────────────────────────

    init() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)

        setupButton()
        setupMenu()
    }

    deinit {
        NSStatusBar.system.removeStatusItem(statusItem)
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// Update the menu bar icon / tooltip to reflect active or idle state.
    func setActiveState(_ isClicking: Bool) {
        DispatchQueue.main.async { [weak self] in
            guard let self else { return }
            let title = isClicking ? "●" : "◎"
            self.statusItem.button?.title = title
            self.statusItem.button?.toolTip = isClicking ? "QuadClicker — Clicking" : "QuadClicker"
            self.startStopMenuItem.title = isClicking ? "Stop" : "Start / Stop"
        }
    }

    // ── Private setup ─────────────────────────────────────────────────────────

    private func setupButton() {
        guard let button = statusItem.button else { return }
        button.title = "◎"
        button.toolTip = "QuadClicker"
        // Allow both left click (show menu) and double-click (show window)
        button.target = self
        button.action = #selector(statusItemClicked(_:))
        button.sendAction(on: [.leftMouseUp, .rightMouseUp])
    }

    private func setupMenu() {
        menu = NSMenu()

        let showItem = NSMenuItem(
            title: "Show Window",
            action: #selector(showWindowAction),
            keyEquivalent: ""
        )
        showItem.target = self
        menu.addItem(showItem)

        startStopMenuItem = NSMenuItem(
            title: "Start / Stop",
            action: #selector(toggleClickingAction),
            keyEquivalent: ""
        )
        startStopMenuItem.target = self
        menu.addItem(startStopMenuItem)

        menu.addItem(.separator())

        let quitItem = NSMenuItem(
            title: "Quit",
            action: #selector(quitAction),
            keyEquivalent: ""
        )
        quitItem.target = self
        menu.addItem(quitItem)
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    @objc private func statusItemClicked(_ sender: NSStatusBarButton) {
        guard let event = NSApp.currentEvent else { return }
        if event.clickCount >= 2 {
            onShowWindow?()
        } else {
            statusItem.menu = menu
            statusItem.button?.performClick(nil)
            statusItem.menu = nil
        }
    }

    @objc private func showWindowAction() {
        onShowWindow?()
    }

    @objc private func toggleClickingAction() {
        onToggleClicking?()
    }

    @objc private func quitAction() {
        onQuit?()
    }
}
