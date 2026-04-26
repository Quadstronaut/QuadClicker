# Click Rate Redesign — Design Spec

**Date:** 2026-04-25
**Scope:** Windows app (`windows/`). macOS and Linux tracks unchanged.
**Status:** Approved for implementation.

## Problem

The current Click Rate input mixes two paradigms (delay-per-click vs. clicks-per-time-unit) into a single value+unit row:

```
Click Rate:  [100] [ms ▾]      ← unit dropdown contains: ms, /s, /min
```

Users have to mentally translate: "I want 10 clicks/sec" → "that's 100 ms" — or rely on the parser to accept either. The current ms default also implies a frame-of-reference (delay) that not all users share. The dropdown silently mixes `ms` (a duration) with `/s` and `/min` (rates), which is the same kind of category error as putting "miles" and "miles per hour" in the same dropdown.

## Goal

Split the control into a **Mode** (Delay vs. Frequency) chosen by radio button, and surface only the units valid for that mode. Show a live conversion hint so users build intuition without having to think in two paradigms at once. Add a "very fast" warning before users land on rates that misbehave on most apps.

## Non-goals

- Not redesigning any other field (mouse button, click type, location, stop conditions, hotkeys).
- Not changing macOS or Linux apps.
- Not changing the click engine or input injection.
- Not breaking existing CLI scripts.

## Design

### 1. UX / Layout (`MainWindow.xaml`)

Replace the single Click Rate row with a two-row block:

```
Click Rate:   ◉ Delay   ○ Frequency
              [ 100  ] [ ms     ▾ ]
              ≈ 10 clicks/sec
```

Toggling to **Frequency**:
```
Click Rate:   ○ Delay   ◉ Frequency
              [ 10   ] [ per sec ▾ ]
              ≈ 100 ms between clicks
```

**Behavior**

- Mode radios live on the same row as the `Click Rate:` label, on the right, in a horizontal `StackPanel`.
- The value `TextBox` is fixed-width (~80px), the unit `ComboBox` (~110px) follows.
- Toggling Mode swaps the dropdown's items and re-validates the current value against the new mode's bounds. If out of range for the new mode, the value is clamped (silent — not an error).
- The hint line below uses `TextSecondary` color, 11px, single line.
- When the effective rate is **> 100 clicks/second** (delay < 10ms), the hint switches to `Danger` color and shows `⚠ Very fast — input may not register reliably` followed by the conversion (`≈ 50 ms between clicks`) on the same wrapped block.
- Numeric value box: positive numbers only. Decimals allowed for `sec`, `min`, `per min`, `per hour`. Integer-only enforced for `ms` and `per sec`.
- Empty / zero / out-of-range hard-rejects only on Start (existing inline `ErrorLabel`) — the live hint just gives feedback.
- Window height grows ~30px (one extra row). Stays within `MinHeight=430`.

**Units exposed**

| Mode | Dropdown items (Tag values) | Bounds |
|---|---|---|
| Delay | `ms`, `sec`, `min` | 1 ms – 360 min |
| Frequency | `per sec`, `per min`, `per hour` | 1 / hour – 1000 / sec |

Hard floor is **1 ms** (= 1000/sec). Hard ceiling for delay is **360 min** (= 6 hours). These bounds are enforced in the parser, not just the UI.

### 2. Parser (`Core/ClickRateParser.cs`)

**Additive only** — every existing format keeps working. New unit tokens:

| New token forms | Parses to |
|---|---|
| `s`, `sec`, `seconds`, `secs` (suffix) | seconds → ms |
| `m`, `min`, `minutes`, `mins` (suffix) | minutes → ms |
| `/h`, `cph`, `times per hour` | per hour → ms delay |

**Disambiguation**: `m` alone is ambiguous (minutes vs. milliseconds-prefix-typo) — accept it as **minutes**, since `ms` is parsed first. Order of suffix checks: `ms` → `min`/`m` → `sec`/`s` → `/h`/`cph` → `/min`/`cpm` → `/s`/`cps` → bare integer.

**Bounds enforcement** (all paths, not just new ones):

```
const long MaxDelayMs = 360L * 60_000;  // 360 minutes
const double MinDelayMs = 1.0;
```

If parsed delay falls outside `[MinDelayMs, MaxDelayMs]`, return `false` with a clear error message.

**Public surface unchanged**:
```csharp
public static bool TryParse(string text, out TimeSpan delay, out string error)
```

CLI consumers and existing tests continue to work.

### 3. Settings (`Models/AppSettings.cs`)

