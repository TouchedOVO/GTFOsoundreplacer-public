using BepInEx.Configuration;

namespace GTFOSoundReplacer
{
    /// <summary>
    /// 主配置封装。所有配置项通过 <see cref="ConfigFile.Bind{T}(string, string, T, string)"/>
    /// 绑定，存于 BepInEx\config\GTFOSoundReplacer.cfg。
    /// </summary>
    public static class PluginConfig
    {
        public const string SectionGeneral = "General";

        public const string DefaultHotkey = "F8";
        public const string DefaultHotkeyModifier = "None";

        /// <summary>GUI 主键（Unity KeyCode 名称字符串，默认 F8）。</summary>
        public static ConfigEntry<string> Hotkey;

        /// <summary>修饰键：None / Ctrl / Shift / Alt（支持双键组合，如 Ctrl+F8）。</summary>
        public static ConfigEntry<string> HotkeyModifier;

        /// <summary>全局音量倍率（0.0 – 2.0，1.0 = 原始音量）。</summary>
        public static ConfigEntry<float> GlobalVolume;

        /// <summary>启动时自动加载的预设名（留空 = 不自动加载）。</summary>
        public static ConfigEntry<string> DefaultPreset;

        /// <summary>新建替换项的默认「静音原音」开关（true = 替换后不播放原音）。</summary>
        public static ConfigEntry<bool> MuteOriginalDefault;

        /// <summary>是否在日志中打印每个音效事件的 ID 与名称（用于定位音效文件，默认关闭）。</summary>
        public static ConfigEntry<bool> LogEventIds;

        public static void BindAll(ConfigFile config)
        {
            Hotkey = config.Bind<string>(
                SectionGeneral,
                "Hotkey",
                DefaultHotkey,
                "呼出 / 隐藏 GUI 的主键（Unity KeyCode 名称，例如 F8 / F9 / H）。");

            HotkeyModifier = config.Bind<string>(
                SectionGeneral,
                "HotkeyModifier",
                DefaultHotkeyModifier,
                "与主键组合的修饰键：None / Ctrl / Shift / Alt（例如 Ctrl+F8）。");

            GlobalVolume = config.Bind<float>(
                SectionGeneral,
                "GlobalVolume",
                1.0f,
                "全局音量倍率（0.0 – 2.0，1.0 = 原始音量）。");

            DefaultPreset = config.Bind<string>(
                SectionGeneral,
                "DefaultPreset",
                "",
                "启动时自动加载的预设名（留空 = 不自动加载）。");

            MuteOriginalDefault = config.Bind<bool>(
                SectionGeneral,
                "MuteOriginalDefault",
                true,
                "新建替换项的默认「静音原音」开关（true = 替换后不播放原音）。");

            LogEventIds = config.Bind<bool>(
                SectionGeneral,
                "LogEventIds",
                false,
                "是否在日志中打印每个音效事件的 ID 与名称（用于定位音效文件，默认关闭）。");
        }
    }
}
