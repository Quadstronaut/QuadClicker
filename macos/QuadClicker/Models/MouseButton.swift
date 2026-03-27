// Models/MouseButton.swift
// QuadClicker — macOS

import Foundation

/// Mouse button to click.
enum MouseButton: String, Codable, CaseIterable {
    case left   = "left"
    case right  = "right"
    case middle = "middle"

    var displayName: String {
        switch self {
        case .left:   return "Left"
        case .right:  return "Right"
        case .middle: return "Middle"
        }
    }
}