**New schema**:
```csharp
public ClickRateMode ClickRateMode    { get; set; } = ClickRateMode.Delay;
public string        ClickRateValue   { get; set; } = "100";
public string        ClickRateUnit    { get; set; } = "ms";   // canonical Tag value
```

**New enum** in `Models/ClickRateMode.cs`:
```csharp
public enum ClickRateMode { Delay, Frequency }
```

**Canonical unit tags** (stored as strings for forward-compat, enum'd at runtime):
- Delay: `"ms"`, `"sec"`, `"min"`
- Frequency: `"per_sec"`, `"per_min"`, `"per_hour"`

**Migration** (handled in `AppSettings.Load()`):
- After deserialization, if `ClickRateMode` is missing/default AND the old `ClickRateUnit` is one of the legacy values:
  - `"ms"` → Mode=Delay, Unit=`"ms"`
  - `"/s"` → Mode=Frequency, Unit=`"per_sec"`
  - `"/min"` → Mode=Frequency, Unit=`"per_min"`
- Migration runs once; the next `Save()` writes the new schema and the legacy unit string is overwritten.
- Detection: a `[JsonIgnore]` flag isn't needed; we detect by checking whether the deserialized `ClickRateUnit` is one of the legacy values that no longer match the new canonical set.

### 4. Code-behind (`MainWindow.xaml.cs`)

New private state: none (all state lives on the controls).

**New handlers**:
- `ClickRateMode_Changed` — fires when either Mode radio toggles. Repopulates `ClickRateUnitBox` items, restores selection from the matching settings unit (or the first item), and recomputes the hint.
- `ClickRateValueBox_TextChanged` and `ClickRateUnitBox_SelectionChanged` — recompute the hint by calling the existing `ClickRateParser.TryParse` against the composed string and rendering a counterpart-side view.

**Hint rendering helper** (private static):
```csharp
private void UpdateRateHint()
{
    if (!ClickRateParser.TryParse(ComposedRateText, out var delay, out _))
    {
        RateHintLabel.Text = string.Empty;
        return;
    }
    double cps = 1000.0 / delay.TotalMilliseconds;
    bool isDelayMode = ModeDelay.IsChecked == true;
    string conversion = isDelayMode ? FormatRate(cps) : FormatDelay(delay);
    bool veryFast = cps > 100;
    RateHintLabel.Foreground = veryFast ? DangerBrush : TextSecondaryBrush;
    RateHintLabel.Text = veryFast
        ? $"⚠ Very fast — input may not register reliably  (≈ {conversion})"
        : $"≈ {conversion}";
}
```

Existing `TryBuildSession` updates: composes `value + unit` differently per mode (e.g., `"10per_sec"` → translate `per_sec` → `/s` for parser input, since the parser still speaks the old DSL externally). Or simpler: have a small `ComposeRateString(mode, value, unitTag)` helper that maps internal tag → parser-compatible suffix.

**Persistence**: `SaveSettings()` records `ClickRateMode`, the numeric `ClickRateValue`, and the canonical unit tag.

### 5. Tests

Add to `Tests/ClickRateParserTests.cs`:
- `Seconds_ValidInput` — `"5s"`, `"5 sec"`, `"5seconds"` → 5000ms.
- `Minutes_ValidInput` — `"2min"`, `"2 minutes"`, `"2m"` → 120000ms.
- `PerHour_ValidInput` — `"60/h"`, `"60cph"`, `"60 times per hour"` → 60000ms.
- `MaxDelay_AcceptedAtBoundary` — `"360min"` parses; `"361min"` rejects.
- `MaxRate_AcceptedAtBoundary` — `"1000/s"` parses (= 1ms); `"1001/s"` rejects.
- `MinDelay_RejectsBelowOne` — `"0.5ms"` rejects.

New `Tests/AppSettingsMigrationTests.cs`:
- Old JSON with `"ClickRateUnit": "/s"` and no `ClickRateMode` field → loads as Frequency / per_sec.
- Old JSON with `"/min"` → Frequency / per_min.
- Old JSON with `"ms"` → Delay / ms.
- New JSON round-trips unchanged.

## Out-of-scope risks accepted

- **Multi-monitor / DPI**: untouched.
- **Localization**: hint text is English-only (matches the rest of the app).
- **Accessibility**: radio buttons + textbox + combobox are all natively keyboard-accessible. No new accessibility work needed.

## Rollback

Single-revert friendly. Settings migration is forward-only; users who downgrade get default Delay/100/ms — minor inconvenience, no corruption.
