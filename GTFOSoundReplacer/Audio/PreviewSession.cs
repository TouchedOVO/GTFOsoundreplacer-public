using System;
using System.Threading;

namespace GTFOSoundReplacer.Audio
{
    internal sealed class PreviewRequest
    {
        internal readonly uint EventId;
        internal readonly float X, Y, Z, Volume;
        internal readonly bool Voice;
        internal readonly string Label;
        internal PreviewRequest(uint eventId, float x, float y, float z, float volume, bool voice, string label)
        { EventId = eventId; X = x; Y = y; Z = z; Volume = volume; Voice = voice; Label = label; }
    }

    internal interface IPreviewBackend
    {
        uint Post(PreviewRequest request, Action ended);
        void Stop(uint playingId);
        void PlaybackEnded();
        void Close(bool nativeAvailable);
    }

    internal enum PreviewState { Idle, Playing, Stopping, Faulted, Closed }

    // Main-thread state machine. A callback can only mark its own immutable ticket.
    // No Unity, Wwise, tasks, polling of native state, or growing callback queue here.
    internal sealed class PreviewSession
    {
        private sealed class Ticket
        {
            internal long Number, RequestTick, PostTick, StopTick, EndTick;
            internal uint PlayingId;
            internal int Ended, EndClaimed;
            internal bool StopSent;
        }
        private readonly IPreviewBackend _backend;
        private readonly Func<long> _clock;
        private readonly long _frequency;
        private readonly Action<string, bool> _status;
        private readonly Action<string> _log;
        private volatile Ticket _current;
        private PreviewRequest _pending;
        private bool _backendClosed;
        private long _pendingTick, _sequence;
        private int _lateCallbacks;
        internal PreviewState State { get; private set; }
        internal bool NeedsTick => State != PreviewState.Closed && _current != null;
        internal uint PlayingId => _current == null ? 0 : _current.PlayingId;
        internal bool HasPending => _pending != null;
        internal int Posts { get; private set; }
        internal int Ends { get; private set; }
        internal int Stops { get; private set; }
        internal int Superseded { get; private set; }
        internal int LateCallbacks => Volatile.Read(ref _lateCallbacks);

