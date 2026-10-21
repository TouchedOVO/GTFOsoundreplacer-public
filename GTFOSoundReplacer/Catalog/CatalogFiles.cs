using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GTFOSoundReplacer.UI;

namespace GTFOSoundReplacer.Catalog
{
    public sealed class BankRecord
    {
        public string Path { get; set; }
        public uint Version { get; set; }
        public uint BankId { get; set; }
        public uint LanguageId { get; set; }
        public string StructureHash { get; set; }
        public string TextHash { get; set; } = "";
        public List<uint> EventIds { get; set; } = new List<uint>();
    }

    public sealed class EventRecord
    {
        public uint Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "Other";
        public List<string> BankPaths { get; set; } = new List<string>();
    }

    public sealed class CatalogDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public string Recipe { get; set; } = "bnk-118-125-structures-txt-optional-v1";
        public string SourceRoot { get; set; }
        public List<BankRecord> Banks { get; set; } = new List<BankRecord>();
        public List<EventRecord> Events { get; set; } = new List<EventRecord>();
    }

    public sealed class Translation
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public SoundCategory Category { get; set; }
    }

    // Independent of Unity / Wwise: this module never posts, prepares or loads audio.
    public static class CatalogFiles
    {
        public const int MaxJsonBytes = 8 * 1024 * 1024;
        private const int MaxStructureBytes = 64 * 1024 * 1024;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static byte[] ReadBounded(string path)
        {
            using var file = File.OpenRead(path);
            if (file.Length > MaxJsonBytes) throw new InvalidDataException("Catalog file exceeds 8 MiB");
            var data = new byte[(int)file.Length];
            file.ReadExactlyCompat(data);
            return data;
        }

        private static void ReadExactlyCompat(this Stream stream, byte[] bytes)
        {
            int pos = 0;
            while (pos < bytes.Length)
            {
                int count = stream.Read(bytes, pos, bytes.Length - pos);
                if (count == 0) throw new EndOfStreamException();
                pos += count;
            }
        }

        public static BankRecord ReadBank(string root, string path)
        {
            var bank = new BankRecord { Path = System.IO.Path.GetRelativePath(root, path) };
            using var stream = new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.Open, Access = FileAccess.Read, Share = FileShare.Read,
                BufferSize = 1, Options = FileOptions.RandomAccess
            });
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var header = new byte[8];
            bool gotHeader = false, gotHirc = false;
            var ids = new HashSet<uint>();
            while (stream.Position < stream.Length)
            {
                if (stream.Length - stream.Position < 8) throw new InvalidDataException("BNK chunk header bounds");
                stream.ReadExactlyCompat(header);
                uint size = BitConverter.ToUInt32(header, 4);
                long end = checked(stream.Position + size);
                if (end > stream.Length) throw new InvalidDataException("BNK chunk bounds");
                string tag = Encoding.ASCII.GetString(header, 0, 4);
                if (tag == "BKHD" || tag == "HIRC" || tag == "STID")
                {
                    if (size > MaxStructureBytes) throw new InvalidDataException("BNK structure exceeds 64 MiB");
                    var data = new byte[(int)size];
                    stream.ReadExactlyCompat(data);
                    hash.AppendData(header);
                    hash.AppendData(data);
                    if (tag == "BKHD")
                    {
                        if (gotHeader || size < 12) throw new InvalidDataException("Invalid BKHD");
                        bank.Version = BitConverter.ToUInt32(data, 0);
                        bank.BankId = BitConverter.ToUInt32(data, 4);
                        bank.LanguageId = BitConverter.ToUInt32(data, 8);
                        if (bank.Version != 118 && bank.Version != 125)
                            throw new NotSupportedException("Unsupported BNK version " + bank.Version);
                        gotHeader = true;
                    }
                    if (!gotHeader) throw new InvalidDataException("BKHD must precede structures");
                    if (tag == "HIRC")
                    {
                        if (gotHirc || data.Length < 4) throw new InvalidDataException("Invalid HIRC");
                        gotHirc = true;
                        uint count = BitConverter.ToUInt32(data, 0);
                        int cur = 4;
                        for (uint i = 0; i < count; i++)
                        {
                            if (data.Length - cur < 9) throw new InvalidDataException("HIRC record header bounds");
                            byte type = data[cur];
                            uint length = BitConverter.ToUInt32(data, cur + 1);
                            if (length < 4 || length > data.Length - cur - 5)
                                throw new InvalidDataException("HIRC record bounds");
                            if (type == 4)
                            {
                                uint id = BitConverter.ToUInt32(data, cur + 5);
                                if (id == 0 || !ids.Add(id)) throw new InvalidDataException("Invalid/duplicate event ID in bank");
                                bank.EventIds.Add(id);
                            }
                            cur = checked(cur + 5 + (int)length);
                        }
                        if (cur != data.Length) throw new InvalidDataException("Trailing HIRC data");
                    }
                }
                // In particular, DATA is skipped without reading its media contents.
                stream.Position = end;
            }
            if (!gotHeader) throw new InvalidDataException("Missing BKHD");
            bank.StructureHash = Convert.ToHexString(hash.GetHashAndReset());
            bank.EventIds.Sort();
            return bank;
        }

        private sealed class TextEntry
        {
            public string Name;
            public string Category;
        }

        private static Dictionary<uint, TextEntry> ParseText(byte[] bytes)
        {
            string text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            using var reader = new StringReader(text);
            var entries = new Dictionary<uint, TextEntry>();
            bool inEvents = false;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] cols = line.Split('\t');
                if (!line.StartsWith("\t", StringComparison.Ordinal))
                {
                    inEvents = cols[0].Trim() == "Event";
                    continue;
                }
                if (!inEvents) continue;
                if (cols.Length < 3 || !uint.TryParse(cols[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint id) ||
                    id == 0 || string.IsNullOrWhiteSpace(cols[2]) || cols[2].Length > 256)
                    throw new InvalidDataException("Malformed TXT event row");
                if (entries.ContainsKey(id)) throw new InvalidDataException("Duplicate TXT event ID");
                entries.Add(id, new TextEntry { Name = cols[2], Category = Classify(cols.Length > 5 ? cols[5] : "").ToString() });
            }
            return entries;
        }

        // Same ordered path rules used by the previous export tool, with no event-name table.
        public static SoundCategory Classify(string path)
        {
            string p = path.ToLowerInvariant();
            if (p.Contains("\\enemies\\") || p.Contains("\\enemies_henriks\\")) return SoundCategory.Enemy;
            if (p.Contains("\\weapons\\") || p.Contains("\\melee\\") || p.Contains("\\glue_gun\\") ||
                p.Contains("\\sentrygun\\") || p.Contains("\\stickymine\\") || p.Contains("\\bulletHit\\".ToLowerInvariant()) ||
                p.Contains("\\glue_grenade_and_mine\\")) return SoundCategory.Weapon;
            foreach (string s in new[] { "doors", "elevator", "reactor", "battery_station", "decontamination_unit", "disinfection_machine" })
                if (p.Contains("\\" + s + "\\")) return SoundCategory.Door;
            foreach (string s in new[] { "gui", "main_menu", "game_menu", "terminal", "bioscan", "hacking_puzzle", "apex_puzzle", "mapper" })
                if (p.Contains("\\" + s + "\\")) return SoundCategory.UI;
            foreach (string s in new[] { "ambience", "ambiences", "alarm", "emitters", "infestation" })
                if (p.Contains("\\" + s + "\\")) return SoundCategory.Ambience;
            foreach (string s in new[] { "player_henriks", "player", "foley", "breathing" })
                if (p.Contains("\\" + s + "\\")) return SoundCategory.Player;
            if (p.Contains("\\vo\\") || p.Contains("\\text-to-speech_voice\\")) return SoundCategory.Voice;
            if (p.Contains("\\music\\")) return SoundCategory.Music;
            foreach (string s in new[] { "tools", "consumables", "props", "pickups", "casings", "special_props", "log_recs", "biotracker", "torch", "glow_stick", "mapperdrone", "fog_repeller", "lockers_and_boxes" })
                if (p.Contains("\\" + s + "\\")) return SoundCategory.Item;
            return SoundCategory.Other;
        }

        public static CatalogDocument LoadCache(string path, string root)
        {
            var doc = JsonSerializer.Deserialize<CatalogDocument>(ReadBounded(path), JsonOptions);
            Validate(doc, root);
            return doc;
        }

        public static void Validate(CatalogDocument doc, string root)
        {
            if (doc == null || doc.SchemaVersion != 1 || doc.Recipe != "bnk-118-125-structures-txt-optional-v1" ||
                !string.Equals(doc.SourceRoot, root, StringComparison.OrdinalIgnoreCase) ||
                doc.Banks == null || doc.Banks.Count == 0 || doc.Events == null || doc.Events.Count > 100000)
                throw new InvalidDataException("Catalog cache schema/environment mismatch");
            var origins = new Dictionary<uint, HashSet<string>>();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bank in doc.Banks)
            {
                if (bank == null || string.IsNullOrEmpty(bank.Path) || System.IO.Path.IsPathRooted(bank.Path) ||
                    Array.IndexOf(bank.Path.Replace('\\', '/').Split('/'), "..") >= 0 || !paths.Add(bank.Path) ||
                    (bank.Version != 118 && bank.Version != 125) || !IsHash(bank.StructureHash) ||
                    (bank.TextHash != "" && !IsHash(bank.TextHash)) || bank.EventIds == null)
                    throw new InvalidDataException("Invalid cached bank");
                var ids = new HashSet<uint>();
                foreach (uint id in bank.EventIds)
                {
                    if (id == 0 || !ids.Add(id)) throw new InvalidDataException("Invalid cached event ID");
                    if (!origins.TryGetValue(id, out var set)) origins[id] = set = new HashSet<string>(StringComparer.Ordinal);
                    set.Add(bank.Path);
                }
            }
            var all = new HashSet<uint>();
            foreach (var entry in doc.Events)
            {
                if (entry == null || !all.Add(entry.Id) || !origins.TryGetValue(entry.Id, out var expected) ||
                    entry.Name == null || entry.Name.Length > 256 || !TryCategory(entry.Category, out _) ||
                    entry.BankPaths == null || entry.BankPaths.Count != expected.Count || !expected.SetEquals(entry.BankPaths))
                    throw new InvalidDataException("Invalid cached event/origin");
            }
            if (all.Count != origins.Count) throw new InvalidDataException("Incomplete cached event set");
        }

        private static bool IsHash(string s)
        {
            if (s == null || s.Length != 64) return false;
            foreach (char c in s) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        public static bool TryCategory(string name, out SoundCategory category)
        {
            return Enum.TryParse(name, false, out category) && Enum.IsDefined(typeof(SoundCategory), category) &&
                category.ToString() == name;
        }

        public static CatalogDocument Scan(string root, CatalogDocument previous, Action<string> warn, out bool reused)
        {
            string[] files = Directory.GetFiles(root, "*.bnk", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            if (files.Length == 0) throw new InvalidDataException("No BNK files");
            var doc = new CatalogDocument { SourceRoot = root };
            var texts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string path in files)
            {
                BankRecord bank = ReadBank(root, path);
                string txt = System.IO.Path.ChangeExtension(path, ".txt");
                try
                {
                    if (File.Exists(txt))
                    {
                        byte[] data = ReadBounded(txt);
                        using var sha = SHA256.Create();
                        bank.TextHash = Convert.ToHexString(sha.ComputeHash(data));
                        texts.Add(bank.Path, data);
                    }
                }
                catch (Exception e) { warn("Optional TXT unavailable: " + bank.Path + ": " + e.Message); }
                doc.Banks.Add(bank);
            }
            reused = previous != null && previous.Banks.Count == doc.Banks.Count;
            if (reused)
                for (int i = 0; i < doc.Banks.Count; i++)
                {
                    var a = doc.Banks[i]; var b = previous.Banks[i];
                    if (a.Path != b.Path || a.StructureHash != b.StructureHash || a.TextHash != b.TextHash ||
                        !new HashSet<uint>(a.EventIds).SetEquals(b.EventIds)) { reused = false; break; }
                }
            if (reused) return previous;

            var old = new Dictionary<uint, EventRecord>();
            if (previous != null) foreach (var e in previous.Events) old.Add(e.Id, e);
            var events = new SortedDictionary<uint, EventRecord>();
            var newNames = new Dictionary<uint, string>();
            var conflicts = new HashSet<uint>();
            foreach (BankRecord bank in doc.Banks)
            {
                var names = new Dictionary<uint, TextEntry>();
                if (texts.TryGetValue(bank.Path, out byte[] data))
                {
                    try { names = ParseText(data); }
                    catch (Exception e) { warn("Ignoring malformed optional TXT " + bank.Path + ": " + e.Message); }
                }
                foreach (uint id in bank.EventIds)
                {
                    if (!events.TryGetValue(id, out var entry))
                    {
                        entry = new EventRecord { Id = id };
                        if (old.TryGetValue(id, out var cached)) { entry.Name = cached.Name; entry.Category = cached.Category; }
                        events.Add(id, entry);
                    }
                    entry.BankPaths.Add(bank.Path);
                    if (names.TryGetValue(id, out var named))
                    {
                        if (newNames.TryGetValue(id, out string known) && known != named.Name) conflicts.Add(id);
                        else { newNames[id] = named.Name; entry.Name = named.Name; entry.Category = named.Category; }
                    }
                }
            }
            foreach (uint id in conflicts)
            {
                events[id].Name = ""; events[id].Category = "Other";
                warn("Conflicting TXT names for ID " + id + "; using ID/translation mapping");
            }
            foreach (var entry in events.Values) doc.Events.Add(entry);
            Validate(doc, root);
            return doc;
        }

        public static Dictionary<uint, Translation> ParseTranslations(byte[] bytes)
        {
            if (bytes.Length > MaxJsonBytes) throw new InvalidDataException("Translation too large");
            using var doc = JsonDocument.Parse(bytes);
            var root = doc.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("locale").GetString() != "zh-CN")
                throw new InvalidDataException("Translation schema/locale mismatch");
            var events = root.GetProperty("events");
            if (events.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid translation events");
            var result = new Dictionary<uint, Translation>();
            foreach (var prop in events.EnumerateObject())
            {
                if (result.Count >= 100000 || !uint.TryParse(prop.Name, NumberStyles.None, CultureInfo.InvariantCulture, out uint id) ||
                    id == 0 || prop.Name != id.ToString(CultureInfo.InvariantCulture) || result.ContainsKey(id))
                    throw new InvalidDataException("Invalid/duplicate translation ID");
                var value = prop.Value;
                string name = value.GetProperty("name").GetString();
                string text = value.GetProperty("zhCN").GetString();
                string cat = value.GetProperty("category").GetString();
                if (name == null || name.Length > 256 || string.IsNullOrWhiteSpace(text) || text.Length > 512 ||
                    !TryCategory(cat, out var category)) throw new InvalidDataException("Invalid translation fields");
                result.Add(id, new Translation { Name = name, Text = text, Category = category });
            }
            if (result.Count == 0) throw new InvalidDataException("Empty translation");
            return result;
        }

        public static void WriteCache(string path, CatalogDocument doc) => AtomicWrite(path, JsonSerializer.SerializeToUtf8Bytes(doc, JsonOptions));

        public static void AtomicWrite(string path, byte[] bytes)
        {
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { file.Write(bytes); file.Flush(true); }
                File.Move(temp, path, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
