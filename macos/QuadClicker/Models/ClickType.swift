// Models/ClickType.swift
// QuadClicker — macOS

import Foundation

/// Whether each event is a single or double click.
enum ClickType: String, Codable, CaseIterable {
    case single = "single"
    case double_ = "double"   // raw value "double" matches Windows JSON

    var displayName: String {
        switch self {
        case .single:  return "Single"
        case .double_: return "Double"
        }
    }
}
