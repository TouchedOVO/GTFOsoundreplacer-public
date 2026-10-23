using System.Threading;

namespace GTFOSoundReplacer.Audio
{
    // Independent request identity. Never infer it from mutable PlayerVoice fields.
    internal class VoiceRequest
    {
        internal readonly uint OriginalId, TargetId, DialogId;
        internal readonly long Voice, Sound;
        private long _playing;
        private int _ended;
        internal uint PlayingId => (uint)Interlocked.Read(ref _playing);
        internal bool Ended => Volatile.Read(ref _ended) != 0;
        internal VoiceRequest(uint original, uint target, uint dialog, long voice, long sound)
        { OriginalId = original; TargetId = target; DialogId = dialog; Voice = voice; Sound = sound; }
        internal bool Bind(uint id)
        {
            if (id == 0) return false;
            long prior = Interlocked.CompareExchange(ref _playing, id, 0);
            return prior == 0 || prior == id;
        }
        internal bool Matches(uint eventId, uint playingId) => eventId == TargetId && Bind(playingId);
        internal bool ClaimEnd() => Interlocked.CompareExchange(ref _ended, 1, 0) == 0;
        internal bool MatchesCompletion(uint eventId, uint dialogId) => eventId == TargetId && dialogId == DialogId;
        internal static bool MatchesSource(long scopedSound, uint scopedEvent, long sound, uint eventId)
            => scopedSound != 0 && scopedSound == sound && scopedEvent == eventId;
    }
}
