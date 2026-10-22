using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace GTFOSoundReplacer.Audio
{
    internal static class PreviewService
    {
        private static PreviewSession _session;
        private static PreviewEmitter _emitter;
        private static int _mainThread;
        private static bool _hooksReady, _nativeClosed;
        internal static string Status { get; private set; } = "";
        internal static int StatusVersion { get; private set; }
        internal static void Initialize()
        {
            _mainThread = Environment.CurrentManagedThreadId;
            _emitter = new PreviewEmitter(_mainThread);
            _session = new PreviewSession(_emitter, Stopwatch.GetTimestamp, Stopwatch.Frequency, Notify, Log);
            InstallHooks();
        }
        private static void InstallHooks()
        {
            var harmony = new Harmony(Plugin.GUID + ".preview");
            try
            {
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                var term = typeof(AkSoundEngine).GetMethod("Term", flags, null, Type.EmptyTypes, null);
                var callbackTerm = typeof(AkCallbackManager).GetMethod("Term", flags, null, Type.EmptyTypes, null);
                var level = typeof(CellSound).GetMethod("OnLevelCleanup", flags, null, Type.EmptyTypes, null);
                if (term == null || callbackTerm == null || level == null) throw new MissingMethodException("Preview lifecycle boundary unavailable");
                harmony.Patch(term, prefix: new HarmonyMethod(typeof(PreviewService), nameof(BeforeTerm)) { priority = Priority.First });
                harmony.Patch(callbackTerm, finalizer: new HarmonyMethod(typeof(PreviewService), nameof(AfterCallbackTerm)));
                harmony.Patch(level, prefix: new HarmonyMethod(typeof(PreviewService), nameof(LevelCleanup)));
                _hooksReady = true;
                Log("prototype-lifecycle-ready; native cleanup before Term; no per-preview bank load");
            }
            catch (Exception e)
            {
                try { harmony.UnpatchSelf(); } catch { }
                Notify("试听生命周期钩子不可用，试听已禁用", true); Log("hook-error " + e);
            }
        }
        internal static void Request(uint eventId, Vector3 position, float volume, bool voice, string label)
        {
            if (!_hooksReady || _nativeClosed || _session == null)
            { Notify("音频生命周期未就绪，试听不可用", true); return; }
            if (Environment.CurrentManagedThreadId != _mainThread) { Log("off-main preview refused"); return; }
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            lock (ListenerRouting.Gate)
            {
                if (_nativeClosed) return;
                _session.Request(new PreviewRequest(eventId, position.x, position.y, position.z, volume, voice, label));
                Log("request-metrics managedAllocatedBytes=" + (GC.GetAllocatedBytesForCurrentThread() - allocated) +
                    " created=" + _emitter.Created + " registrationBalance=" + _emitter.RegistrationBalance +
                    " callbackRoots=" + _emitter.CallbackRoots + " pending=" + _session.HasPending);
            }
        }
        internal static void Tick()
        {
            if (_emitter == null || _session == null || (!_session.NeedsTick && !_emitter.NeedsMainThreadCleanup)) return;
            lock (ListenerRouting.Gate)
            {
                try
                {
                    _emitter.DestroyOnMainThread();
                    if (!_nativeClosed)
                    {
                        int ends = _session.Ends;
                        var state = _session.State;
                        _session.Tick();
                        if (ends != _session.Ends || state != _session.State)
                            Log("state-metrics state=" + _session.State + " created=" + _emitter.Created +
                                " registrationBalance=" + _emitter.RegistrationBalance + " callbackRoots=" + _emitter.CallbackRoots +
                                " pending=" + _session.HasPending + " ignoredLateCallbacks=" + _session.LateCallbacks);
                    }
                }
                catch (Exception e)
                {
                    Log("update-error " + e);
                    _session.Close(!_nativeClosed);
                    _nativeClosed = true;
                }
            }
        }
        internal static void Stop(string reason)
        {
            if (_session == null || _nativeClosed) return;
            lock (ListenerRouting.Gate) { if (!_nativeClosed) _session.Stop(reason); }
        }
        private static void BeforeTerm()
        {
            // Never let preview cleanup prevent the game from executing its original Term.
            try
            {
                lock (ListenerRouting.Gate)
                {
                    if (_nativeClosed) return;
                    _nativeClosed = true;
                    Log("term-prefix-cleanup-begin thread=" + Environment.CurrentManagedThreadId);
                    _session?.Close(true);
                    ListenerRouting.Shutdown();
                    Log("term-prefix-cleanup-end; subsequent preview native calls disabled");
                }
            }
            catch (Exception e) { Log("term-prefix-error " + e); }
        }
        private static Exception AfterCallbackTerm(Exception __exception)
        {
            try
            {
                lock (ListenerRouting.Gate)
                {
                    _nativeClosed = true;
                    _session?.Close(false); // Safe fallback if an unobserved engine path skipped our Term hook.
                    _emitter?.CallbackManagerGone();
                    ListenerRouting.Shutdown();
                }
            }
            catch (Exception e) { Log("callback-term-error " + e); }
            return __exception;
        }
        private static void LevelCleanup() { try { Stop("level-cleanup"); } catch (Exception e) { Log("level-cleanup-error " + e); } }
        internal static void ManagedQuit()
        {
            // Measured OnApplicationQuit is AFTER engine teardown: no native cleanup here.
            try
            {
                lock (ListenerRouting.Gate)
                {
                    _nativeClosed = true; _session?.Close(false);
                    _emitter?.DestroyOnMainThread();
                }
            }
            catch (Exception e) { Log("managed-quit-error " + e); }
        }
        internal static void HostDestroyed()
        {
            try
            {
                lock (ListenerRouting.Gate)
                {
                    bool nativeAvailable = !_nativeClosed && Environment.CurrentManagedThreadId == _mainThread && AkSoundEngine.IsInitialized();
                    _nativeClosed = true;
                    _session?.Close(nativeAvailable);
                    _emitter?.DestroyOnMainThread();
                    Log("host-destroyed nativeAvailable=" + nativeAvailable);
                }
            }
            catch (Exception e) { _nativeClosed = true; Log("host-destroyed-error " + e); }
        }
        private static void Notify(string message, bool failed)
        {
            Status = message; StatusVersion++;
            try
            {
                if (failed) Plugin.Instance?.Log.LogWarning("[SR-Preview] " + message);
                else Plugin.Instance?.Log.LogInfo("[SR-Preview] " + message);
            }
            catch { }
        }
        internal static void Log(string message)
        { try { Plugin.Instance?.Log.LogInfo("[SR-Preview] " + message); } catch { } }
    }
}
