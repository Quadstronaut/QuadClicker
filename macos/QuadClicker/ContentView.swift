// ContentView.swift
// QuadClicker — macOS
//
// Full SwiftUI view matching the Windows WPF layout.
// Design tokens (Taneth palette — deep-green hull with gold HUD accent):
//   Background      #0A1410   Surface          #13211C   SurfaceElevated #1B2E27
//   Border          #2D5448   TextPrimary      #E8DCB0   TextSecondary   #7A9088
//   TextDisabled    #3D5048   Accent           #E8B547   AccentHover     #F5C75A
//   AccentPressed   #B88A2A   AccentForeground #0A1410   Danger          #E04030
//   DangerHover     #C8331E   StatusWaiting    #5BA89A

import SwiftUI
import AppKit

// ── Design tokens ─────────────────────────────────────────────────────────────

extension Color {
    /// Initialise from a hex string, e.g. "#1A1A1A" or "1A1A1A".
    init(hex: String) {
        let h = hex.trimmingCharacters(in: CharacterSet(charactersIn: "#"))
        var rgb: UInt64 = 0
        Scanner(string: h).scanHexInt64(&rgb)
        let r = Double((rgb >> 16) & 0xFF) / 255
        let g = Double((rgb >>  8) & 0xFF) / 255
        let b = Double( rgb        & 0xFF) / 255
        self.init(red: r, green: g, blue: b)
    }
}

extension Color {
    static let qcBackground       = Color(hex: "#0A1410")
    static let qcSurface          = Color(hex: "#13211C")
    static let qcSurfaceElevated  = Color(hex: "#1B2E27")
    static let qcBorder           = Color(hex: "#2D5448")
    static let qcTextPrimary      = Color(hex: "#E8DCB0")
    static let qcTextSecondary    = Color(hex: "#7A9088")
    static let qcTextDisabled     = Color(hex: "#3D5048")
    static let qcAccent           = Color(hex: "#E8B547")
    static let qcAccentHover      = Color(hex: "#F5C75A")
    static let qcAccentPressed    = Color(hex: "#B88A2A")
    static let qcAccentForeground = Color(hex: "#0A1410")
    static let qcDanger           = Color(hex: "#E04030")
    static let qcDangerHover      = Color(hex: "#C8331E")
    static let qcStatusWaiting    = Color(hex: "#5BA89A")
}

// ── ViewModel ─────────────────────────────────────────────────────────────────

@MainActor
final class ClickerViewModel: ObservableObject {

    // ── Form fields ───────────────────────────────────────────────────────────
    @Published var clickRateValue: String = "100"
    @Published var clickRateUnit: String  = "ms"   // "ms" | "/s" | "/min"
    @Published var button: MouseButton    = .left
    @Published var clickType: ClickType   = .single
    @Published var useCurrentPosition: Bool = true
    @Published var xText: String  = "0"
    @Published var yText: String  = "0"
    @Published var stopClicksText:  String = "0"
    @Published var stopSecondsText: String = "0"
    @Published var idleWaitText:    String = "0"
    @Published var alwaysOnTop:     Bool   = false
    @Published var startHotkeyText: String = ""
    @Published var stopHotkeyText:  String = "F10"

    // ── Runtime state ─────────────────────────────────────────────────────────
    @Published var isClicking:  Bool         = false
    @Published var engineStatus: EngineStatus = .stopped
    @Published var clickCount:  Int?          = nil
    @Published var errorMessage: String?      = nil

    // ── Services ──────────────────────────────────────────────────────────────
    let engine         = ClickEngine()
    let locationPicker = LocationPicker()
    let trayManager    = TrayManager()
    let hotkeyManager  = HotkeyManager()

    private var cancellationToken: CancellationToken?
    private var startHotkeyToken: Any?
    private var stopHotkeyToken:  Any?

    // ── Settings ──────────────────────────────────────────────────────────────
    private let settings: AppSettings

