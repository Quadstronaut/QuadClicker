namespace QuadClicker.Models;

/// <summary>Immutable configuration for a single clicking session.</summary>
public sealed record ClickSession(
    TimeSpan     ClickRate,
    MouseButton  Button,
    ClickType    ClickType,
    bool         UseCurrentPosition,
    int          X,
    int          Y,
    int          StopAfterClicks,    // 0 = unlimited
    double       StopAfterSeconds,   // 0 = unlimited
    double       IdleWaitSeconds     // 0 = disabled
);
