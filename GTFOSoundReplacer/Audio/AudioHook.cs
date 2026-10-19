using System;
using HarmonyLib;
using UnityEngine;
using GTFOSoundReplacer.UI;

namespace GTFOSoundReplacer.Audio
{
    /// <summary>
    /// 主钩子共享逻辑。CellSoundPlayer.Post 共有 7 个重载（3D 带位置 / 2D global / 带回调等）。
    /// 覆盖其中 4 个无回调重载（uint/string × Vector3/bool）。带回调的 3 个重载暂不覆盖。
    ///
    /// 替换提交成功且配置静音原音时，Prefix返回false，跳过原Post。
    /// 其他情况放行原Post，不主动回收游戏对象。
    /// </summary>
    internal static class AudioHook
    {
        /// <summary>
        /// 共享处理（Prefix）。返回 false = 跳过原事件（静音原音）；true = 放行原事件。
        /// 命中替换项时在此处同步播放 SoundBank 替换音（TryPlayReplacement 内部）。
        /// </summary>
        internal static bool PrefixCore(uint eventId, Vector3 position)
        {
            // [可选诊断] 按配置打印每个音效事件的 ID/名称，用于定位音效文件（默认关闭）。
            if (PluginConfig.LogEventIds != null && PluginConfig.LogEventIds.Value)
                LogEventId(eventId);

            SoundReplacer replacer = SoundReplacer.Instance;
            if (replacer == null)
                return true;

            bool handled = replacer.TryPlayReplacement(eventId, position, out bool muteOriginal);
            // handled && muteOriginal → 跳过原 Post（静音原音）。不 Recycle。
            return !(handled && muteOriginal);
        }

        /// <summary>是否已就名称目录失败告警过一次（避免每个事件刷屏）。</summary>
        private static bool _lookupWarned;

        /// <summary>
        /// 打印 [SR-ID] 诊断行。id 与 name 分步容错：
        /// 名称反向索引（SoundCatalog.LookupName，懒构建 2244 条）即使抛异常，id 部分也一定照常打印；
        /// 任何一步异常都被就地消化并（仅首次）告警，绝不向音频热路径冒泡。
        /// </summary>
        private static void LogEventId(uint eventId)
        {
            string namePart;
            try
            {
                namePart = SoundCatalog.LookupName(eventId);
            }
            catch (Exception e)
            {
                namePart = "<lookup-error>";
                if (!_lookupWarned)
                {
                    _lookupWarned = true;
                    try
                    {
                        Plugin.Instance?.Log.LogWarning("[SR-ID] LookupName(" + eventId + ") failed: " +
                                                        e.GetType().Name + ": " + e.Message);
                    }
                    catch
                    {
                    }
                }
            }

            string line = "[SR-ID] id=" + eventId + " name=" + namePart;
            try
            {
                if (Plugin.Instance?.Log != null)
                    Plugin.Instance.Log.LogInfo(line);
                else
                    UnityEngine.Debug.Log(line);
            }
            catch
            {
            }
        }

        /// <summary>2D/global 音效：以监听者(主相机)位置播放替换音，保证始终可听。</summary>
        internal static Vector3 ListenerPosition()
        {
            try
            {
                Camera cam = Camera.main;
                if (cam != null)
                    return cam.transform.position;
            }
            catch
            {
            }
            return Vector3.zero;
        }

        internal static uint NameToId(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                return 0u;
            try
            {
                return AkSoundEngine.GetIDFromString(eventName);
            }
            catch
            {
                return 0u;
            }
        }
    }

    [HarmonyPatch(typeof(CellSoundPlayer), "Post", new Type[] { typeof(uint), typeof(Vector3) })]
    internal static class AudioHook_Post_UInt_Vector3
    {
        private static bool Prefix(uint eventID, Vector3 pos)
            => AudioHook.PrefixCore(eventID, pos);
    }

    [HarmonyPatch(typeof(CellSoundPlayer), "Post", new Type[] { typeof(uint), typeof(bool) })]
    internal static class AudioHook_Post_UInt_Bool
    {
        private static bool Prefix(uint eventID, bool isGlobal)
            => AudioHook.PrefixCore(eventID, AudioHook.ListenerPosition());
    }

    [HarmonyPatch(typeof(CellSoundPlayer), "Post", new Type[] { typeof(string), typeof(Vector3) })]
    internal static class AudioHook_Post_String_Vector3
    {
        private static bool Prefix(string eventName, Vector3 pos)
            => AudioHook.PrefixCore(AudioHook.NameToId(eventName), pos);
    }

    [HarmonyPatch(typeof(CellSoundPlayer), "Post", new Type[] { typeof(string), typeof(bool) })]
    internal static class AudioHook_Post_String_Bool
    {
        private static bool Prefix(string eventName, bool isGlobal)
            => AudioHook.PrefixCore(AudioHook.NameToId(eventName), AudioHook.ListenerPosition());
    }
}