    init(settings: AppSettings = AppState.shared.settings) {
        self.settings = settings
        loadSettings()
        setupCallbacks()
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    private func setupCallbacks() {
        engine.onClickCountUpdated = { [weak self] count in
            DispatchQueue.main.async { self?.clickCount = count }
        }
        engine.onStatusChanged = { [weak self] status in
            DispatchQueue.main.async { self?.engineStatus = status }
        }
        locationPicker.onLocationPicked = { [weak self] x, y in
            DispatchQueue.main.async {
                self?.xText = "\(x)"
                self?.yText = "\(y)"
            }
        }
        locationPicker.onCancelled = { /* nothing extra needed */ }

        trayManager.onShowWindow = { [weak self] in
            DispatchQueue.main.async { self?.showWindow() }
        }
        trayManager.onToggleClicking = { [weak self] in
            DispatchQueue.main.async { self?.startStop() }
        }
        trayManager.onQuit = {
            DispatchQueue.main.async { NSApp.terminate(nil) }
        }
    }

    // ── Start / Stop ──────────────────────────────────────────────────────────

    func startStop() {
        if isClicking { stopClicking() }
        else          { startClicking() }
    }

    func startClicking() {
        errorMessage = nil
        guard let session = buildSession() else { return }

        isClicking = true
        clickCount = nil
        trayManager.setActiveState(true)

        let token = CancellationToken()
        cancellationToken = token

        Task {
            do {
                try await engine.run(session: session, cancellationToken: token)
            } catch is CancellationError {
                // normal stop
            } catch {
                errorMessage = "Error: \(error.localizedDescription)"
            }
            // Always land here
            await MainActor.run {
                self.isClicking = false
                self.engineStatus = .stopped
                self.trayManager.setActiveState(false)
            }
        }
    }

    func stopClicking() {
        cancellationToken?.cancel()
        cancellationToken = nil
        isClicking = false
        engineStatus = .stopped
        trayManager.setActiveState(false)
    }

    // ── Build session ─────────────────────────────────────────────────────────

    /// Returns a ClickSession, or nil and sets errorMessage on failure.
    private func buildSession() -> ClickSession? {
        let combined = clickRateUnit == "ms"
            ? (clickRateValue + "ms")
            : (clickRateValue + clickRateUnit)

        let rate: TimeInterval
        switch ClickRateParser.parse(combined) {
        case .success(let r): rate = r
        case .failure(let e): errorMessage = e; return nil
        }

        guard let stopClicks = Int(stopClicksText), stopClicks >= 0 else {
            errorMessage = "Stop after (clicks) must be a non-negative integer."
            return nil
        }
        guard let stopSecs = Double(stopSecondsText), stopSecs >= 0 else {
            errorMessage = "Stop after (seconds) must be a non-negative number."
            return nil
        }
        guard let idle = Double(idleWaitText), idle >= 0 else {
            errorMessage = "Idle wait must be a non-negative number."
            return nil
        }

        var x = 0, y = 0
        if !useCurrentPosition {
            guard let px = Int(xText), let py = Int(yText) else {
                errorMessage = "X and Y coordinates must be valid integers."
                return nil
            }
            x = px; y = py
        }

        return ClickSession(
            clickRate: rate,
            button: button,
            clickType: clickType,
            useCurrentPosition: useCurrentPosition,
            x: x,
            y: y,
            stopAfterClicks: stopClicks,
            stopAfterSeconds: stopSecs,
            idleWaitSeconds: idle
        )
    }

    // ── Hotkeys ───────────────────────────────────────────────────────────────

    func reRegisterHotkeys() {
        if let t = startHotkeyToken { hotkeyManager.unregister(t); startHotkeyToken = nil }
        if let t = stopHotkeyToken  { hotkeyManager.unregister(t); stopHotkeyToken  = nil }

        if let spec = HotkeySpec.parse(startHotkeyText) {
            startHotkeyToken = hotkeyManager.register(spec: spec) { [weak self] in
                guard let self, !self.isClicking else { return }
                self.startClicking()
            }
        }
        if let spec = HotkeySpec.parse(stopHotkeyText) {
            stopHotkeyToken = hotkeyManager.register(spec: spec) { [weak self] in
                guard let self, self.isClicking else { return }
                self.stopClicking()
            }
        }
    }

    // ── Window management ─────────────────────────────────────────────────────

    func showWindow() {
        if let window = NSApp.windows.first {
            window.makeKeyAndOrderFront(nil)
            NSApp.activate(ignoringOtherApps: true)
        }
    }

    // ── Settings persistence ──────────────────────────────────────────────────

    func loadSettings() {
        clickRateValue      = settings.clickRateValue
        clickRateUnit       = settings.clickRateUnit
        button              = settings.button
        clickType           = settings.clickType
        useCurrentPosition  = settings.useCurrentPosition
        xText               = "\(settings.x)"
        yText               = "\(settings.y)"
        stopClicksText      = "\(settings.stopAfterClicks)"
        stopSecondsText     = "\(settings.stopAfterSeconds)"
        idleWaitText        = "\(settings.idleWaitSeconds)"
        alwaysOnTop         = settings.alwaysOnTop
        startHotkeyText     = settings.startHotkeyText
        stopHotkeyText      = settings.stopHotkeyText
        reRegisterHotkeys()
    }

    func saveSettings() {
        settings.clickRateValue     = clickRateValue
        settings.clickRateUnit      = clickRateUnit
        settings.button             = button
        settings.clickType          = clickType
        settings.useCurrentPosition = useCurrentPosition
        settings.x                  = Int(xText)  ?? 0
        settings.y                  = Int(yText)  ?? 0
        settings.stopAfterClicks    = Int(stopClicksText)   ?? 0
        settings.stopAfterSeconds   = Double(stopSecondsText) ?? 0
        settings.idleWaitSeconds    = Double(idleWaitText)    ?? 0
        settings.alwaysOnTop        = alwaysOnTop
        settings.startHotkeyText    = startHotkeyText
        settings.stopHotkeyText     = stopHotkeyText
        settings.save()
    }
}

// ── ContentView ───────────────────────────────────────────────────────────────

struct ContentView: View {
    @StateObject private var vm = ClickerViewModel()
    @EnvironmentObject private var appState: AppState

