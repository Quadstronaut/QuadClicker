// CI probe (QCLK-8): what can this runner do with input events?
import Cocoa
import ApplicationServices

print("AXIsProcessTrusted=\(AXIsProcessTrusted())")
print("CGPreflightListenEventAccess=\(CGPreflightListenEventAccess())")
print("CGPreflightPostEventAccess=\(CGPreflightPostEventAccess())")
print("NSScreen.count=\(NSScreen.screens.count)")
for s in NSScreen.screens { print("screen frame=\(s.frame)") }
print("mainDisplay bounds=\(CGDisplayBounds(CGMainDisplayID()))")
print("idle(any)=\(CGEventSource.secondsSinceLastEventType(.hidSystemState, eventType: CGEventType(rawValue: ~0)!))")
print("idle(null)=\(CGEventSource.secondsSinceLastEventType(.hidSystemState, eventType: .null))")
print("idle(mouseMoved)=\(CGEventSource.secondsSinceLastEventType(.hidSystemState, eventType: .mouseMoved))")
print("idle(leftDown)=\(CGEventSource.secondsSinceLastEventType(.hidSystemState, eventType: .leftMouseDown))")

final class Box { var downs: [Int64] = []; var ups = 0 }
let box = Box()
let mask: CGEventMask = (1 << CGEventType.leftMouseDown.rawValue) | (1 << CGEventType.leftMouseUp.rawValue)
let cb: CGEventTapCallBack = { _, type, event, ref in
    let b = Unmanaged<Box>.fromOpaque(ref!).takeUnretainedValue()
    if type == .leftMouseDown { b.downs.append(event.getIntegerValueField(.mouseEventClickState)) }
    if type == .leftMouseUp { b.ups += 1 }
    return Unmanaged.passUnretained(event)
}
for (name, loc) in [("session", CGEventTapLocation.cgSessionEventTap), ("hid", .cghidEventTap), ("annotated", .cgAnnotatedSessionEventTap)] {
    box.downs = []; box.ups = 0
    guard let tap = CGEvent.tapCreate(tap: loc, place: .headInsertEventTap, options: .listenOnly,
                                      eventsOfInterest: mask, callback: cb,
                                      userInfo: Unmanaged.passUnretained(box).toOpaque()) else {
        print("tapCreate(\(name))=FAILED"); continue
    }
    print("tapCreate(\(name))=OK")
    let src = CFMachPortCreateRunLoopSource(nil, tap, 0)!
    CFRunLoopAddSource(CFRunLoopGetCurrent(), src, .commonModes)
    CGEvent.tapEnable(tap: tap, enable: true)
    let p = CGPoint(x: 600, y: 400)
    for state in [Int64(1), 2] {
        let d = CGEvent(mouseEventSource: nil, mouseType: .leftMouseDown, mouseCursorPosition: p, mouseButton: .left)!
        let u = CGEvent(mouseEventSource: nil, mouseType: .leftMouseUp, mouseCursorPosition: p, mouseButton: .left)!
        d.setIntegerValueField(.mouseEventClickState, value: state)
        u.setIntegerValueField(.mouseEventClickState, value: state)
        d.post(tap: .cghidEventTap); u.post(tap: .cghidEventTap)
    }
    CFRunLoopRunInMode(.defaultMode, 1.0, false)
    print("observed(\(name)) downs=\(box.downs) ups=\(box.ups)")
    print("idle(any) after post=\(CGEventSource.secondsSinceLastEventType(.hidSystemState, eventType: CGEventType(rawValue: ~0)!))")
    CFRunLoopRemoveSource(CFRunLoopGetCurrent(), src, .commonModes)
}
