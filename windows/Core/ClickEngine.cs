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
        var startTime = DateTime.UtcNow;

        var initialStatus = session.IdleWaitSeconds > 0
            ? EngineStatus.WaitingForIdle
            : EngineStatus.Clicking;
        StatusChanged?.Invoke(initialStatus);

        while (!token.IsCancellationRequested)
        {
            // ── Stop conditions ───────────────────────────────────────────────
            if (session.StopAfterClicks > 0 && clicks >= session.StopAfterClicks) break;
            if (session.StopAfterSeconds > 0 &&
                (DateTime.UtcNow - startTime).TotalSeconds >= session.StopAfterSeconds) break;

            // ── Idle wait ─────────────────────────────────────────────────────
            if (session.IdleWaitSeconds > 0)
            {
                var threshold = TimeSpan.FromSeconds(session.IdleWaitSeconds);
                while (IdleDetector.GetIdleTime() < threshold && !token.IsCancellationRequested)
                    token.WaitHandle.WaitOne(100);

                if (token.IsCancellationRequested) break;
                StatusChanged?.Invoke(EngineStatus.Clicking);
            }

            // ── Position ──────────────────────────────────────────────────────
            if (!session.UseCurrentPosition)
                NativeMethods.SetCursorPos(session.X, session.Y);

            // ── Click ─────────────────────────────────────────────────────────
            InputInjector.Click(session.Button, session.ClickType);
            ClickCountUpdated?.Invoke(++clicks);

            // ── Delay ─────────────────────────────────────────────────────────
            if (session.ClickRate.TotalMilliseconds >= 1)
                token.WaitHandle.WaitOne((int)session.ClickRate.TotalMilliseconds);
        }

        StatusChanged?.Invoke(EngineStatus.Stopped);
    }
}