    // Hotkey capture state
    @State private var capturingHotkey: String? = nil  // "start" | "stop"

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            formContent
        }
        .frame(width: 420, height: 480)
        .background(Color.qcBackground)
        .foregroundColor(Color.qcTextPrimary)
        .onAppear {
            configureWindow()
            if appState.startMinimized {
                DispatchQueue.main.async {
                    NSApp.windows.first?.miniaturize(nil)
                }
            }
        }
        .onDisappear {
            vm.saveSettings()
        }
    }

    // ── Form ──────────────────────────────────────────────────────────────────

    private var formContent: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 6) {
                clickRateRow
                mouseButtonRow
                clickTypeRow
                locationRow
                coordinatesRow
                sectionSeparator("Stop Conditions")
                stopAfterClicksRow
                stopAfterSecondsRow
                sectionSeparator("Advanced")
                advancedRow
                hotkeysRow
                Spacer().frame(height: 4)
                errorRow
                statusRow
                Spacer().frame(height: 8)
                startStopButton
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 12)
        }
    }

    // ── Click Rate ────────────────────────────────────────────────────────────

    private var clickRateRow: some View {
        HStack {
            label("Click Rate:")
                .frame(width: 155, alignment: .leading)
            HStack(spacing: 6) {
                QCTextField(text: $vm.clickRateValue)
                    .frame(width: 80)
                    .help("Enter a number. Unit selected to the right.")
                Picker("", selection: $vm.clickRateUnit) {
                    Text("ms").tag("ms")
                    Text("/s").tag("/s")
                    Text("/min").tag("/min")
                }
                .pickerStyle(.menu)
                .frame(width: 80)
                .help("Unit for the click rate value")
                .background(Color.qcSurface)
                .cornerRadius(4)
            }
        }
    }

    // ── Mouse Button ──────────────────────────────────────────────────────────

    private var mouseButtonRow: some View {
        HStack {
            label("Mouse Button:")
                .frame(width: 155, alignment: .leading)
            HStack(spacing: 16) {
                ForEach(MouseButton.allCases, id: \.self) { btn in
                    radioButton(label: btn.displayName, isSelected: vm.button == btn) {
                        vm.button = btn
                    }
                }
            }
        }
    }

    // ── Click Type ────────────────────────────────────────────────────────────

    private var clickTypeRow: some View {
        HStack {
            label("Click Type:")
                .frame(width: 155, alignment: .leading)
            HStack(spacing: 16) {
                ForEach(ClickType.allCases, id: \.self) { ct in
                    radioButton(label: ct.displayName, isSelected: vm.clickType == ct) {
                        vm.clickType = ct
                    }
                }
            }
        }
    }

    // ── Location ──────────────────────────────────────────────────────────────

    private var locationRow: some View {
        HStack {
            label("Location:")
                .frame(width: 155, alignment: .leading)
            HStack(spacing: 16) {
                radioButton(label: "Current", isSelected: vm.useCurrentPosition) {
                    vm.useCurrentPosition = true
                }
                radioButton(label: "Fixed XY", isSelected: !vm.useCurrentPosition) {
                    vm.useCurrentPosition = false
                }
            }
        }
    }

    // ── Coordinates + Pick ────────────────────────────────────────────────────

    private var coordinatesRow: some View {
        HStack {
            label("Coordinates (X, Y):")
                .frame(width: 155, alignment: .leading)
            HStack(spacing: 6) {
                QCTextField(text: $vm.xText)
                    .frame(width: 52)
                    .disabled(vm.useCurrentPosition)
                    .help("X coordinate (pixels from left)")
                QCTextField(text: $vm.yText)
                    .frame(width: 52)
                    .disabled(vm.useCurrentPosition)
                    .help("Y coordinate (pixels from top)")
                Button("Pick…") {
                    if let window = NSApp.windows.first {
                        vm.locationPicker.beginPick(owner: window)
                    }
                }
                .disabled(vm.useCurrentPosition)
                .buttonStyle(SmallButtonStyle())
                .help("Click anywhere on screen to capture coordinates")
                .frame(width: 56)
            }
        }
    }

    // ── Stop Conditions ───────────────────────────────────────────────────────

    private var stopAfterClicksRow: some View {
        HStack {
            label("After clicks:")
                .frame(width: 155, alignment: .leading)
            QCTextField(text: $vm.stopClicksText)
                .help("Stop after this many clicks (0 = unlimited)")
        }
    }

    private var stopAfterSecondsRow: some View {
        HStack {
            label("After seconds:")
                .frame(width: 155, alignment: .leading)
            QCTextField(text: $vm.stopSecondsText)
                .help("Stop after this many seconds (0 = unlimited)")
        }
    }

    // ── Advanced ──────────────────────────────────────────────────────────────

    private var advancedRow: some View {
        HStack {
            label("Idle wait (seconds):")
                .frame(width: 155, alignment: .leading)
            HStack(spacing: 14) {
                QCTextField(text: $vm.idleWaitText)
                    .frame(width: 60)
                    .help("Wait this many seconds of system idle before starting (0 = disabled)")
                Toggle("Always on top", isOn: $vm.alwaysOnTop)
                    .toggleStyle(.checkbox)
                    .foregroundColor(Color.qcTextPrimary)
                    .onChange(of: vm.alwaysOnTop) { newValue in
                        NSApp.windows.first?.level = newValue ? .floating : .normal
                    }
            }
        }
    }

    // ── Hotkeys ───────────────────────────────────────────────────────────────

    private var hotkeysRow: some View {
        HStack {
            label("Hotkeys:")
                .frame(width: 155, alignment: .leading)
            HStack(spacing: 10) {
                Text("Start:")
                    .font(.system(size: 12))
                    .foregroundColor(Color.qcTextSecondary)
                hotkeyField(text: $vm.startHotkeyText, id: "start")
                    .frame(width: 60)
                Text("Stop:")
                    .font(.system(size: 12))
                    .foregroundColor(Color.qcTextSecondary)
                hotkeyField(text: $vm.stopHotkeyText, id: "stop")
                    .frame(width: 60)
            }
        }
    }

    // ── Error label ───────────────────────────────────────────────────────────

    @ViewBuilder
    private var errorRow: some View {
        if let msg = vm.errorMessage {
            Text(msg)
                .font(.system(size: 12))
                .foregroundColor(Color.qcDanger)
                .fixedSize(horizontal: false, vertical: true)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
    }

    // ── Status row ────────────────────────────────────────────────────────────

    private var statusRow: some View {
        HStack(spacing: 6) {
            Circle()
                .fill(statusDotColor)
                .frame(width: 8, height: 8)
            Text(statusText)
                .font(.system(size: 12))
                .foregroundColor(statusTextColor)
            if let count = vm.clickCount, vm.isClicking {
                Text("Clicks: \(count)")
                    .font(.system(size: 12))
                    .foregroundColor(Color.qcTextSecondary)
                    .padding(.leading, 10)
            }
            Spacer()
        }
    }

    private var statusDotColor: Color {
        switch vm.engineStatus {
        case .clicking:        return .qcAccent
        case .waitingForIdle:  return .qcStatusWaiting
        case .stopped:         return .qcTextDisabled
        }
    }

    private var statusText: String {
        switch vm.engineStatus {
        case .clicking:        return "Clicking"
        case .waitingForIdle:  return "Waiting for idle\u{2026}"
        case .stopped:         return "Stopped"
        }
    }

    private var statusTextColor: Color {
        switch vm.engineStatus {
        case .clicking:        return .qcAccent
        case .waitingForIdle:  return .qcStatusWaiting
        case .stopped:         return .qcTextSecondary
        }
    }

    // ── Start / Stop Button ───────────────────────────────────────────────────

    private var startStopButton: some View {
        Button(vm.isClicking ? "STOP" : "START") {
            vm.startStop()
        }
        .buttonStyle(MainActionButtonStyle(isDanger: vm.isClicking))
        .frame(maxWidth: .infinity)
        .frame(height: 38)
    }

    // ── Window setup ──────────────────────────────────────────────────────────

    private func configureWindow() {
        guard let window = NSApp.windows.first else { return }
        window.title = "QuadClicker"
        window.styleMask = [.titled, .closable, .miniaturizable]
        window.isMovableByWindowBackground = false
        window.level = vm.alwaysOnTop ? .floating : .normal
        window.backgroundColor = NSColor(Color.qcBackground)
        window.setContentSize(NSSize(width: 420, height: 480))
        window.center()
    }

    // ── Helper builders ───────────────────────────────────────────────────────

    private func label(_ text: String) -> some View {
        Text(text)
            .font(.system(size: 13))
            .foregroundColor(Color.qcTextPrimary)
    }

    private func radioButton(label: String, isSelected: Bool, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            HStack(spacing: 4) {
                Image(systemName: isSelected ? "largecircle.fill.circle" : "circle")
                    .foregroundColor(isSelected ? Color.qcAccent : Color.qcTextSecondary)
                    .font(.system(size: 13))
                Text(label)
                    .font(.system(size: 13))
                    .foregroundColor(Color.qcTextPrimary)
            }
        }
        .buttonStyle(.plain)
    }

    private func sectionSeparator(_ title: String) -> some View {
        ZStack(alignment: .leading) {
            Divider()
                .background(Color.qcBorder)
            Text(title)
                .font(.system(size: 11))
                .foregroundColor(Color.qcTextSecondary)
                .padding(.horizontal, 8)
                .background(Color.qcBackground)
                .padding(.leading, 8)
        }
        .padding(.vertical, 4)
    }

    // ── Hotkey capture field ──────────────────────────────────────────────────

    private func hotkeyField(text: Binding<String>, id: String) -> some View {
        HotkeyTextField(
            text: text,
            isCapturing: capturingHotkey == id,
            onFocus: { capturingHotkey = id },
            onLoseFocus: { capturingHotkey = nil },
            onKeyCaptured: { newText in
                let other = id == "start" ? vm.stopHotkeyText : vm.startHotkeyText
                if newText == other && !newText.isEmpty {
                    vm.errorMessage = "Start and stop hotkeys cannot be the same."
                } else {
                    vm.errorMessage = nil
                    text.wrappedValue = newText
                    vm.reRegisterHotkeys()
                }
                capturingHotkey = nil
            }
        )
    }
}

