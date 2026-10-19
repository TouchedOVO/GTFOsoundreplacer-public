using System;
using System.Collections.Generic;
using GTFOSoundReplacer.Audio;

// The queue deliberately separates API acceptance from engine processing.
// Engine behavior here models the native disassembly; it is not a Wwise integration test.
internal sealed class QueuedEngine : IRouteBindingBackend
{
    internal readonly List<string> Calls = new List<string>();
    internal readonly Queue<Action> Queue = new Queue<Action>();
    internal readonly List<string> Logs = new List<string>();
    internal readonly ulong Owned = 100, GameObject = 442;
    internal ulong DefaultListener = 10, OwnedListener = 10, GameListener = 10;
    internal bool Independent, FailReset, FailGain, RejectAtProcessing;
    internal float AppliedGain = 1;
    internal void SubmitDefaults(ulong id) {
        Calls.Add("defaults:" + id);
        Queue.Enqueue(() => {
            DefaultListener = id; GameListener = id;
            if (!Independent) OwnedListener = id;
        });
    }
    public bool ResetToDefault() {
        Calls.Add("reset:" + Owned);
        if (FailReset) return false;
        Queue.Enqueue(() => {
            if (RejectAtProcessing) return;
            Independent = false; OwnedListener = DefaultListener; AppliedGain = 1;
        });
        return true;
    }
    public bool SetOutputGain(float gain) {
        Calls.Add("gain:" + Owned);
        if (FailGain) return false;
        Queue.Enqueue(() => {
            if (RejectAtProcessing) return;
            if (!Independent) OwnedListener = DefaultListener;
            Independent = true; AppliedGain = gain;
        });
        return true;
    }
    internal void Drain() { while (Queue.Count != 0) Queue.Dequeue()(); }
    internal void ClearCalls() { Calls.Clear(); }
}

