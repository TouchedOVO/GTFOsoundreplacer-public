using System;
using System.Collections.Generic;
using GTFOSoundReplacer.Catalog;

namespace GTFOSoundReplacer.UI
{
    /// <summary>Completed memory snapshot; no event mapping is embedded.</summary>
    public sealed class SoundCatalog : ISoundCatalog
    {
        private readonly IReadOnlyList<SoundEntry> _entries;
        private readonly Dictionary<uint, string> _names = new Dictionary<uint, string>();
        public IReadOnlyList<SoundEntry> Entries => _entries;
        public int Count => _entries.Count;
        public SoundCatalog(CatalogDocument doc = null, IReadOnlyDictionary<uint, Translation> translations = null)
        {
            var list = new List<SoundEntry>();
            if (doc != null)
                foreach (var record in doc.Events)
                {
                    string name = record.Name ?? "";
                    string chinese = "";
                    CatalogFiles.TryCategory(record.Category, out var category);
                    if (translations != null && translations.TryGetValue(record.Id, out var t) &&
                        (name.Length == 0 || t.Name.Length == 0 || name == t.Name))
                    {
                        if (name.Length == 0) name = t.Name;
                        chinese = t.Text;
                        if (category == SoundCategory.Other) category = t.Category;
                    }
                    _names.Add(record.Id, name);
                    list.Add(new SoundEntry { EventId = record.Id, Name = name, FriendlyName = chinese, Category = category });
                }
            _entries = list.AsReadOnly();
        }
        public string FindName(uint eventId) => _names.TryGetValue(eventId, out string name) ? name : "";
        public static string LookupName(uint eventId) => CatalogService.Current.FindName(eventId);
        public static SoundCatalog LoadDefault() => CatalogService.Current;
        public IEnumerable<SoundEntry> Search(string query)
        {
            foreach (var entry in _entries)
                if (string.IsNullOrWhiteSpace(query) || entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.FriendlyName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || entry.EventId.ToString().Contains(query))
                    yield return entry;
        }
    }
}