// ── Custom text field component ───────────────────────────────────────────────

struct QCTextField: View {
    @Binding var text: String

    var body: some View {
        TextField("", text: $text)
            .textFieldStyle(.plain)
            .font(.system(size: 13))
            .foregroundColor(Color.qcTextPrimary)
            .padding(.horizontal, 6)
            .padding(.vertical, 4)
            .background(Color.qcSurface)
            .cornerRadius(4)
            .overlay(
                RoundedRectangle(cornerRadius: 4)
                    .stroke(Color.qcBorder, lineWidth: 1)
            )
    }
}

// ── Hotkey capture text field ─────────────────────────────────────────────────

struct HotkeyTextField: NSViewRepresentable {
    @Binding var text: String
    var isCapturing: Bool
    var onFocus: () -> Void
    var onLoseFocus: () -> Void
    var onKeyCaptured: (String) -> Void

    func makeNSView(context: Context) -> NSTextField {
        let field = HotkeyNSTextField()
        field.delegate = context.coordinator
        field.isEditable = false
        field.isSelectable = false
        field.drawsBackground = true
        field.backgroundColor = NSColor(Color.qcSurface)
        field.textColor = NSColor(Color.qcTextPrimary)
        field.font = .systemFont(ofSize: 13)
        field.isBordered = true
        field.bezelStyle = .roundedBezel
        field.focusRingType = .none
        field.onKeyDown = context.coordinator.handleKeyDown
        return field
    }