public static class ListenerBindingChecks
{
    private static int count;
    private static void Check(bool pass, string label) {
        if (!pass) throw new Exception("FAIL: " + label);
        count++;
    }
    private static RouteBindingState State(QueuedEngine e) {
        var s = new RouteBindingState(e, e.Logs.Add); s.SetOwned(true); return s;
    }
    private static void SameCalls(QueuedEngine e, string expected, string label) {
        Check(string.Join(",", e.Calls) == expected, label + " actual=" + string.Join(",", e.Calls));
    }
    public static int Run()
    {
        count = 0;
        var e = new QueuedEngine(); var s = new RouteBindingState(e, e.Logs.Add);
        s.DefaultListenersSubmitted(true);
        s.PreparePost();
        s.ApplyOutputGain(.35f);
        Check(e.Calls.Count == 0 && e.Queue.Count == 0, "no emitter means no registration, reset, or gain command");

        e = new QueuedEngine(); s = State(e);
        s.ApplyOutputGain(.35f); e.Drain(); e.ClearCalls();
        e.SubmitDefaults(20); s.DefaultListenersSubmitted(true);
        SameCalls(e, "defaults:20,reset:100,gain:100", "default submit precedes reset and cached gain");
        Check(e.OwnedListener == 10 && e.GameListener == 10, "accepted commands have not executed yet");
        e.Drain();
        Check(e.OwnedListener == 20 && e.GameListener == 20, "engine consumption attaches both objects to new listener");
        Check(e.AppliedGain == .35f, "gain preserved after route rebuild");
        e.ClearCalls(); Check(s.PreparePost(), "clean state permits post");
        Check(e.Calls.Count == 0, "clean playback does not reset routes per post");
        for (ulong id = 21; id <= 24; id++) {
            e.SubmitDefaults(id); s.DefaultListenersSubmitted(true); e.Drain();
            Check(e.OwnedListener == id && e.GameListener == id && e.AppliedGain == .35f,
                  "multiple transitions preserve latest listener and gain " + id);
        }
        Check(e.Calls.TrueForAll(c => !c.StartsWith("reset:442") && !c.StartsWith("gain:442")), "maintenance never mutates game emitter");

        e.ClearCalls(); s.ApplyOutputGain(1); e.Drain();
        SameCalls(e, "reset:100", "unit gain restores following without copying default connections");
        Check(!e.Independent && e.AppliedGain == 1, "unit gain removes explicit connection copy");
        e.ClearCalls(); e.SubmitDefaults(30); s.DefaultListenersSubmitted(true); e.Drain();
        Check(e.OwnedListener == 30 && e.AppliedGain == 1, "unit gain follows future defaults");
        Check(!e.Calls.Contains("gain:100"), "unit gain does not create a new independent list");

        e = new QueuedEngine(); s = State(e); s.SetFollowDefaults(false); e.ClearCalls();
        s.DefaultListenersSubmitted(true); s.PreparePost();
        Check(e.Calls.Count == 0, "explicit listener ignores global default changes");
        s.ApplyOutputGain(1);
        SameCalls(e, "gain:100", "explicit mode unit gain retains explicitly owned routing");
        e.ClearCalls(); s.ApplyOutputGain(.2f);
        SameCalls(e, "gain:100", "explicit mode gain update does not reset listener");

        e = new QueuedEngine(); s = State(e); s.ApplyOutputGain(.3f); e.Drain(); e.FailReset = true;
        s.DefaultListenersSubmitted(true);
        Check(s.NeedsRepair, "failed reset leaves pending repair");
        e.ClearCalls(); Check(!s.PreparePost(), "failed repair prevents a falsely ready post");
        SameCalls(e, "reset:100", "failed reset does not enqueue output gain");
        Check(e.Logs.Count != 0, "failure is observable");
        e.FailReset = false; e.ClearCalls(); Check(s.PreparePost(), "repair retries at next playback boundary");
        Check(!s.NeedsRepair, "accepted retry clears submission pending state");

        e = new QueuedEngine(); s = State(e); s.ApplyOutputGain(.4f); e.Drain();
        e.FailGain = true; e.SubmitDefaults(40); s.DefaultListenersSubmitted(true);
        Check(s.NeedsRepair, "gain reapply failure retains pending repair");
        e.FailGain = false; e.ClearCalls(); Check(s.PreparePost(), "failed gain is retried before subsequent post");
        SameCalls(e, "reset:100,gain:100", "retry reestablishes reset then gain order");
        e.Drain(); Check(e.OwnedListener == 40 && e.AppliedGain == .4f, "retry consumed correct gain and route");

        e.ClearCalls(); s.DefaultListenersSubmitted(false);
        Check(e.Calls.Count == 0, "failed default-listener submission does not trigger maintenance");
        s.SetOwned(false); s.DefaultListenersSubmitted(true); s.PreparePost();
        Check(e.Calls.Count == 0, "released emitter is never touched");

        e = new QueuedEngine(); s = State(e);
        s.DefaultListenersSubmitted(true);
        Check(e.Calls.Count == 0, "already following defaults needs no per-transition reset");
        s.ApplyOutputGain(.6f); e.Drain(); e.FailGain = true;
        Check(!s.ApplyOutputGain(.2f) && s.OutputGain == .6f, "failed new gain does not overwrite last successful object gain");
        e.FailGain = false; e.ClearCalls(); e.SubmitDefaults(60); s.DefaultListenersSubmitted(true); e.Drain();
        Check(e.AppliedGain == .6f && e.OwnedListener == 60, "subsequent transition uses last accepted gain");

        e = new QueuedEngine(); s = State(e); s.SetFollowDefaults(false); s.ApplyOutputGain(.7f); e.Drain();
        e.SubmitDefaults(70); s.DefaultListenersSubmitted(true); e.Drain();
        Check(e.OwnedListener == 10, "default update leaves explicit emitter listener untouched");
        s.SetFollowDefaults(true); e.ClearCalls(); Check(s.PreparePost(), "explicit to default mode repairs before post");
        SameCalls(e, "reset:100,gain:100", "mode transition reset precedes preserved gain");
        e.Drain(); Check(e.OwnedListener == 70 && e.AppliedGain == .7f, "mode transition adopts current default");
        e = new QueuedEngine(); s = State(e); s.ApplyOutputGain(.35f); e.Drain();
        e.FailReset = true; s.DefaultListenersSubmitted(true);
        Check(s.NeedsRepair, "failed repair pending before release");
        s.SetOwned(false);
        Check(!s.NeedsRepair && s.OutputGain == 1f, "emitter release clears route and gain state of old object");
        e.FailReset = false; e.ClearCalls(); s.SetOwned(true); s.DefaultListenersSubmitted(true);
        Check(e.Calls.Count == 0 && s.OutputGain == 1f, "new owned emitter does not inherit old object gain or dirty state");
        // Processing can reject an accepted command. This deliberately demonstrates the
        // submission-only guarantee: actual success must be verified with live readback.
        e = new QueuedEngine(); s = State(e); s.ApplyOutputGain(.5f); e.Drain();
        e.RejectAtProcessing = true; e.SubmitDefaults(50); s.DefaultListenersSubmitted(true); e.Drain();
        Check(!s.NeedsRepair && e.OwnedListener == 10 && e.GameListener == 50,
              "accepted submission is not engine execution confirmation");
        return count;
    }
}
