using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GTFOSoundReplacer.Catalog;
using GTFOSoundReplacer.UI;

public static class CatalogChecks
{
    private static int count;
    private static void Check(bool test, string message) { if (!test) throw new Exception("FAIL: " + message); count++; }
    private static void Reject(Action action, string message)
    {
        bool failed = false;
        try { action(); } catch (Exception) { failed = true; }
        Check(failed, message);
    }
    private static void Bank(string path, params uint[] ids)
    {
        using var file = File.Create(path);
        using var writer = new BinaryWriter(file);
        writer.Write(Encoding.ASCII.GetBytes("BKHD")); writer.Write(12u);
        writer.Write(125u); writer.Write(100u); writer.Write(0u);
        writer.Write(Encoding.ASCII.GetBytes("HIRC")); writer.Write((uint)(4 + 9 * ids.Length)); writer.Write((uint)ids.Length);
        foreach (uint id in ids) { writer.Write((byte)4); writer.Write(4u); writer.Write(id); }
        writer.Write(Encoding.ASCII.GetBytes("DATA")); writer.Write(1024u); writer.Write(new byte[1024]);
    }
    private static byte[] Translation(string text = "测试中文") => Encoding.UTF8.GetBytes(
        "{\"schemaVersion\":1,\"locale\":\"zh-CN\",\"events\":{\"11\":{\"name\":\"TestEvent\",\"zhCN\":\"" + text + "\",\"category\":\"Weapon\"}}}");