    func updateNSView(_ nsView: NSTextField, context: Context) {
        if nsView.stringValue != text {
            nsView.stringValue = text
        }
        nsView.layer?.borderColor = isCapturing
            ? NSColor(Color.qcAccent).cgColor
            : NSColor(Color.qcBorder).cgColor
    }

    func makeCoordinator() -> Coordinator { Coordinator(self) }

    final class Coordinator: NSObject, NSTextFieldDelegate {
        var parent: HotkeyTextField
        init(_ parent: HotkeyTextField) { self.parent = parent }

        func controlTextDidBeginEditing(_ obj: Notification) { parent.onFocus() }
        func controlTextDidEndEditing(_ obj: Notification)   { parent.onLoseFocus() }

        func handleKeyDown(event: NSEvent) -> Bool {
            guard parent.isCapturing else { return false }

            let code = event.keyCode
            // ESC → clear
            if code == 53 {
                parent.onKeyCaptured("")
                return true
            }
            // Ignore lone modifier keys
            let modOnly: Set<UInt16> = [54, 55, 56, 57, 58, 59, 60, 61, 62, 63]
            if modOnly.contains(code) { return true }

            let mods = event.modifierFlags.intersection([.control, .shift, .option, .command])
            var parts: [String] = []
            if mods.contains(.control) { parts.append("Ctrl") }
            if mods.contains(.shift)   { parts.append("Shift") }
            if mods.contains(.option)  { parts.append("Alt") }
            if mods.contains(.command) { parts.append("Cmd") }

            let keyName = keyNameForCode(code) ?? (event.charactersIgnoringModifiers?.uppercased() ?? "?")
            parts.append(keyName)
            parent.onKeyCaptured(parts.joined(separator: "+"))
            return true
        }
    }
}

