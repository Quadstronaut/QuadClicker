// Core/LocationPicker.swift
// QuadClicker — macOS
//
// Shows a fullscreen transparent overlay. The user clicks to capture a coordinate;
// ESC cancels. The selection click is swallowed (not forwarded to the app underneath).
//
// Uses an NSWindow at .screenSaver level (above all normal windows) + a CGEventTap
// to intercept the mouse-down event before it reaches other applications.

import AppKit
import CoreGraphics
import Foundation

final class LocationPicker {

    // ── Callbacks (always delivered on main thread) ───────────────────────────
    var onLocationPicked: ((Int, Int) -> Void)?
    var onCancelled: (() -> Void)?

    // ── Private state ─────────────────────────────────────────────────────────
    private var overlayWindow: NSWindow?
    private var eventTap: CFMachPort?
    private var runLoopSource: CFRunLoopSource?
    private var localKeyMonitor: Any?
    private var delayWorkItem: DispatchWorkItem?

    deinit {
        cleanup()
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// Begin a pick operation. `owner` is minimised first (matches Windows behaviour).
    func beginPick(owner: NSWindow) {
        // Cancel any in-progress delayed start
        delayWorkItem?.cancel()

        // Minimise the main window
        DispatchQueue.main.async { owner.miniaturize(nil) }

        let work = DispatchWorkItem { [weak self] in
            guard let self else { return }
            self.showOverlay(owner: owner)
        }
        delayWorkItem = work
        // 300 ms delay so the window finishes minimising before the overlay appears
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3, execute: work)
    }

    // ── Overlay ───────────────────────────────────────────────────────────────

    private func showOverlay(owner: NSWindow) {
        // Cover every screen
        let frame = NSScreen.screens.reduce(CGRect.null) { $0.union($1.frame) }

        let window = NSWindow(
            contentRect: frame,
            styleMask: .borderless,
            backing: .buffered,
            defer: false
        )
        window.backgroundColor = NSColor(white: 0, alpha: 0.01) // nearly transparent
        window.level = .screenSaver                               // above everything
        window.isOpaque = false
        window.ignoresMouseEvents = false
        window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        window.cursor = .crosshair

        // Instruction label
        let label = NSTextField(labelWithString: "Click to select location  |  ESC to cancel")
        label.textColor = .white
        label.backgroundColor = NSColor(white: 0.08, alpha: 0.85)
        label.font = NSFont.systemFont(ofSize: 14)
        label.isBordered = false
        label.isEditable = false
        label.sizeToFit()

        let container = NSView(frame: frame)
        container.wantsLayer = true
        container.layer?.backgroundColor = NSColor.clear.cgColor

        // Position label near top-centre
        let labelFrame = CGRect(
            x: (frame.width - label.frame.width - 28) / 2,
            y: frame.height - 90,
            width: label.frame.width + 28,
            height: label.frame.height + 16
        )
        label.frame = labelFrame
        container.addSubview(label)
        window.contentView = container

        overlayWindow = window
        window.makeKeyAndOrderFront(nil)

        // ESC via local key monitor on the overlay window
        localKeyMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            guard let self else { return event }
            if event.keyCode == 53 { // ESC
                self.cancelPick(owner: owner)
                return nil // swallow
            }
            return event
        }

        // CGEventTap to intercept the left-mouse-down click
        installMouseTap(owner: owner)
    }

    // ── CGEventTap ────────────────────────────────────────────────────────────

    private func installMouseTap(owner: NSWindow) {
        let mask: CGEventMask = (1 << CGEventType.leftMouseDown.rawValue)

        // We need a reference to self inside the C callback; use a retained pointer
        let selfPtr = Unmanaged.passRetained(self).toOpaque()

        let tap = CGEvent.tapCreate(
            tap: .cghidEventTap,
            place: .headInsertEventTap,
            options: .defaultTap,
            eventsOfInterest: mask,
            callback: { proxy, type, event, userInfo -> Unmanaged<CGEvent>? in
                guard let userInfo else { return Unmanaged.passRetained(event) }
                let picker = Unmanaged<LocationPicker>.fromOpaque(userInfo).takeUnretainedValue()
                return picker.handleMouseEvent(proxy: proxy, type: type, event: event)
            },
            userInfo: selfPtr
        )

        guard let tap else {
            // Accessibility permission not granted — clean up
            Unmanaged<LocationPicker>.fromOpaque(selfPtr).release()
            DispatchQueue.main.async { [weak self] in
                self?.cancelPick(owner: owner)
            }
            return
        }

        eventTap = tap
        // Store the owner reference in the closure via a captured property
        _pendingPickOwner = owner
        _selfPtrForRelease = selfPtr

        let source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
        runLoopSource = source
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
    }

    // Stored so we can access them during tap callback
    private var _pendingPickOwner: NSWindow?
    private var _selfPtrForRelease: UnsafeMutableRawPointer?

    private func handleMouseEvent(
        proxy: CGEventTapProxy,
        type: CGEventType,
        event: CGEvent
    ) -> Unmanaged<CGEvent>? {
        guard type == .leftMouseDown else {
            return Unmanaged.passRetained(event)
        }

        let pos = event.location
        let x = Int(pos.x)
        let y = Int(pos.y)

        // Remove tap before dispatching (prevents re-entry)
        removeTap()

        DispatchQueue.main.async { [weak self] in
            guard let self else { return }
            self.overlayWindow?.close()
            self.overlayWindow = nil
            if let owner = self._pendingPickOwner {
                owner.deminiaturize(nil)
                owner.makeKeyAndOrderFront(nil)
            }
            self._pendingPickOwner = nil
            self.removeLocalKeyMonitor()
            self.onLocationPicked?(x, y)
        }

        return nil // swallow the click
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private func cancelPick(owner: NSWindow) {
        cleanup()
        DispatchQueue.main.async {
            owner.deminiaturize(nil)
            owner.makeKeyAndOrderFront(nil)
        }
        onCancelled?()
    }

    private func removeTap() {
        if let tap = eventTap {
            CGEvent.tapEnable(tap: tap, enable: false)
            if let src = runLoopSource {
                CFRunLoopRemoveSource(CFRunLoopGetMain(), src, .commonModes)
            }
            eventTap = nil
            runLoopSource = nil
        }
        if let ptr = _selfPtrForRelease {
            Unmanaged<LocationPicker>.fromOpaque(ptr).release()
            _selfPtrForRelease = nil
        }
    }

    private func removeLocalKeyMonitor() {
        if let m = localKeyMonitor {
            NSEvent.removeMonitor(m)
            localKeyMonitor = nil
        }
    }

    private func cleanup() {
        delayWorkItem?.cancel()
        delayWorkItem = nil
        removeTap()
        removeLocalKeyMonitor()
        DispatchQueue.main.async { [weak self] in
            self?.overlayWindow?.close()
            self?.overlayWindow = nil
        }
    }
}