    public static int Run()
    {
        count = 0;
        string folder = Path.Combine(Path.GetTempPath(), "gtfo-catalog-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string source = Path.Combine(folder, "banks"), cache = Path.Combine(folder, "cache");
            Directory.CreateDirectory(source); Directory.CreateDirectory(cache);
            string a = Path.Combine(source, "a.bnk"), b = Path.Combine(source, "b.bnk");
            Bank(a, 11, 22); Bank(b, 11, 33);
            var warnings = new List<string>();
            var doc = CatalogFiles.Scan(source, null, warnings.Add, out bool reused);
            Check(!reused && doc.Events.Count == 3, "BNK-only first startup deduplicates IDs");
            Check(doc.Events[0].BankPaths.Count == 2, "duplicate ID retains physical origins");
            Check(doc.Events[0].Name == "", "BNK-only does not invent names");
            var bare = new SoundCatalog(doc);
            Check(bare.Entries[0].DisplayName == "11", "BNK-only numeric fallback");
            Check(bare.FindName(11) == "" && bare.FindName(99) == "", "unknown name lookup stays empty");
            Check(CatalogFiles.Scan(source, doc, warnings.Add, out reused) == doc && reused, "unchanged structure cache reused");
            string hash = doc.Banks[0].StructureHash;
            using (var file = new FileStream(a, FileMode.Open, FileAccess.Write)) { file.Position = file.Length - 1; file.WriteByte(127); }
            Check(CatalogFiles.ReadBank(source, a).StructureHash == hash, "media DATA content excluded from fingerprint");
            File.WriteAllText(Path.ChangeExtension(a, ".txt"), "Event\tID\tName\t\t\tWwise Object Path\tNotes\n\t11\tTestEvent\t\t\t\\Henkes\\Weapons\\test\n\t999\tStaleNotInBank\t\t\t\\Henkes\\Weapons\\test\n", new UTF8Encoding(false));
            var named = CatalogFiles.Scan(source, doc, warnings.Add, out reused);
            Check(!reused && named.Events.Count == 3 && named.Events[0].Name == "TestEvent", "optional TXT maps only real BNK event IDs");
            Check(named.Events[0].Category == "Weapon", "TXT object path supplies category");
            Check(named.Events[1].Name == "", "missing TXT row still keeps BNK ID");
            string cachePath = Path.Combine(cache, "events.cache.json");
            CatalogFiles.WriteCache(cachePath, named);
            var loaded = CatalogFiles.LoadCache(cachePath, source);
            Check(loaded.Events.Count == 3 && loaded.Events[0].BankPaths.Count == 2, "cache round trip preserves origins");
            File.Delete(Path.ChangeExtension(a, ".txt"));
            var withoutText = CatalogFiles.Scan(source, loaded, warnings.Add, out reused);
            Check(withoutText.Events[0].Name == "TestEvent", "removed TXT falls back to cached names");
            Bank(a, 11, 44);
            var changed = CatalogFiles.Scan(source, withoutText, warnings.Add, out reused);
            Check(!reused && changed.Events.Count == 3 && changed.Events[1].Id == 33 && changed.Events[2].Id == 44, "structure mutation rebuilds exact current ID set");
            var translated = CatalogFiles.ParseTranslations(Translation());
            var snapshot = new SoundCatalog(doc, translated);
            Check(snapshot.Entries[0].FriendlyName == "测试中文" && snapshot.FindName(11) == "TestEvent", "translation supplies Chinese and missing original name");
            Check(bare.Entries[0].FriendlyName == "" && bare.FindName(11) == "", "publishing new snapshot cannot mutate old snapshot");
            Check(snapshot.Entries[0].GetDisplayName(false) == "TestEvent", "translation disabled shows original name");
            Check(snapshot.Entries[0].GetDisplayName(true) == "测试中文 (TestEvent)", "translation enabled shows Chinese and original name");
            Check(bare.Entries[0].GetDisplayName(false) == "11", "translation disabled retains numeric fallback");

            Check(new List<SoundEntry>(snapshot.Search("中文")).Count == 1, "Chinese search");
            Check(new List<SoundEntry>(snapshot.Search("testevent")).Count == 1, "case insensitive original-name search");
            Check(new List<SoundEntry>(snapshot.Search("11")).Count == 1, "numeric search");
            var mismatch = new CatalogDocument { Events = new List<EventRecord> { new EventRecord { Id = 11, Name = "DifferentName" } } };
            Check(new SoundCatalog(mismatch, translated).Entries[0].FriendlyName == "", "contradictory remote name does not override local name");
            Reject(() => CatalogFiles.ParseTranslations(Encoding.UTF8.GetBytes("{")), "malformed translations rejected");
            string good = Encoding.UTF8.GetString(Translation());
            Reject(() => CatalogFiles.ParseTranslations(Encoding.UTF8.GetBytes(good.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"))), "translation schema checked");
            Reject(() => CatalogFiles.ParseTranslations(Encoding.UTF8.GetBytes(good.Replace("\"Weapon\"", "\"999\""))), "undefined category rejected");
            string value = "{\"name\":\"a\",\"zhCN\":\"甲\",\"category\":\"Other\"}";
            Reject(() => CatalogFiles.ParseTranslations(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"locale\":\"zh-CN\",\"events\":{\"11\":" + value + ",\"11\":" + value + "}}")), "duplicate translation IDs rejected");
            Reject(() => CatalogFiles.ParseTranslations(new byte[CatalogFiles.MaxJsonBytes + 1]), "oversized translations rejected");
            loaded.Events[0].BankPaths.Clear();
            Reject(() => CatalogFiles.Validate(loaded, source), "missing cached origin rejected");
            Reject(() => CatalogFiles.LoadCache(cachePath, folder), "cache cannot cross game roots");

            // Exercise the real initialization/publication/file replacement path with offline transport.
            string localTranslation = Path.Combine(cache, "zh-CN.json");
            File.WriteAllBytes(localTranslation, Translation("旧中文"));
            CatalogService.Initialize(source, cache, "offline-test", _ => { }, warnings.Add,
                _ => Task.FromResult(Translation("新中文"))).GetAwaiter().GetResult();
            Check(CatalogService.Current.Entries[0].FriendlyName == "新中文", "download success publishes snapshot");
            Check(Encoding.UTF8.GetString(File.ReadAllBytes(localTranslation)).Contains("新中文"), "download success atomically updates local cache");
            byte[] saved = File.ReadAllBytes(localTranslation);
            CatalogService.Initialize(source, cache, "offline-test", _ => { }, warnings.Add,
                _ => Task.FromResult(Encoding.UTF8.GetBytes("{"))).GetAwaiter().GetResult();
            Check(CatalogService.Current.Entries[0].FriendlyName == "新中文", "invalid download retains cached translation");
            Check(Convert.ToBase64String(saved) == Convert.ToBase64String(File.ReadAllBytes(localTranslation)), "invalid download leaves cache byte identical");
            CatalogService.Initialize(source, cache, "offline-test", _ => { }, warnings.Add,
                _ => Task.FromException<byte[]>(new IOException("offline"))).GetAwaiter().GetResult();
            Check(CatalogService.Current.Entries[0].FriendlyName == "新中文", "network failure retains cached translation");
            Check(CatalogService.Status.Contains("下载失败"), "offline state visible in UI status");
            byte[] bankBytes = File.ReadAllBytes(a); File.WriteAllText(a, "broken");
            CatalogService.Initialize(source, cache, "", _ => { }, warnings.Add).GetAwaiter().GetResult();
            Check(CatalogService.Status.Contains("旧缓存") && CatalogService.Current.Count == 3, "scan failure explicitly marks stale directory fallback");
            File.WriteAllBytes(a, bankBytes);
            Reject(() => CatalogService.Download("http://invalid.example/test").GetAwaiter().GetResult(), "non-HTTPS URL rejected without network");
            Reject(() => CatalogService.Download("https://user:pass@invalid.example/test").GetAwaiter().GetResult(), "credential URL rejected without network");
            File.WriteAllText(a, "bad");
            Reject(() => CatalogFiles.ReadBank(source, a), "truncated BNK rejected");
            Check(CatalogFiles.Classify("\\Simons\\music\\example") == SoundCategory.Music, "music path classification");
            return count;
        }
        finally
        {
            string full = Path.GetFullPath(folder);
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("gtfo-catalog-checks-", StringComparison.Ordinal))
                Directory.Delete(full, true);
        }
    }
}