// NSTextField subclass that can intercept keyDown before AppKit consumes it
private class HotkeyNSTextField: NSTextField {
    var onKeyDown: ((NSEvent) -> Bool)?

    override func keyDown(with event: NSEvent) {
        if onKeyDown?(event) == true { return }
        super.keyDown(with: event)
    }

    // Allow the field to become first responder to receive key events
    override var acceptsFirstResponder: Bool { true }
}

private func keyNameForCode(_ code: UInt16) -> String? {
    let map: [UInt16: String] = [
        122: "F1",  120: "F2",  99: "F3",   118: "F4",
        96:  "F5",  97: "F6",   98: "F7",   100: "F8",
        101: "F9",  109: "F10", 103: "F11", 111: "F12",
        105: "F13", 107: "F14", 113: "F15", 106: "F16",
        36: "Return", 53: "Escape", 51: "Delete", 48: "Tab",
        49: "Space",  115: "Home", 119: "End",
        116: "PageUp", 121: "PageDown",
        123: "Left", 124: "Right", 125: "Down", 126: "Up",
    ]
    return map[code]
}

// ── Button styles ─────────────────────────────────────────────────────────────

struct MainActionButtonStyle: ButtonStyle {
    var isDanger: Bool

    func makeBody(configuration: Configuration) -> some View {
        let base    = isDanger ? Color.qcDanger         : Color.qcAccent
        let pressed = isDanger ? Color.qcDangerHover   : Color.qcAccentPressed

        configuration.label
            .font(.system(size: 15, weight: .semibold))
            .foregroundColor(isDanger ? .white : Color.qcAccentForeground)
            .frame(maxWidth: .infinity)
            .frame(height: 38)
            .background(configuration.isPressed ? pressed : base)
            .cornerRadius(6)
            .animation(.easeInOut(duration: 0.08), value: configuration.isPressed)
    }
}

struct SmallButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 12))
            .foregroundColor(Color.qcTextPrimary)
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
            .background(configuration.isPressed ? Color.qcSurfaceElevated : Color.qcSurface)
            .cornerRadius(4)
            .overlay(
                RoundedRectangle(cornerRadius: 4)
                    .stroke(Color.qcBorder, lineWidth: 1)
            )
    }
}

