using System.IO;
using BepInEx;

namespace GTFOSoundReplacer
{
    /// <summary>
    /// 创建预设目录与SoundBank音库目录。
    /// </summary>
    public static class DirectoryManager
    {
        public static string PluginDirectory { get; private set; }
        public static string PresetsDirectory { get; private set; }
        public static string SoundBankDirectory { get; private set; }

        public static void EnsureRuntimeDirectories()
        {
            PluginDirectory = Path.Combine(Paths.PluginPath, "GTFOSoundReplacer");
            PresetsDirectory = Path.Combine(PluginDirectory, "presets");
            SoundBankDirectory = Path.Combine(Paths.BepInExRootPath, "Assets", "SoundBank");

            Directory.CreateDirectory(PresetsDirectory);
            Directory.CreateDirectory(SoundBankDirectory);

            if (Plugin.Instance != null)
            {
                Plugin.Instance.Log.LogInfo("Directories ready: " + PresetsDirectory);
                Plugin.Instance.Log.LogInfo("Directories ready: " + SoundBankDirectory);
            }
        }
    }
}
