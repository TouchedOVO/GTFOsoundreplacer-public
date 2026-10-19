namespace GTFOSoundReplacer.Audio
{
    /// <summary>
    /// 一条音效替换项：eventId（Wwise event ID，规范主键）→ SoundBank 替换事件。
    /// 纯 POCO（无 Unity 依赖），便于 JSON 持久化与单元测试。
    /// </summary>
    public class ReplacementEntry
    {
        /// <summary>目标游戏音效的 Wwise event ID（uint）。</summary>
        public uint EventId { get; set; }

        /// <summary>是否启用该替换。</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>音量倍率，范围0到2。</summary>
        public float Volume { get; set; } = 1.0f;

        /// <summary>替换成功时是否跳过原事件。</summary>
        public bool MuteOriginal { get; set; } = true;

        /// <summary>自定义 SoundBank 中的事件 ID（0 = 未分配）。</summary>
        public uint BankEventId { get; set; }

        public static float ClampVolume(float v)
        {
            if (v < 0f) return 0f;
            if (v > 2f) return 2f;
            return v;
        }

        /// <summary>计算最终音量 = 项音量 × 全局音量，最终夹取到 0–2（文档「0–200%」区间，避免 4.0 超区间）。</summary>
        public float EffectiveVolume(float globalVolume)
        {
            return ClampVolume(ClampVolume(Volume) * ClampVolume(globalVolume));
        }
    }
}
