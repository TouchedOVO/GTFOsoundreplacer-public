using System.Collections.Concurrent;
using System.Collections.Generic;

namespace GTFOSoundReplacer.Audio
{
    /// <summary>
    /// eventId → ReplacementEntry 的并发安全映射表。
    /// 使用 <see cref="ConcurrentDictionary{TKey,TValue}"/>，支持在 Harmony 补丁
    /// （音频线程）与 GUI（主线程）之间安全读写。
    /// </summary>
    public class ReplacementMap
    {
        private readonly ConcurrentDictionary<uint, ReplacementEntry> _entries =
            new ConcurrentDictionary<uint, ReplacementEntry>();

        public int Count => _entries.Count;

        public bool TryGet(uint eventId, out ReplacementEntry entry)
        {
            return _entries.TryGetValue(eventId, out entry);
        }

        public void Set(uint eventId, ReplacementEntry entry)
        {
            if (entry == null) return;
            entry.EventId = eventId;
            _entries[eventId] = entry;
        }

        public bool Remove(uint eventId)
        {
            return _entries.TryRemove(eventId, out _);
        }

        public void Clear()
        {
            _entries.Clear();
        }

        public bool Contains(uint eventId)
        {
            return _entries.ContainsKey(eventId);
        }

        /// <summary>快照迭代（安全遍历，改动不抛异常）。</summary>
        public IEnumerable<KeyValuePair<uint, ReplacementEntry>> Entries => _entries;
    }
}
