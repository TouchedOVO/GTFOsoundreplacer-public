using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using GTFOSoundReplacer.Audio;
using GTFOSoundReplacer.Presets;
using GTFOSoundReplacer.UI;
using GTFOSoundReplacer.Catalog;
using System.IO;

namespace GTFOSoundReplacer
{
    /// <summary>
    /// GTFOSoundReplacer 插件入口。
    /// BepInEx 6 (IL2CPP) 会在游戏启动时实例化本类并调用 <see cref="Load"/>。
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInProcess("GTFO.exe")]
    public class Plugin : BasePlugin
    {
        public const string GUID = "dev.gtfo.GTFOSoundReplacer";
        public const string NAME = "GTFOSoundReplacer";
        public const string VERSION = "1.0.3";

        /// <summary>当前插件实例（供其他模块访问 Log / Config）。</summary>
        public static Plugin Instance { get; private set; }

        public override void Load()
        {
            Instance = this;

            Log.LogInfo("Loading " + NAME + " " + VERSION + " ...");
            Log.LogInfo("Audio backend: Wwise SoundBank; listener routing maintenance enabled.");

            // 1. 绑定配置项（生成 BepInEx\config\GTFOSoundReplacer.cfg）
            PluginConfig.BindAll(Config);

            // 2. 确保运行时目录存在（presets/、BepInEx\Assets\SoundBank\）
            DirectoryManager.EnsureRuntimeDirectories();

            CatalogService.Start(
                Path.Combine(Paths.GameRootPath, "GTFO_Data", "StreamingAssets", "GeneratedSoundBanks", "Windows"),
                Path.Combine(DirectoryManager.PluginDirectory, "catalog"),
                PluginConfig.TranslationUrl.Value,
                message => Log.LogInfo(message), message => Log.LogWarning(message));

            // 初始化替换映射和SoundBank播放器。
            SoundReplacer.Initialize();

            // 3.5 初始化预设管理（落地 IPresetManager，替代 NullPresetManager）并恢复状态
            PresetManager.Initialize(SoundReplacer.Instance.Map);
            PresetManager.Instance.RestoreState();

            // 4. 应用 Harmony 钩子（CellSoundPlayer.Post Prefix）
            var harmony = new Harmony(GUID);
            harmony.PatchAll();

            // 记录实际安装的补丁数量。
            int patchedCount = 0;
            foreach (System.Reflection.MethodBase m in harmony.GetPatchedMethods())
                patchedCount++;
            Log.LogInfo("Harmony patched methods: " + patchedCount);

            // 5. 挂载热键监听组件（负责呼出/隐藏 GUI）
            AddComponent<HotkeyListener>();

            // 6. 挂载 GUI 列表界面
            AddComponent<SoundListGUI>();

            Log.LogInfo(NAME + " loaded. Hotkey = " + PluginConfig.Hotkey.Value +
                        " + " + PluginConfig.HotkeyModifier.Value + ".");
        }
    }
}
