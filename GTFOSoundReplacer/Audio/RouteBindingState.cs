using System;

namespace GTFOSoundReplacer.Audio
{
    internal interface IRouteBindingBackend
    {
        bool ResetToDefault();
        bool SetOutputGain(float gain);
    }

    // All calls are serialized by ListenerRouting.Gate, including the original
    // default-listener submission and plugin PostEvent. Results mean submission,
    // not audio-thread completion; the observer must verify actual routing.
    internal sealed class RouteBindingState
    {
        private readonly IRouteBindingBackend _backend;
        private readonly Action<string> _log;
        private bool _independent;
        internal float OutputGain { get; private set; } = 1f;
        internal bool FollowsDefaults { get; private set; } = true;
        internal bool IsOwned { get; private set; }
        internal bool NeedsRepair { get; private set; }

        internal RouteBindingState(IRouteBindingBackend backend, Action<string> log = null)
        { _backend = backend ?? throw new ArgumentNullException(nameof(backend)); _log = log; }

        internal void SetOwned(bool registered)
        {
            IsOwned = registered;
            if (!registered)
            {
                OutputGain = 1f;
                _independent = false;
                NeedsRepair = false;
            }
        }
        internal void SetFollowDefaults(bool follow)
        {
            if (follow && !FollowsDefaults) NeedsRepair = true;
            FollowsDefaults = follow;
        }

        internal void DefaultListenersSubmitted(bool success)
        {
            if (!success || !IsOwned || !FollowsDefaults || (!_independent && !NeedsRepair)) return;
            NeedsRepair = true;
            PreparePost();
        }

        internal bool PreparePost()
        {
            if (!NeedsRepair || !FollowsDefaults) return true;
            if (!IsOwned) return false;
            if (!SubmitReset()) return false;
            if (Math.Abs(OutputGain - 1f) >= 0.0001f)
            {
                if (!SubmitGain(OutputGain)) return false;
                _independent = true;
            }
            NeedsRepair = false;
            return true;
        }

        internal bool ApplyOutputGain(float gain)
        {
            if (!IsOwned) return false;
            if (!PreparePost()) return false;
            if (FollowsDefaults && Math.Abs(gain - 1f) < 0.0001f)
            {
                if (_independent && !SubmitReset()) { NeedsRepair = true; return false; }
                OutputGain = 1f;
                return true;
            }
            if (!SubmitGain(gain)) return false;
            OutputGain = gain;
            _independent = true;
            return true;
        }

        private bool SubmitReset()
        {
            try
            {
                if (_backend.ResetToDefault()) { _independent = false; return true; }
                _log?.Invoke("ResetListenersToDefault submission failed");
            }
            catch (Exception e) { _log?.Invoke("ResetListenersToDefault failed: " + e.Message); }
            return false;
        }
        private bool SubmitGain(float gain)
        {
            try
            {
                if (_backend.SetOutputGain(gain)) return true;
                _log?.Invoke("Output gain submission failed: " + gain);
            }
            catch (Exception e) { _log?.Invoke("Output gain failed: " + e.Message); }
            return false;
        }
    }
}
