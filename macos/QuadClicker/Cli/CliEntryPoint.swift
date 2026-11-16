// Cli/CliEntryPoint.swift
// QuadClicker — macOS
//
// Full port of Windows CliEntryPoint.cs.
// Invoked from QuadClickerApp.swift when CLI arguments are present.

import Foundation

enum CliEntryPoint {

    // ── Exit codes (match Windows) ────────────────────────────────────────────
    static let exitSuccess:     Int32 = 0
    static let exitBadArgument: Int32 = 1
    static let exitRuntimeError: Int32 = 2
    static let exitInterrupted: Int32 = 130

    // ── Main entry point ──────────────────────────────────────────────────────

    static func run(args: [String]) -> Int32 {
        // args is already stripped of CommandLine.arguments[0] (the executable path) by the caller.
        let filteredArgs = args

        if filteredArgs.count == 1 {
            switch filteredArgs[0] {
            case "--help", "-h":
                printHelp()
                return exitSuccess
            case "--version", "-v":
                printVersion()
                return exitSuccess
            default:
                break
            }
        }

        switch parseArgs(filteredArgs) {
        case .failure(let error):
            fputs("Error: \(error)\nRun 'quadclicker --help' for usage.\n", stderr)
            return exitBadArgument

        case .success(let session):
            return runHeadless(session: session)
        }
    }

    // ── Headless execution ────────────────────────────────────────────────────

    private static func runHeadless(session: ClickSession) -> Int32 {
        let delayMs = Int(session.clickRate * 1000)
        let locStr = session.useCurrentPosition
            ? "cursor position"
            : "(\(session.x),\(session.y))"

        print("QuadClicker | \(delayMs)ms delay | \(session.button.rawValue) \(session.clickType.rawValue) click | \(locStr)")
        if session.stopAfterClicks  > 0 { print("  Stop after \(session.stopAfterClicks) clicks") }
        if session.stopAfterSeconds > 0 { print("  Stop after \(session.stopAfterSeconds)s") }
        if session.idleWaitSeconds  > 0 { print("  Wait for \(session.idleWaitSeconds)s idle") }
        print("Press Ctrl+C to stop.")

        let token = CancellationToken()

        // Handle Ctrl+C (SIGINT) through a dispatch source, not a C signal handler:
        // token.cancel() takes an NSLock and signals a semaphore, neither of which
        // is async-signal-safe (a signal landing while the click thread held the
        // lock could deadlock). The source runs the handler on a normal queue.
        signal(SIGINT, SIG_IGN)
        let sigintSource = DispatchSource.makeSignalSource(signal: SIGINT, queue: .global())
        sigintSource.setEventHandler { token.cancel() }
        sigintSource.resume()

        let engine = ClickEngine()
        var lastCount = 0

        engine.onClickCountUpdated = { count in
            lastCount = count
            if count % 50 == 0 {
                print("\rClicks: \(count)   ", terminator: "")
                fflush(stdout)
            }
        }
        engine.onStatusChanged = { status in
            switch status {
            case .waitingForIdle:
                print("\rWaiting for idle...   ", terminator: "")
                fflush(stdout)
            case .clicking:
                print("\rClicking...           ", terminator: "")
                fflush(stdout)
            case .stopped:
                break
            }
        }

        // Run synchronously (CLI mode is synchronous)
        let semaphore = DispatchSemaphore(value: 0)
        // A Task closure may not mutate captured local vars, so the outcome goes
        // into a reference box. The semaphore orders the write before the read.
        final class Outcome { var error: Error?; var wasCancelled = false }
        let outcome = Outcome()

        Task {
            do {
                try await engine.run(session: session, cancellationToken: token)
            } catch is CancellationError {
                outcome.wasCancelled = true
            } catch {
                outcome.error = error
            }
            semaphore.signal()
        }

        semaphore.wait()
        sigintSource.cancel()
        signal(SIGINT, SIG_DFL)

        if outcome.wasCancelled || token.isCancelled {
            print("\nStopped. Total clicks: \(lastCount)")
            return exitInterrupted
        }
        if let err = outcome.error {
            fputs("\nRuntime error: \(err.localizedDescription)\n", stderr)
            return exitRuntimeError
        }
        print("\nDone. Total clicks: \(lastCount)")
        return exitSuccess
    }

    // ── Argument parser ───────────────────────────────────────────────────────

