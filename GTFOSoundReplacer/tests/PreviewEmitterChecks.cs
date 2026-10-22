using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using GTFOSoundReplacer.Audio;

namespace GTFOSoundReplacer.Audio
{
    // Logger only; the real PreviewEmitter, RouteBindingState and ListenerRouting are compiled below.
    internal static class PreviewService { internal static void Log(string message) {} }
}

public static class PreviewEmitterChecks
{
    private static int Count;
    private static readonly PreviewRequest Plain = new PreviewRequest(123, 1, 2, 3, 1, false, "plain");
    private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL emitter: " + message); Count++; }
    private static PreviewEmitter Fresh()
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        typeof(ListenerRouting).GetField("_shutdown", flags).SetValue(null, false);
        var list = typeof(ListenerRouting).GetField("Owned", flags).GetValue(null);
        list.GetType().GetMethod("Clear").Invoke(list, null);
        PreviewNativeFixture.Callbacks.Clear(); PreviewNativeFixture.FailCancel = false; PreviewNativeFixture.FailScopeStop = false;
        PreviewNativeFixture.FailDestroy = false; PreviewNativeFixture.EngineReady = true;
        PreviewNativeFixture.UnregisterResult = AKRESULT.AK_Success;
        PlayerEngine.RegisterResult = AKRESULT.AK_Success; PlayerEngine.SwitchResult = AKRESULT.AK_Success;
        PlayerEngine.PostResult = 99; PlayerEngine.Clear(); UnityEngine.Object.Destroyed = 0;
        return new PreviewEmitter(Environment.CurrentManagedThreadId);
    }
    private static void Gate(Action action) { lock (ListenerRouting.Gate) action(); }
    private static void Reject(Action action, string message)
    { bool rejected = false; try { Gate(action); } catch { rejected = true; } Check(rejected, message); }
    public static int Run()
    {
        Count = 0;
        var e = Fresh();
        Check(e.Created == 0 && ListenerRouting.OwnedCount == 0, "constructing service creates no object or route");
        Gate(() => e.Post(Plain, () => {}));
        Check(e.Created == 1 && e.RegistrationBalance == 1 && ListenerRouting.OwnedCount == 1, "first Post owns one object and one route");
        Check(e.CallbackRoots == 1, "active callback kept alive");
        Check(!PlayerEngine.Calls.Any(c => c.Contains("switch:")), "plain first preview never sets a voice switch");
        Gate(() => e.PlaybackEnded());
        Check(e.CallbackRoots == 0, "natural end releases owned callback reference");
        for (int i = 0; i < 100; i++) Gate(() => { e.Post(Plain, () => {}); e.PlaybackEnded(); });
        Check(e.Created == 1 && e.Registrations == 1 && ListenerRouting.OwnedCount == 1, "repeated previews do not grow objects, registrations or routes");
        Gate(() => e.Post(new PreviewRequest(123, 0, 0, 0, .3f, true, "voice"), () => {}));
        Check(PlayerEngine.Calls.Contains("switch:character:ch_01") && PlayerEngine.Calls.Contains("switch:intensity_state:intensity_1_exploration"), "voice sets only its own context");
        Gate(() => e.PlaybackEnded()); PlayerEngine.Clear(); Gate(() => e.Post(Plain, () => {}));
        Check(e.Created == 1 && e.Registrations == 2 && e.Unregistrations == 1, "voice-to-plain resets native context on same object");
        Check(PlayerEngine.Calls[0] == "unregister" && PlayerEngine.Calls[1] == "register", "context reset ordering is unregister then register");
        Check(!PlayerEngine.Calls.Any(c => c.StartsWith("switch:")), "plain path does not assign a character after reset");
        Gate(() => e.Close(true));
        Check(e.Created == 1 && e.RegistrationBalance == 0 && ListenerRouting.OwnedCount == 0, "native close balances registration and removes route");
        Check(UnityEngine.Object.Destroyed == 1 && PlayerEngine.Calls.Contains("scope-stop") && PlayerEngine.Calls.Contains("cancel"), "close stops only private object, cancels then destroys");
        Check(e.CallbackRoots == 1, "terminal cancellation holds callback until manager teardown");
        Gate(() => e.CallbackManagerGone()); Check(e.CallbackRoots == 0, "manager teardown releases retired root");
        PlayerEngine.Clear(); Gate(() => { e.Close(true); e.Stop(99); ListenerRouting.DefaultSubmitted(true); });
        Check(PlayerEngine.Calls.Count == 0 && UnityEngine.Object.Destroyed == 1, "repeated close/late Stop/default updates never touch released native object");
        Reject(() => e.Post(Plain, () => {}), "Post after closure rejected");

        e = Fresh(); PlayerEngine.RegisterResult = AKRESULT.AK_Fail;
        Reject(() => e.Post(Plain, () => {}), "registration failure is visible");
        int attempts = PlayerEngine.Calls.Count(c => c == "register");
        Reject(() => e.Post(Plain, () => {}), "failed registration cannot silently retry/create");
        Check(e.Created == 1 && PlayerEngine.Calls.Count(c => c == "register") == attempts, "failed registration remains bounded");
        Gate(() => e.Close(true)); Check(ListenerRouting.OwnedCount == 0 && UnityEngine.Object.Destroyed == 1, "partial creation is still cleaned up");

        e = Fresh(); Gate(() => e.Post(Plain, () => {})); PreviewNativeFixture.FailCancel = true; PreviewNativeFixture.FailScopeStop = true;
        Gate(() => e.Close(true));
        Check(PlayerEngine.Calls.Contains("unregister") && UnityEngine.Object.Destroyed == 1 && ListenerRouting.OwnedCount == 0, "stop/cancel exceptions do not skip unregister and object cleanup");

        e = Fresh(); Gate(() => e.Post(Plain, () => {})); PreviewNativeFixture.UnregisterResult = AKRESULT.AK_Fail;
        Gate(() => e.Close(true));
        Check(e.IsRegistered && e.RegistrationBalance == 1, "failed unregister is not falsely reported as released");
        Check(ListenerRouting.OwnedCount == 0 && UnityEngine.Object.Destroyed == 1, "unregister failure still disables later route writes");

        e = Fresh(); Gate(() => e.Post(Plain, () => {})); PlayerEngine.Clear(); Gate(() => e.Close(false));
        Check(PlayerEngine.Calls.Count == 0 && ListenerRouting.OwnedCount == 0, "cleanup after native shutdown performs no Wwise call");

        e = Fresh(); Gate(() => e.Post(Plain, () => {})); PlayerEngine.Clear();
        Exception workerError = null;
        var worker = new Thread(() => { try { Gate(() => e.Close(true)); } catch (Exception ex) { workerError = ex; } });
        worker.Start(); worker.Join();
        Check(workerError == null && PlayerEngine.Calls.Contains("unregister-id") && PlayerEngine.Calls.Contains("cancel-id"), "off-main termination uses cached numeric native ID path");
        Check(UnityEngine.Object.Destroyed == 0 && e.NeedsMainThreadCleanup, "off-main termination defers Unity destruction");
        Gate(() => e.DestroyOnMainThread()); Check(UnityEngine.Object.Destroyed == 1, "main thread completes deferred Unity destruction");

        e = Fresh(); Gate(() => e.Post(Plain, () => {})); PreviewNativeFixture.FailDestroy = true;
        Gate(() => e.Close(true)); Gate(() => e.DestroyOnMainThread());
        Check(UnityEngine.Object.Destroyed == 1 && !e.NeedsMainThreadCleanup, "Unity destroy failure cannot create per-frame retry calls");

        e = Fresh(); PreviewNativeFixture.EngineReady = false;
        Reject(() => e.Post(Plain, () => {}), "uninitialized engine refuses Post before object creation");
        Gate(() => e.Close(true));
        Check(e.Created == 0 && PlayerEngine.Calls.Count == 0, "engine-unavailable cleanup does not issue native writes");
        return Count;
    }
}
