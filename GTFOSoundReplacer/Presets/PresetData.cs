using System.Collections.Generic;
using GTFOSoundReplacer.Audio;

namespace GTFOSoundReplacer.Presets
{
    /// <summary>
    /// 预设 JSON 模型：name + entries[]（ReplacementEntry 的集合）。
    /// 纯 POCO，供 System.Text.Json 序列化到 presets\*.json。
    /// </summary>
    public class PresetData
    {
        public string Name { get; set; }

        public List<ReplacementEntry> Entries { get; set; } = new List<ReplacementEntry>();
    }
}
