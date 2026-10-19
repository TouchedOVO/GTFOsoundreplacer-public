using System.Collections.Generic;

namespace GTFOSoundReplacer.UI
{
    /// <summary>
    /// 默认音效目录实现：
    /// 主目录 = 嵌入的 .txtp 全量目录（SoundCatalogData.generated.cs，2244 个唯一事件，
    /// 由tools/gen-catalog.ps1解析txtp导出数据并按objpath分类）。
    /// 在嵌入目录之上叠加 curated 中文友好名（仅覆盖 FriendlyName，不改变分类），
    /// 显示为「中文友好名 (英文事件名)」。
    /// 运行时不再解析游戏 Wwise_IDs.cs（旧分类过粗且部分 ID 有误，见 docs/CATALOG_DESIGN.md）。
    /// </summary>
    public class SoundCatalog : ISoundCatalog
    {
        private readonly List<SoundEntry> _entries = new List<SoundEntry>();
        private readonly List<uint> _eventIds = new List<uint>();

        public IReadOnlyList<SoundEntry> Entries => _entries;

        public int Count => _entries.Count;

        private static readonly object NameLookupLock = new object();
        private static Dictionary<uint, string> _nameLookup;

        /// <summary>
        /// 按 eventId 查事件名（用于日志定位音效文件；懒构建 2244 条反向索引）。
        /// 线程安全：索引先在局部变量中构建完毕再发布，读取方不会看到半成品字典；
        /// 若构建抛异常则不发布，下次调用会重新尝试（配合调用方的 try/catch，日志逻辑不会卡顿/崩溃）。
        /// </summary>
        public static string LookupName(uint eventId)
        {
            Dictionary<uint, string> lookup = _nameLookup;
            if (lookup == null)
            {
                lock (NameLookupLock)
                {
                    lookup = _nameLookup;
                    if (lookup == null)
                    {
                        var built = new Dictionary<uint, string>(SoundCatalogData.Entries.Length);
                        foreach (SoundCatalogData.Entry e in SoundCatalogData.Entries)
                        {
                            if (!built.ContainsKey(e.EventId))
                                built[e.EventId] = e.Name;
                        }
                        _nameLookup = built;
                        lookup = built;
                    }
                }
            }
            if (lookup.TryGetValue(eventId, out string name))
                return name;
            return "";
        }

        public void Add(SoundEntry entry)
        {
            if (entry == null || entry.EventId == 0) return;
            // 按 EventId 去重（先加入者优先）。
            if (_eventIds.Contains(entry.EventId)) return;
            _eventIds.Add(entry.EventId);
            _entries.Add(entry);
        }

        public void Append(IEnumerable<SoundEntry> entries)
        {
            if (entries == null) return;
            foreach (SoundEntry e in entries) Add(e);
        }

        public IEnumerable<SoundEntry> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                foreach (SoundEntry e in _entries) yield return e;
                yield break;
            }

            string q = query.ToLowerInvariant();
            foreach (SoundEntry e in _entries)
            {
                if ((e.Name != null && e.Name.ToLowerInvariant().Contains(q)) ||
                    (e.FriendlyName != null && e.FriendlyName.ToLowerInvariant().Contains(q)) ||
                    e.EventId.ToString().Contains(q))
                {
                    yield return e;
                }
            }
        }

        /// <summary>构建主目录（嵌入的 .txtp 目录 + curated 中文友好名）。</summary>
        public static SoundCatalog LoadDefault()
        {
            var catalog = new SoundCatalog();

            // 主目录：嵌入数据（Name=事件名，EventId，Category 已按 objpath 分类）。
            foreach (SoundCatalogData.Entry data in SoundCatalogData.Entries)
            {
                catalog.Add(new SoundEntry
                {
                    EventId = data.EventId,
                    Name = data.Name,
                    FriendlyName = "",
                    Category = data.Category,
                });
            }

            // curated 中文友好名（仅覆盖 FriendlyName；事件名/分类保持嵌入目录的权威值）。
            foreach (CuratedName c in Curated)
                catalog.SetFriendlyName(c.EventId, c.FriendlyName);

            return catalog;
        }

        private void SetFriendlyName(uint eventId, string friendlyName)
        {
            foreach (SoundEntry e in _entries)
            {
                if (e.EventId == eventId)
                {
                    e.FriendlyName = friendlyName;
                    return;
                }
            }
        }

        /// <summary>curated 中文友好名（ID 均经 .txtp 权威目录核实存在，已删除旧表错误 ID）。</summary>
        private readonly struct CuratedName
        {
            public readonly uint EventId;
            public readonly string FriendlyName;

            public CuratedName(uint eventId, string friendlyName)
            {
                EventId = eventId;
                FriendlyName = friendlyName;
            }
        }

        private static readonly CuratedName[] Curated = new CuratedName[]
        {
            // 敌人（真实事件名见 SoundCatalogData，此处仅加中文标签）
            new CuratedName(2377057307u, "Striker 战斗长啸"),
            new CuratedName(225404576u, "Striker 死亡"),
            new CuratedName(3821460214u, "Striker 受击(重)"),
            new CuratedName(3228683296u, "Striker Child 战斗"),
            new CuratedName(3821690095u, "Striker Child 死亡"),
            new CuratedName(2803582429u, "Striker Child 受击(重)"),
            new CuratedName(352123317u, "Shadow 尖啸"),
            new CuratedName(880873801u, "Shadow 受击(重)"),
            new CuratedName(1082079783u, "Flyer 尖啸"),
            new CuratedName(242958158u, "Flyer 死亡"),
            new CuratedName(2652400352u, "Flyer 受击(重)"),
            new CuratedName(748973542u, "Flyer Big 尖啸"),
            new CuratedName(884101509u, "Flyer Big 死亡"),
            new CuratedName(2044858532u, "Striker Big 战斗"),
            new CuratedName(3068236291u, "Striker Big 死亡"),
            new CuratedName(530125293u, "Tank 战斗"),
            new CuratedName(1431152350u, "Tank 死亡"),
            new CuratedName(1148323316u, "Tank Boss 战斗"),
            new CuratedName(3978524414u, "Shooter 战斗尖叫"),
            new CuratedName(2542276107u, "Shooter 攻击"),
            new CuratedName(3580112372u, "Shooter 死亡尖叫"),
            new CuratedName(493397974u, "Scout 死亡尖叫"),
            new CuratedName(736704011u, "Scout 侦测尖叫"),
            new CuratedName(1242392806u, "Birther 死亡尖叫"),
            new CuratedName(334153491u, "Birther Boss 战斗"),
            new CuratedName(778541630u, "Birther Boss 受击(重)"),
            new CuratedName(2040824805u, "Pouncer 攻击蓄力"),
            new CuratedName(1850167424u, "Pouncer 死亡"),
            new CuratedName(4268530262u, "Pouncer 受击(重)"),
            // 近战
            new CuratedName(2360028946u, "近战蓄力"),
        };
    }
}
