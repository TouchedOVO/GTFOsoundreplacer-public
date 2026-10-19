using UnityEngine;

namespace GTFOSoundReplacer.Audio
{
    /// <summary>
    /// 音频替换核心编排器：映射表查询、静音原音决策、音量计算及 Wwise SoundBank
    /// 播放器。由 <see cref="AudioHook"/> 的 Harmony Prefix 调用。
    /// </summary>
    public class SoundReplacer
    {
        public static SoundReplacer Instance { get; private set; }

        public ReplacementMap Map { get; }
        public SoundBankPlayer SoundBank { get; }
        // Compatibility with current UI: both preview paths retain original shared emitter.
        public SoundBankPlayer PreviewBank => SoundBank;
        public SoundReplacer()
        {
            Map = new ReplacementMap();
            SoundBank = new SoundBankPlayer();
        }
        public void StopPreview() { } // Original GUI close did not stop or destroy this object.

        /// <summary>初始化全局单例（Plugin.Load 调用）。</summary>
        public static SoundReplacer Initialize()
        {
            Instance = new SoundReplacer();
            return Instance;
        }

        /// <summary>
        /// 处理一次 CellSoundPlayer.Post 调用（由 AudioHook 的 Harmony Prefix 调用）。
        /// 语义：返回 true = 命中替换项并已处理（已播放 SoundBank 替换音，或按设计无声如音量=0）；
        ///       返回 false = 未命中、已禁用、未分配事件或播放失败。
        ///       → 原音应正常透传播放。
        /// muteOriginal 输出 = 本次命中是否需要静音原音（仅返回 true 时有意义）。
        /// 静音策略保留原版：播放成功后由 AudioHook 跳过原事件，不回收游戏实例。
        /// </summary>
        public bool TryPlayReplacement(uint eventId, Vector3 position, out bool muteOriginal)
        {
            muteOriginal = false;
            if (Map == null || !Map.TryGet(eventId, out ReplacementEntry entry) || entry == null || !entry.Enabled)
                return false; // 未分配 / 条目已禁用 → 透传原音。

            // 未分配 Bank 事件（等同「未替换」）：静默透传原音。
            if (entry.BankEventId == 0u)
                return false;

            float globalVolume = PluginConfig.GlobalVolume != null ? PluginConfig.GlobalVolume.Value : 1f;
            float volume = entry.EffectiveVolume(globalVolume);
            muteOriginal = entry.MuteOriginal;

            if (volume <= 0f)
                return true; // 音量 0：不播替换音；是否停原音由 muteOriginal 决定（默认静音 → 原音也不播）。

            bool played = false;
            try
            {
                played = SoundBank.Play(entry, position, volume);
            }
            catch (System.Exception e)
            {
                played = false;
                if (Plugin.Instance != null)
                    Plugin.Instance.Log.LogWarning("Replacement play failed for event " + eventId + ": " + e.Message);
            }

            if (!played)
            {
                // 替换提交失败时放行原事件。
                muteOriginal = false;
                if (Plugin.Instance != null)
                    Plugin.Instance.Log.LogWarning("Replacement unavailable for event " + eventId + " (bank not loaded or event missing); falling back to original sound.");
                return false;
            }

            return true;
        }
    }
}
