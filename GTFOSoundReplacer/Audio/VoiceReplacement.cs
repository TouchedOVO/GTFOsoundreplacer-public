using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace GTFOSoundReplacer.Audio
{
    // Local prototype: only the observed SayLine -> callback Post protocol.
    internal static class VoiceReplacement
    {
        private const int Capacity = 128;
        private static readonly object Gate = new object();
        private static readonly HashSet<Ticket> Pending = new HashSet<Ticket>();
        private static volatile bool _ready, _disabled, _shutdown, _callbacksGone;
        private static long _sequence;
        [ThreadStatic] private static Frame Current;
        [ThreadStatic] private static Ticket Callback;
        [ThreadStatic] private static long CallbackVoice;
        [ThreadStatic] private static bool Fallback;
        private sealed class Frame
        {
            internal long Voice, Sound;
            internal uint Original, Dialog;
            internal bool Consumed;
        }
        private sealed class Ticket : VoiceRequest
        {
            internal readonly long Number;
            internal AkCallbackManager.EventCallback OriginalCallback, Wrapper;
            internal volatile bool Destroyed;
            internal Ticket(Frame frame, uint target, AkCallbackManager.EventCallback callback)
                : base(frame.Original, target, frame.Dialog, frame.Voice, frame.Sound)
            { Number = Interlocked.Increment(ref _sequence); OriginalCallback = callback; }
        }
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Initialize()
        {
            if (!PreviewService.LifecycleReady) { Log("disabled: lifecycle hooks unavailable"); return; }
            var h = new Harmony(Plugin.GUID + ".voice-prototype");
            try
            {
                Hook(h, typeof(PlayerVoice).GetMethod("SayLine", Flags, null, new[] { typeof(uint),typeof(uint),typeof(uint),typeof(uint),typeof(uint) }, null), nameof(SayBefore), null, nameof(SayAfter));
                Hook(h, typeof(CellSoundPlayer).GetMethod("Post", Flags, null, new[] { typeof(uint),typeof(Vector3),typeof(uint),typeof(AkCallbackManager.EventCallback),typeof(Il2CppSystem.Object) }, null), nameof(PostBefore), nameof(PostAfter), nameof(PostFinally));
                Hook(h, typeof(PlayerVoice).GetMethod("VoiceCallback", Flags), nameof(CallbackBefore), null, nameof(CallbackAfter));
                Hook(h, typeof(Player.PlayerDialogManager).GetMethod("OnDialogLineFinished", Flags, null, new[] { typeof(uint),typeof(uint) }, null), nameof(DialogBefore), null, null);
                Hook(h, typeof(PlayerVoice).GetMethod("OnDestroy", Flags), nameof(DestroyBefore), null, null);
                _ready = true;
                Log("prototype-v1-ready; fixed replace-only; voice fixed-volume=1; hooks=5 capacity=" + Capacity);
            }
            catch (Exception e) { try { h.UnpatchSelf(); } catch (Exception rollback) { Log("hook-rollback-error " + rollback.Message); } Log("hook-error " + e); }
        }
        private static void Hook(Harmony h, MethodInfo method, string before, string after, string finalizer)
        {
            if (method == null) throw new MissingMethodException("Required voice protocol method");
            h.Patch(method, before == null ? null : new HarmonyMethod(typeof(VoiceReplacement), before),
                after == null ? null : new HarmonyMethod(typeof(VoiceReplacement), after),
                finalizer: finalizer == null ? null : new HarmonyMethod(typeof(VoiceReplacement), finalizer));
        }
        private static long Ptr(Il2CppObjectBase value) => ReferenceEquals(value,null) ? 0 : value.Pointer.ToInt64();
        private static void SayBefore(PlayerVoice __instance, uint __0, uint __2, out Frame __state)
        {
            __state = Current;
            // Clear outer context even when this nested request cannot be identified.
            Current = null;
            try
            {
                if (!__instance.m_isSetup) return;
                Current = new Frame { Voice=Ptr(__instance), Sound=Ptr(__instance.m_sound), Original=__0, Dialog=__2 };
            }
            catch (Exception e) { Disable("context-error " + e.Message); }
        }
        private static Exception SayAfter(Frame __state, Exception __exception) { Current=__state; return __exception; }
        private static void PostBefore(CellSoundPlayer __instance, ref uint __0, uint __2, ref AkCallbackManager.EventCallback __3, out Ticket __state)
        {
            __state = null;
            var frame = Current;
            if (Fallback || frame == null || frame.Consumed || !VoiceRequest.MatchesSource(frame.Sound,frame.Original,Ptr(__instance),__0)) return;
            frame.Consumed = true;
            if (PluginConfig.LogEventIds != null && PluginConfig.LogEventIds.Value) AudioHook.LogEventId(frame.Original);
            if (!_ready || _disabled || _shutdown || ListenerRouting.IsShutdown) return;
            try
            {
                var decision = ReplacementResolver.Resolve(SoundReplacer.Instance?.Map,frame.Original,1f, fixedUnitVolume: true);
                if (decision.Kind == ReplacementKind.Original) return;
                if (ReferenceEquals(__3,null) || (__2 & (uint)AkCallbackType.AK_EndOfEvent)==0) { Disable("callback protocol unavailable; original retained"); return; }
                var ticket = new Ticket(frame,decision.TargetId,__3);
                Action<Il2CppSystem.Object,AkCallbackType,AkCallbackInfo> wrapper = (cookie,kind,info) => Forward(ticket,cookie,kind,info);
                ticket.Wrapper = wrapper;
                lock (Gate)
                {
                    if (_shutdown || _callbacksGone) return;
                    if (Pending.Count >= Capacity) { Disable("request capacity reached; original retained"); return; }
                    Pending.Add(ticket);
                }
                __state = ticket;
                __3 = ticket.Wrapper;
                __0 = ticket.TargetId;
                Log("submit request="+ticket.Number+" original="+ticket.OriginalId+" target="+ticket.TargetId+" dialog="+ticket.DialogId+" sound="+ticket.Sound);
            }
            catch (Exception e) { Disable("prepare-error "+e); }
        }
        private static void PostAfter(CellSoundPlayer __instance, Vector3 __1, uint __2, Il2CppSystem.Object __4, Ticket __state, ref uint __result)
        {
            if (__state == null) return;
            if (__result != 0)
            {
                if (!__state.Bind(__result)) Disable("returned playing ID disagrees with early callback");
                Log("posted request="+__state.Number+" playing="+__result);
                return;
            }
            Disable("Post returned0; one original fallback; failed callback root retained until callback-manager Term");
            if (_shutdown || _callbacksGone) return;
            bool prior=Fallback; Fallback=true;
            try { __result=__instance.Post(__state.OriginalId,__1,__2,__state.OriginalCallback,__4); Log("fallback original="+__state.OriginalId+" playing="+__result); }
            finally { Fallback=prior; }
        }
        private static Exception PostFinally(Ticket __state, Exception __exception)
        {
            if (__state != null && __exception != null) Disable("post-exception; acceptance unknown; no replay: "+__exception.GetType().Name);
            return __exception;
        }
        private static void Forward(Ticket ticket, Il2CppSystem.Object cookie, AkCallbackType kind, AkCallbackInfo info)
        {
            var prior=Callback;
            bool ending=kind==AkCallbackType.AK_EndOfEvent;
            bool matched=false;
            if (ending && !ticket.ClaimEnd()) { Log("duplicate-end request="+ticket.Number); return; }
            try
            {
                try
                {
                    var eventInfo=info?.TryCast<AkEventCallbackInfo>();
                    matched=eventInfo!=null && ticket.Matches(eventInfo.eventID,eventInfo.playingID);
                }
                catch (Exception e) { Disable("callback-read-error "+e.GetType().Name); }
                if (!matched) Disable("callback identity mismatch; game callback retained, completion unconfirmed");
                Callback=matched ? ticket : null;
                Log("callback request="+ticket.Number+" matched="+matched+" playing="+ticket.PlayingId+" kind="+kind);
                ticket.OriginalCallback?.Invoke(cookie,kind,info);
            }
            finally
            {
                Callback=prior;
                if (ending) lock (Gate) Pending.Remove(ticket);
                Log("callback-done request="+ticket.Number+" remaining="+Count());
            }
        }
        private static void CallbackBefore(PlayerVoice __instance, out long __state) { __state=CallbackVoice; CallbackVoice=Ptr(__instance); }
        private static Exception CallbackAfter(long __state, Exception __exception) { CallbackVoice=__state; return __exception; }
        private static void DialogBefore(ref uint __0, uint __1)
        {
            var ticket=Callback;
            if (ticket==null) return;
            if (ticket.Destroyed || CallbackVoice!=ticket.Voice || !ticket.MatchesCompletion(__0,__1))
            { Disable("dialog identity mismatch; notification not changed"); return; }
            __0=ticket.OriginalId;
            Log("dialog-complete request="+ticket.Number+" original="+__0+" dialog="+__1);
        }
        private static void DestroyBefore(PlayerVoice __instance)
        { long voice=Ptr(__instance); lock(Gate) foreach(var ticket in Pending) if(ticket.Voice==voice) ticket.Destroyed=true; }
        internal static void BeforeTerm() { _shutdown=true; Log("term outstanding="+Count()); }
        internal static void CallbackManagerGone()
        {
            lock(Gate) { _shutdown=true; _callbacksGone=true; foreach(var ticket in Pending) { ticket.OriginalCallback=null; ticket.Wrapper=null; } Pending.Clear(); }
            Log("callback-manager-gone retained=0");
        }
        private static int Count() { lock(Gate) return Pending.Count; }
        private static void Disable(string reason) { _disabled=true; Log("disabled "+reason); }
        private static void Log(string text) { try { Plugin.Instance?.Log.LogInfo("[SR-Voice] "+text); } catch { } }
    }
}