    private static func parseArgs(_ args: [String]) -> Result<ClickSession, String> {
        var rate: TimeInterval = 0
        var button: MouseButton = .left
        var clickType: ClickType = .single
        var useCurrentPos = true
        var x = 0, y = 0
        var stopClicks = 0
        var stopSeconds: Double = 0
        var idleWait: Double = 0
        var hasRate = false

        var i = 0
        while i < args.count {
            let arg = args[i]

            func nextValue() -> String? {
                guard i + 1 < args.count else { return nil }
                i += 1
                return args[i]
            }

            switch arg.lowercased() {
            case "--rate":
                guard let val = nextValue() else {
                    return .failure("Missing value after '\(arg)'")
                }
                switch ClickRateParser.parse(val) {
                case .success(let r):
                    rate = r
                    hasRate = true
                case .failure(let err):
                    return .failure(err)
                }

            case "--button":
                guard let val = nextValue() else {
                    return .failure("Missing value after '\(arg)'")
                }
                switch val.lowercased() {
                case "left":   button = .left
                case "right":  button = .right
                case "middle": button = .middle
                default:
                    return .failure("Unknown button '\(val)'. Use: left, right, middle.")
                }

            case "--type":
                guard let val = nextValue() else {
                    return .failure("Missing value after '\(arg)'")
                }
                switch val.lowercased() {
                case "single": clickType = .single
                case "double": clickType = .double_
                default:
                    return .failure("Unknown click type '\(val)'. Use: single, double.")
                }

            case "--location":
                guard let val = nextValue() else {
                    return .failure("Missing value after '\(arg)'")
                }
                let parts = val.split(separator: ",").map { String($0).trimmingCharacters(in: .whitespaces) }
                guard parts.count == 2,
                      let px = Int(parts[0]),
                      let py = Int(parts[1])
                else {
                    return .failure("Location must be 'X,Y' (e.g. 500,300).")
                }
                x = px; y = py
                useCurrentPos = false

            case "--stop-after-clicks":
                guard let val = nextValue() else {
                    return .failure("Missing value after '\(arg)'")
                }
                // Negative, NaN or infinite limits would silently mean "unlimited".
                guard let n = Int(val), n >= 0 else {
                    return .failure("--stop-after-clicks must be a non-negative integer.")
                }
                stopClicks = n

            case "--stop-after-seconds":
                guard let val = nextValue() else {
                    return .failure("Missing value after '\(arg)'")
                }
                guard let n = Double(val), n.isFinite, n >= 0 else {
                    return .failure("--stop-after-seconds must be a non-negative number.")
                }
                stopSeconds = n

            case "--idle-wait":
                guard let val = nextValue() else {
                    return .failure("Missing value after '\(arg)'")
                }
                guard let n = Double(val), n.isFinite, n >= 0 else {
                    return .failure("--idle-wait must be a non-negative number.")
                }
                idleWait = n

            case "--no-gui", "--minimized":
                break // handled at the entry level

            default:
                return .failure("Unknown argument: '\(arg)'")
            }

            i += 1
        }

        guard hasRate else {
            return .failure("--rate is required in CLI mode.")
        }

        let session = ClickSession(
            clickRate: rate,
            button: button,
            clickType: clickType,
            useCurrentPosition: useCurrentPos,
            x: x,
            y: y,
            stopAfterClicks: stopClicks,
            stopAfterSeconds: stopSeconds,
            idleWaitSeconds: idleWait
        )
        return .success(session)
    }

    // ── Help / version ────────────────────────────────────────────────────────

    private static func printVersion() {
        let ver = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "1.0.0"
        print("QuadClicker \(ver)")
    }

    private static func printHelp() {
        print("""
        Usage: quadclicker [OPTIONS]

        When run without arguments, launches the GUI.

        Options:
          --rate <value>               Click rate. Formats: 100ms | 10/s | 600/min  [required in CLI mode]
          --button <left|right|middle> Mouse button to click (default: left)
          --type <single|double>       Click type (default: single)
          --location <x,y>             Fixed screen coordinate (default: current cursor)
          --stop-after-clicks <n>      Stop after N clicks, N >= 0 (0 = unlimited)
          --stop-after-seconds <n>     Stop after N seconds, N >= 0 (0 = unlimited)
          --idle-wait <n>              Wait for N seconds of system idle before starting (N >= 0)
          --no-gui                     Force headless mode
          --minimized                  Launch GUI minimized to tray
          --version                    Print version and exit 0
          --help                       Print this help and exit 0

        Exit codes:
          0   Success / clean stop
          1   Invalid argument
          2   Runtime error
          130 Ctrl+C / interrupted

        Examples:
          quadclicker --rate 10/s
          quadclicker --rate 10/s --location 500,300 --button right --stop-after-clicks 100
          quadclicker --rate 500ms --type double --stop-after-seconds 30
          quadclicker --rate 1ms --button middle --stop-after-clicks 50
        """)
    }
}