        internal PreviewSession(IPreviewBackend backend, Func<long> clock, long frequency,
            Action<string, bool> status = null, Action<string> log = null)
        {
            _backend = backend; _clock = clock; _frequency = frequency;
            _status = status; _log = log;
        }
        internal void Request(PreviewRequest request)
        {
            if (State == PreviewState.Closed) { Notify("试听已关闭", true); return; }
            if (request == null || request.EventId == 0 || float.IsNaN(request.Volume) || float.IsInfinity(request.Volume))
            { Notify("试听参数无效", true); return; }
            if (_pending != null) Superseded++;
            _pending = request; _pendingTick = _clock();
            Tick();
            if (State == PreviewState.Faulted)
            { _pending = null; Notify("试听状态异常，请先停止或重新启动游戏", true); return; }
            // Tick may already have consumed the latest request after an old End arrived.
            if (_pending == null) return;
            if (_current == null) StartPending();
            else StopCurrent();
        }
        internal void Stop(string reason)
        {
            _pending = null;
            if (State == PreviewState.Closed) return;
            Tick();
            if (_current == null) { Notify("试听已停止", false); return; }
            _log?.Invoke("stop-request reason=" + reason);
            StopCurrent();
        }
        private void StopCurrent()
        {
            var ticket = _current;
            if (ticket == null || ticket.StopSent || Volatile.Read(ref ticket.Ended) != 0) return;
            ticket.StopSent = true; ticket.StopTick = _clock();
            State = PreviewState.Stopping;
            try
            {
                _backend.Stop(ticket.PlayingId); Stops++;
                _log?.Invoke("stop number=" + ticket.Number + " playingId=" + ticket.PlayingId);
            }
            catch (Exception e) { Fault("停止试听失败", e); }
        }
        internal void Tick()
        {
            var ticket = _current;
            if (State == PreviewState.Closed || ticket == null) return;
            if (Volatile.Read(ref ticket.Ended) != 0)
            {
                long now = _clock(); Ends++;
                _current = null;
                bool wasFaulted = State == PreviewState.Faulted;
                State = PreviewState.Idle;
                try { _backend.PlaybackEnded(); }
                catch (Exception e) { Fault("释放试听回调失败", e); return; }
                _log?.Invoke("end number=" + ticket.Number + " playingId=" + ticket.PlayingId +
                    " stopToCallbackMs=" + (ticket.StopSent ? Ms(ticket.EndTick - ticket.StopTick) : "natural") +
                    " callbackToUpdateMs=" + Ms(now - ticket.EndTick));
                if (_pending != null && !wasFaulted) StartPending();
                else Notify("试听已结束", false);
            }
            else if (State == PreviewState.Stopping && _clock() - ticket.StopTick >= _frequency)
            {
                State = PreviewState.Faulted; _pending = null;
                Notify("停止结果未确认，已暂停新试听", true);
                _log?.Invoke("stop-timeout playingId=" + ticket.PlayingId + " retained=true; no automatic retry/recreate");
            }
        }
        private void StartPending()
        {
            if (_pending == null || State == PreviewState.Closed || _current != null) return;
            PreviewRequest request = _pending;
            var ticket = new Ticket { Number = ++_sequence, RequestTick = _pendingTick };
            _pending = null;
            _current = ticket; // Publish before Post: even a synchronous callback cannot be lost.
            State = PreviewState.Playing;
            long began = _clock();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                ticket.PlayingId = _backend.Post(request, () =>
                {
                    if (Interlocked.CompareExchange(ref ticket.EndClaimed, 1, 0) != 0)
                    { Interlocked.Increment(ref _lateCallbacks); return; }
                    if (!ReferenceEquals(_current, ticket)) Interlocked.Increment(ref _lateCallbacks);
                    Interlocked.Exchange(ref ticket.EndTick, _clock());
                    Volatile.Write(ref ticket.Ended, 1);
                });
                ticket.PostTick = _clock();
                if (ticket.PlayingId == 0)
                {
                    _current = null; State = PreviewState.Faulted;
                    // This SDK creates a callback package before native Post. Bound failed submissions
                    // to one backend lifetime rather than accumulating unknown zero-ID packages.
                    CloseBackend(true);
                    Notify("试听提交失败，本轮试听已停用；检查音库后重启", true);
                    _log?.Invoke("post-failed number=" + ticket.Number + " eventId=" + request.EventId);
                    return;
                }
                Posts++;
                _log?.Invoke("post number=" + ticket.Number + " eventId=" + request.EventId +
                    " playingId=" + ticket.PlayingId + " voice=" + request.Voice +
                    " requestToPostMs=" + Ms(ticket.PostTick - ticket.RequestTick) +
                    " submitMs=" + Ms(ticket.PostTick - began) +
                    " submitManagedAllocatedBytes=" + (GC.GetAllocatedBytesForCurrentThread() - allocated));
                Notify(request.Voice ? "试听已提交：角色 01 / 探索" : "试听已提交", false);
            }
            catch (Exception e) { Fault("试听出错，已暂停新试听", e); }
        }
        internal void Close(bool nativeAvailable)
        {
            if (State == PreviewState.Closed) return;
            State = PreviewState.Closed; _pending = null;
            var ticket = _current;
            _current = null;
            if (ticket != null && Volatile.Read(ref ticket.Ended) != 0)
            {
                Ends++;
                try { _backend.PlaybackEnded(); }
                catch (Exception e) { _log?.Invoke("close-end-reference-error " + e); }
            }
            // Stop is finite, even if an earlier stop failed or timed out. Teardown never waits for End.
            try
            {
                if (nativeAvailable && ticket != null && ticket.PlayingId != 0 && Volatile.Read(ref ticket.Ended) == 0)
                    _backend.Stop(ticket.PlayingId);
            }
            catch (Exception e) { _log?.Invoke("close-stop-error " + e); }
            CloseBackend(nativeAvailable);
            _log?.Invoke("closed posts=" + Posts + " ends=" + Ends + " stops=" + Stops + " superseded=" + Superseded + " ignoredLateCallbacks=" + LateCallbacks);
        }
        private string Ms(long ticks) => (ticks * 1000.0 / _frequency).ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        private void Fault(string message, Exception e)
        {
            State = PreviewState.Faulted; _pending = null;
            Notify(message, true); _log?.Invoke("fault " + e);
            // A thrown native boundary can leave an accepted event without a returned ID.
            // Retire this one backend, never retry/create another behind the user's back.
            _current = null;
            CloseBackend(true);
        }
        private void CloseBackend(bool nativeAvailable)
        {
            if (_backendClosed) return;
            _backendClosed = true;
            try { _backend.Close(nativeAvailable); }
            catch (Exception e) { _log?.Invoke("close-backend-error " + e); }
        }
        private void Notify(string message, bool failed) { _status?.Invoke(message, failed); }
    }
}
