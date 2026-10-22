using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace GTFOSoundReplacer.Audio
{
    internal static class ListenerRouting
    {
        internal static readonly object Gate = new object();
        private static readonly List<RouteBindingState> Owned = new List<RouteBindingState>();
        private static bool _shutdown;
        internal static bool IsShutdown => _shutdown;
        internal static void Add(RouteBindingState state)
        {
            if (!_shutdown) Owned.Add(state);
        }
        internal static void Remove(RouteBindingState state) { Owned.Remove(state); }
        internal static int OwnedCount => Owned.Count;
        internal static void DefaultSubmitted(bool success)
        {
            if (_shutdown) return;
            foreach (var state in Owned) state.DefaultListenersSubmitted(success);
        }
        internal static void Shutdown()
        {
            lock (Gate)
            {
                _shutdown = true;
                foreach (var state in Owned) state.SetOwned(false);
            }
        }
    }

    [HarmonyPatch]
    internal static class DefaultListenerHook
    {
        private static MethodBase TargetMethod()
        {
            var method = typeof(AkSoundEngine).GetMethod("SetDefaultListeners",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Il2CppStructArray<ulong>), typeof(uint) }, null);
            if (method == null || method.ReturnType != typeof(AKRESULT))
                throw new MissingMethodException("Verified SetDefaultListeners signature unavailable; refusing unverified patch");
            return method;
        }

        [HarmonyPriority(Priority.First)]
        private static void Prefix(out bool __state)
        {
            __state = false;
            Monitor.Enter(ListenerRouting.Gate);
            __state = true;
        }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(AKRESULT __result)
        {
            // Only numeric IDs and managed state below: safe even if the game
            // submits defaults on a worker. No deferred Unity task can race Post.
            ListenerRouting.DefaultSubmitted(__result == AKRESULT.AK_Success);
        }

        private static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state) Monitor.Exit(ListenerRouting.Gate);
            return __exception;
        }
    }

    internal sealed class NativeRouteBackend : IRouteBindingBackend
    {
        internal ulong Emitter;
        internal ulong Listener = ulong.MaxValue;
        public bool ResetToDefault()
        {
            int result = AkSoundEnginePINVOKE.CSharp_ResetListenersToDefault(Emitter);
            Log("ResetListenersToDefault", result);
            return result == (int)AKRESULT.AK_Success;
        }
        public bool SetOutputGain(float gain)
        {
            int result = AkSoundEnginePINVOKE.CSharp_SetGameObjectOutputBusVolume(Emitter, Listener, gain);
            Log("SetGameObjectOutputBusVolume gain=" + gain, result);
            return result == (int)AKRESULT.AK_Success;
        }
        private void Log(string operation, int result)
        {
            Plugin.Instance?.Log.LogInfo("[SR-Route] object=" + Emitter + " " + operation +
                " submitted=" + (AKRESULT)result + " completion=unverified");
        }
    }
}
