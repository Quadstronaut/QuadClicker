using System.Diagnostics;
using QuadClicker.Models;
using QuadClicker.PInvoke;

namespace QuadClicker.Core;

public enum EngineStatus { Stopped, WaitingForIdle, Clicking }

/// <summary>
/// Runs the click loop on a background thread.
/// All events are raised on the background thread — callers must marshal to the UI thread if needed.
/// </summary>
public sealed class ClickEngine
{
    public event Action<int>? ClickCountUpdated;
    public event Action<EngineStatus>? StatusChanged;

    public async Task RunAsync(ClickSession session, CancellationToken token)
    {
        await Task.Run(() => Loop(session, token), CancellationToken.None);
    }

    private void Loop(ClickSession session, CancellationToken token)
    {
        int clicks = 0;

        // ── Idle wait (once, before the click loop) ───────────────────────────
        if (session.IdleWaitSeconds > 0)
        {
            StatusChanged?.Invoke(EngineStatus.WaitingForIdle);
            var threshold = TimeSpan.FromSeconds(session.IdleWaitSeconds);
            while (IdleDetector.GetIdleTime() < threshold && !token.IsCancellationRequested)
                token.WaitHandle.WaitOne(100);

            if (token.IsCancellationRequested)
            {
                StatusChanged?.Invoke(EngineStatus.Stopped);
                return;
            }
        }

        StatusChanged?.Invoke(EngineStatus.Clicking);

        // Stopwatch is monotonic: wall-clock changes (NTP, manual) cannot stretch
        // or cut the stop-after-seconds limit.
        var clock    = Stopwatch.StartNew();
        var deadline = TimeSpan.Zero; // the first click is due immediately

        while (!token.IsCancellationRequested)
        {
            // ── Stop conditions ───────────────────────────────────────────────
            if (ClickTiming.LimitReached(clicks, session.StopAfterClicks, clock.Elapsed, session.StopAfterSeconds))
                break;

            // ── Position ──────────────────────────────────────────────────────
            if (!session.UseCurrentPosition)
                NativeMethods.SetCursorPos(session.X, session.Y);

            // ── Click ─────────────────────────────────────────────────────────
            InputInjector.Click(session.Button, session.ClickType, token);
            ClickCountUpdated?.Invoke(++clicks);

            // Finish as soon as the last allowed click is sent, not one period later.
            if (ClickTiming.LimitReached(clicks, session.StopAfterClicks, clock.Elapsed, session.StopAfterSeconds))
                break;

            // ── Delay ─────────────────────────────────────────────────────────
            // Wait for the next grid deadline, capped at the stop-after-seconds limit.
            deadline = ClickTiming.NextDeadline(deadline, session.ClickRate, clock.Elapsed);
            int waitMs = ClickTiming.WaitMs(clock.Elapsed, deadline, session.StopAfterSeconds);
            if (waitMs > 0)
                token.WaitHandle.WaitOne(waitMs);
        }

        StatusChanged?.Invoke(EngineStatus.Stopped);
    }
}
