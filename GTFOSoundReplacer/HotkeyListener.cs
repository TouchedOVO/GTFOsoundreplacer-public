using UnityEngine;
using GTFOSoundReplacer.Presets;

namespace GTFOSoundReplacer
{
    /// <summary>
    /// 常驻 MonoBehaviour：每帧读取最新热键配置，检测按下后切换 GUI 显隐。
    /// 热键配置从 <see cref="PluginConfig"/> 实时读取，改 config 后无需重启即可生效。
    /// </summary>
    public class HotkeyListener : MonoBehaviour
    {
        public static HotkeyListener Instance { get; private set; }

        /// <summary>GUI 是否可见（供 t4 的 GUI 模块读取）。</summary>
        public bool GuiVisible { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            // 每帧解析最新配置，实现热键热更新。
            HotkeyBinding binding = HotkeyBinding.Parse(
                PluginConfig.Hotkey.Value,
                PluginConfig.HotkeyModifier.Value);

            if (binding.IsPressedThisFrame())
            {
                ToggleGui();
            }

            // GUI 可见时每帧强制解锁光标（GTFO 会在游戏过程中锁定光标，需持续覆盖）。
            if (GuiVisible)
                EnsureCursorUnlocked();
        }

        /// <summary>切换 GUI 显隐状态。</summary>
        public void ToggleGui()
        {
            SetGuiVisible(!GuiVisible);

            if (Plugin.Instance != null)
            {
                Plugin.Instance.Log.LogInfo(GuiVisible
                    ? "GTFOSoundReplacer GUI shown"
                    : "GTFOSoundReplacer GUI hidden");
            }
        }

        public void SetGuiVisible(bool visible)
        {
            if (!visible)
                Audio.SoundReplacer.Instance?.StopPreview();
            GuiVisible = visible;
        }

        /// <summary>强制解锁并显示光标（GTFO 会锁定光标；GUI 交互需在 OnGUI 前持续覆盖）。</summary>
        public static void EnsureCursorUnlocked()
        {
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;
        }

        private void OnApplicationQuit()
        {
            Audio.ListenerRouting.Shutdown();
            // 退出时自动保存当前映射。
            try
            {
                PresetManager.Instance?.SaveAutosave();
            }
            catch
            {
                // 忽略退出保存失败。
            }
        }
    }
}
