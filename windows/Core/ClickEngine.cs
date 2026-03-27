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
        var startTime = DateTime.UtcNow;

        while (!token.IsCancellationRequested)
        {
            // ── Stop conditions ───────────────────────────────────────────────
            if (session.StopAfterClicks > 0 && clicks >= session.StopAfterClicks) break;
            if (session.StopAfterSeconds > 0 &&
                (DateTime.UtcNow - startTime).TotalSeconds >= session.StopAfterSeconds) break;

            // ── Position ──────────────────────────────────────────────────────
            if (!session.UseCurrentPosition)
                NativeMethods.SetCursorPos(session.X, session.Y);

            // ── Click ─────────────────────────────────────────────────────────
            InputInjector.Click(session.Button, session.ClickType, token);
            ClickCountUpdated?.Invoke(++clicks);

            // ── Delay ─────────────────────────────────────────────────────────
            if (session.ClickRate.TotalMilliseconds >= 1)
                token.WaitHandle.WaitOne((int)session.ClickRate.TotalMilliseconds);
        }

        StatusChanged?.Invoke(EngineStatus.Stopped);
    }
}
