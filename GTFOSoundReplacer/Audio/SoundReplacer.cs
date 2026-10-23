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
        public SoundReplacer()
        {
            Map = new ReplacementMap();
            SoundBank = new SoundBankPlayer();
        }
        public void StopPreview() { PreviewService.Stop("user-stop-or-window-close"); }

        /// <summary>初始化全局单例（Plugin.Load 调用）。</summary>
        public static SoundReplacer Initialize()
        {
            Instance = new SoundReplacer();
            return Instance;
        }

        /// <summary>返回true表示原声应跳过：替换已提交，或有效音量为0。</summary>
        public bool TryPlayReplacement(uint eventId, Vector3 position)
        {
            var decision = ReplacementResolver.Resolve(Map, eventId,
                PluginConfig.GlobalVolume == null ? 1f : PluginConfig.GlobalVolume.Value);
            if (decision.Kind == ReplacementKind.Original) return false;
            if (decision.Kind == ReplacementKind.Silent) return true;
            try
            {
                if (SoundBank.Play(decision.Entry, position, decision.Volume)) return true;
            }
            catch (System.Exception e)
            {
                Plugin.Instance?.Log.LogWarning("Replacement play failed for event " + eventId + ": " + e.Message);
            }
            Plugin.Instance?.Log.LogWarning("Replacement unavailable for event " + eventId + "; falling back to original sound.");
            return false;
        }
    }
}
