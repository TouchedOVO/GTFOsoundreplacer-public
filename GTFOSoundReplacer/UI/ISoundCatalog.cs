using System.Collections.Generic;

namespace GTFOSoundReplacer.UI
{
    /// <summary>音效目录抽象。GUI 只依赖此接口，与具体数据源解耦。</summary>
    public interface ISoundCatalog
    {
        IReadOnlyList<SoundEntry> Entries { get; }

        /// <summary>按关键字过滤（匹配名称 / 友好名 / ID）。空查询返回全部。</summary>
        IEnumerable<SoundEntry> Search(string query);
    }
}
