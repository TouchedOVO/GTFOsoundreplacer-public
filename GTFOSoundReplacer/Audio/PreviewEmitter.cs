using System;
using System.Threading;
using UnityEngine;

namespace GTFOSoundReplacer.Audio
{
    // Owns only one bare game object. Never loads, unloads or copies a SoundBank.
    internal sealed class PreviewEmitter : IPreviewBackend
    {
        private readonly int _mainThread;
        private readonly NativeRouteBackend _backend = new NativeRouteBackend();
        private RouteBindingState _route;
        private GameObject _object;
        private ulong _id;
        private bool _registered, _attached, _closed, _voice, _destroyPending, _callbacksGone, _engineUnavailable;
        private AkCallbackManager.EventCallback _callback, _retiredCallback;
        internal int Created { get; private set; }
        internal int Registrations { get; private set; }
        internal int Unregistrations { get; private set; }
        internal int RegistrationBalance => Registrations - Unregistrations;
        internal int CallbackRoots => (_callback == null ? 0 : 1) + (_retiredCallback == null ? 0 : 1);
        internal bool NeedsMainThreadCleanup => _destroyPending;
        internal bool IsRegistered => _registered;

        internal PreviewEmitter(int mainThread) { _mainThread = mainThread; }
        public uint Post(PreviewRequest request, Action ended)
        {
            if (_closed || ListenerRouting.IsShutdown) throw new InvalidOperationException("Preview native lifetime is closed");
            if (Environment.CurrentManagedThreadId != _mainThread) throw new InvalidOperationException("Preview must run on main thread");
            if (!AkSoundEngine.IsInitialized())
            { _engineUnavailable = true; throw new InvalidOperationException("Audio engine is not initialized"); }
            EnsureObject();
            if (_voice && !request.Voice)
            {
                // Clear Wwise object-scoped switches without assigning a character to a non-voice event.
                // The old playback has ended before the state machine permits this path.
                _route.SetOwned(false);
                var reset = AkSoundEngine.UnregisterGameObj(_object);
                if (reset != AKRESULT.AK_Success) throw new InvalidOperationException("Preview context unregister: " + reset);
                _registered = false; Unregistrations++;
                Register(); _voice = false;
                Log("context-reset sameObject=" + _id + " completion=unverified");
            }
            _object.transform.position = new Vector3(request.X, request.Y, request.Z);
            if (!_route.PreparePost() || !_route.ApplyOutputGain(Math.Max(0f, Math.Min(2f, request.Volume))))
                throw new InvalidOperationException("Preview listener/volume preparation failed");
            if (request.Voice)
            {
                var character = AkSoundEngine.SetSwitch("character", "ch_01", _object);
                var intensity = AkSoundEngine.SetSwitch("intensity_state", "intensity_1_exploration", _object);
                if (character != AKRESULT.AK_Success || intensity != AKRESULT.AK_Success)
                    throw new InvalidOperationException("Preview switches: " + character + "/" + intensity);
                _voice = true;
            }
            Action<Il2CppSystem.Object, AkCallbackType, AkCallbackInfo> callback = (cookie, kind, info) =>
            {
                if (kind == AkCallbackType.AK_EndOfEvent) ended();
            };
            _callback = callback;
            return AkSoundEngine.PostEvent(request.EventId, _object, (uint)AkCallbackType.AK_EndOfEvent, _callback, null);
        }
        private void EnsureObject()
        {
            if (_object == null)
            {
                // Never silently recreate a lost object, or multiply registrations after a failure.
                if (Created != 0) throw new InvalidOperationException("Preview object was lost");
                _object = new GameObject("GTFOSoundReplacer_PreviewEmitter"); Created++;
                UnityEngine.Object.DontDestroyOnLoad(_object);
                _id = AkSoundEngine.GetAkGameObjectID(_object);
                if (_id == 0 || _id == ulong.MaxValue) throw new InvalidOperationException("Invalid preview object ID");
                _backend.Emitter = _id;
                _route = new RouteBindingState(_backend, Log);
                ListenerRouting.Add(_route); _attached = true;
                Register();
                Log("create object=" + _id + " created=" + Created + " routes=" + ListenerRouting.OwnedCount);
            }
            if (!_registered) throw new InvalidOperationException("Preview registration unavailable; automatic retry refused");
        }
        private void Register()
        {
            var result = AkSoundEngine.RegisterGameObj(_object);
            if (result != AKRESULT.AK_Success) throw new InvalidOperationException("Preview register: " + result);
            _registered = true; Registrations++; _route.SetOwned(true);
        }
        public void Stop(uint playingId)
        {
            if (!_closed && !_engineUnavailable && !ListenerRouting.IsShutdown && playingId != 0) AkSoundEngine.StopPlayingID(playingId);
        }
        public void PlaybackEnded() { _callback = null; }
        public void Close(bool nativeAvailable)
        {
            if (_closed) return;
            _closed = true;
            nativeAvailable = nativeAvailable && !_engineUnavailable;
            if (_route != null) _route.SetOwned(false);
            if (_attached) { ListenerRouting.Remove(_route); _attached = false; }
            _retiredCallback = _callback; _callback = null;
            bool validId = _id != 0 && _id != ulong.MaxValue;
            if (nativeAvailable && validId)
            {
                // Scoped to our private nonzero object only. Never invoke the global StopAll overload.
                // Also covers a submission exception after native acceptance but before ID return.
                Safe(() => AkSoundEnginePINVOKE.CSharp_StopAll__SWIG_0(_id), "object-stop");
                if (Environment.CurrentManagedThreadId == _mainThread && _object != null)
                    Safe(() => AkSoundEngine.CancelEventCallbackGameObject(_object), "cancel-callbacks");
                else
                    Safe(() => AkSoundEnginePINVOKE.CSharp_CancelEventCallbackGameObject(_id), "cancel-callbacks-by-id");
                Safe(() =>
                {
                    AKRESULT result = Environment.CurrentManagedThreadId == _mainThread && _object != null
                        ? AkSoundEngine.UnregisterGameObj(_object)
                        : (AKRESULT)AkSoundEnginePINVOKE.CSharp_UnregisterGameObjInternal(_id);
                    if (result == AKRESULT.AK_Success) { _registered = false; Unregistrations++; }
                    Log("unregister submitted=" + result + " completion=unverified");
                }, "unregister");
            }
            _destroyPending = !ReferenceEquals(_object, null);
            DestroyOnMainThread();
            Log("close nativeAvailable=" + nativeAvailable + " created=" + Created +
                " registrations=" + Registrations + " unregistrations=" + Unregistrations +
                " callbackRoots=" + CallbackRoots + " routes=" + ListenerRouting.OwnedCount);
            if (_callbacksGone) _retiredCallback = null;
        }
        internal void CallbackManagerGone()
        {
            _callbacksGone = true; _callback = null; _retiredCallback = null;
            Log("callback-manager-gone callbackRoots=" + CallbackRoots);
        }
        internal void DestroyOnMainThread()
        {
            if (!_destroyPending || Environment.CurrentManagedThreadId != _mainThread) return;
            var retired = _object;
            _object = null; _destroyPending = false;
            try { if (retired != null) UnityEngine.Object.Destroy(retired); }
            catch (Exception e) { Log("unity-destroy-error (no automatic repeated calls) " + e); }
        }
        private void Safe(Action action, string operation, bool mainThreadOnly = false)
        {
            if (mainThreadOnly && Environment.CurrentManagedThreadId != _mainThread) return;
            try { action(); }
            catch (Exception e) { Log(operation + " error=" + e); }
        }
        private static void Log(string message) { PreviewService.Log(message); }
    }
}
