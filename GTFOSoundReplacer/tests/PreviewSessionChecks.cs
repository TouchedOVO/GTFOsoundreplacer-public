using System;
using System.Collections.Generic;
using System.Threading;
using GTFOSoundReplacer.Audio;

public static class PreviewSessionChecks
{
    private sealed class Backend : IPreviewBackend
    {
        internal readonly List<Action> Callbacks = new List<Action>();
        internal readonly List<uint> Events = new List<uint>();
        internal readonly List<uint> Stopped = new List<uint>();
        internal bool SyncEnd, FailPost, FailStop, ZeroPost, FailClose, LastNative;
        internal int Released, Closed;
        public uint Post(PreviewRequest request, Action ended)
        {
            Events.Add(request.EventId); Callbacks.Add(ended);
            if (FailPost) throw new InvalidOperationException("post fixture");
            if (SyncEnd) ended();
            return ZeroPost ? 0u : (uint)Events.Count;
        }
        public void Stop(uint id) { Stopped.Add(id); if (FailStop) throw new InvalidOperationException("stop fixture"); }
        public void PlaybackEnded() { Released++; }
        public void Close(bool native) { Closed++; LastNative = native; if (FailClose) throw new Exception("close fixture"); }
    }
    private static int Count;
    private static long Now;
    private static PreviewRequest R(uint id) => new PreviewRequest(id, 0, 0, 0, 1, false, "fixture");
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception("FAIL preview: " + message); Count++; }
    private static PreviewSession New(Backend backend)
    { Now = 0; return new PreviewSession(backend, () => Now, 1000); }

    public static int Run()
    {
        Count = 0;
        var b = new Backend(); var s = New(b);
        Check(s.State == PreviewState.Idle && !s.NeedsTick, "no eager playback or idle work");
        s.Request(R(10)); Check(s.PlayingId == 1 && b.Events.Count == 1, "first request posted");
        b.Callbacks[0](); Check(s.State == PreviewState.Playing, "callback only marks ticket; main thread owns state");
        s.Tick(); Check(s.State == PreviewState.Idle && s.PlayingId == 0 && b.Released == 1, "natural end clears playback and callback root");

        b = new Backend(); s = New(b); s.Request(R(10)); s.Request(R(20)); s.Request(R(30));
        Check(b.Stopped.Count == 1 && b.Events.Count == 1, "rapid clicks submit only one stop and do not overlap");
        Check(s.HasPending && s.Superseded == 1, "one latest pending request only");
        b.Callbacks[0](); s.Tick();
        Check(b.Events.Count == 2 && b.Events[1] == 30 && s.PlayingId == 2, "obsolete intermediate request never posted");
        b.Callbacks[0](); s.Tick();
        Check(s.State == PreviewState.Playing && s.PlayingId == 2 && b.Released == 1, "late duplicate old end cannot clear new ticket");
        b.Callbacks[1](); s.Tick(); Check(s.State == PreviewState.Idle, "current end completes normally");

        b = new Backend(); s = New(b); s.Request(R(10)); s.Request(R(20)); b.Callbacks[0]();
        s.Request(R(30));
        Check(b.Events.Count == 2 && b.Events[1] == 30, "new click wins even when old end arrived before main update");
        Check(b.Stopped.Count == 1 && s.State == PreviewState.Playing, "newly started winner is not immediately stopped");

        b = new Backend { SyncEnd = true }; s = New(b); s.Request(R(10)); s.Tick();
        Check(s.State == PreviewState.Idle && s.Ends == 1, "end during Post before ID return is not lost");

        b = new Backend(); s = New(b); s.Request(R(10)); s.Request(R(20)); s.Stop("window-close");
        Check(!s.HasPending && b.Stopped.Count == 1, "closing window clears pending without duplicate stop");
        b.Callbacks[0](); s.Tick(); Check(b.Events.Count == 1 && s.State == PreviewState.Idle, "window close never starts queued audio");
        s.Stop("repeat-close"); Check(b.Stopped.Count == 1, "stop while idle is harmless");

        b = new Backend(); s = New(b); s.Request(R(10)); s.Request(R(20)); Now = 999; s.Tick();
        Check(s.State == PreviewState.Stopping, "stop waits rather than assuming native completion");
        Now = 1000; s.Tick();
        Check(s.State == PreviewState.Faulted && !s.HasPending && s.PlayingId == 1, "timeout preserves handle but discards pending");
        s.Request(R(30)); s.Tick();
        Check(b.Events.Count == 1 && b.Stopped.Count == 1, "timeout never creates or retries in a loop");
        b.Callbacks[0](); s.Tick();
        Check(s.State == PreviewState.Idle && b.Events.Count == 1, "late confirmed end can recover without replaying abandoned request");
        s.Request(R(40)); Check(b.Events[1] == 40, "new manual request allowed after confirmed recovery");

        b = new Backend { FailPost = true }; s = New(b); s.Request(R(10)); s.Request(R(20));
        Check(s.State == PreviewState.Faulted && b.Events.Count == 1, "post exception blocks further automatic submissions");
        s.Close(true); Check(b.Closed == 1 && b.Stopped.Count == 0, "unknown-ID exception still closes backend without stopping ID zero");
        b.Callbacks[0](); s.Tick(); Check(s.State == PreviewState.Closed, "callback after disposal cannot reopen state");

        b = new Backend { ZeroPost = true }; s = New(b); s.Request(R(10));
        Check(s.State == PreviewState.Faulted && s.PlayingId == 0 && b.Closed == 1, "rejected Post retires backend and creates no active handle");
        b.ZeroPost = false; s.Request(R(20)); Check(b.Events.Count == 1, "rejected event cannot accumulate more unknown callback packages");

        b = new Backend { FailStop = true, FailClose = true }; s = New(b); s.Request(R(10)); s.Stop("fixture");
        Check(s.State == PreviewState.Faulted, "stop exception becomes bounded fault");
        s.Close(true); Check(s.State == PreviewState.Closed && b.Closed == 1, "stop/backend errors cannot skip terminal state");
        s.Close(true); Check(b.Closed == 1, "close idempotent even when native backend failed");

        b = new Backend(); s = New(b); s.Request(R(10)); s.Close(false);
        Check(b.Stopped.Count == 0 && b.Closed == 1 && !b.LastNative, "fallback after native shutdown submits no stop");
        s.Request(R(20)); s.Stop("late"); s.Tick();
        Check(b.Events.Count == 1 && b.Stopped.Count == 0, "all late calls remain native-silent");

        b = new Backend(); s = New(b); s.Request(R(10)); s.Request(R(0));
        Check(b.Events.Count == 1 && b.Stopped.Count == 0, "invalid request does not disturb active audio");
        s.Request(new PreviewRequest(20, 0, 0, 0, float.NaN, false, "bad"));
        Check(!s.HasPending && b.Stopped.Count == 0, "nonfinite volume rejected before changing playback");

        b = new Backend(); s = New(b); s.Request(R(10));
        for (uint id = 20; id < 1020; id++) s.Request(R(id));
        Check(b.Events.Count == 1 && b.Stopped.Count == 1 && s.Superseded == 999, "1000 rapid requests have bounded native work");
        b.Callbacks[0](); s.Tick(); Check(b.Events.Count == 2 && b.Events[1] == 1019, "only last of burst starts");

        b = new Backend(); s = New(b); s.Request(R(10));
        var worker = new Thread(() => b.Callbacks[0]()); worker.Start(); worker.Join();
        Check(s.State == PreviewState.Playing, "worker end does not mutate UI/main state");
        s.Tick(); Check(s.State == PreviewState.Idle, "worker end observed by main-thread tick");

        b = new Backend(); s = New(b);
        for (int i = 0; i < 100; i++) s.Tick();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) s.Tick();
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated && b.Events.Count == 0, "idle state ticks allocate no managed memory or native work");
        return Count;
    }
}
