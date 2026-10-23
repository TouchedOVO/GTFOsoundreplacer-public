namespace GTFOSoundReplacer.Audio
{
    internal enum ReplacementKind { Original, Silent, Replace }
    internal readonly struct ReplacementDecision
    {
        internal readonly ReplacementKind Kind;
        internal readonly ReplacementEntry Entry;
        internal readonly uint TargetId;
        internal readonly float Volume;
        internal ReplacementDecision(ReplacementEntry entry, float volume)
        { Entry = entry; TargetId = entry.BankEventId; Volume = volume; Kind = volume <= 0 ? ReplacementKind.Silent : ReplacementKind.Replace; }
    }
    internal static class ReplacementResolver
    {
        internal static ReplacementDecision Resolve(ReplacementMap map, uint eventId, float global, bool fixedUnitVolume = false)
        {
            if (map == null || !map.TryGet(eventId, out var entry) || entry == null || !entry.Enabled || entry.BankEventId == 0)
                return default;
            return new ReplacementDecision(entry, fixedUnitVolume ? 1f : entry.EffectiveVolume(global));
        }
    }
}
