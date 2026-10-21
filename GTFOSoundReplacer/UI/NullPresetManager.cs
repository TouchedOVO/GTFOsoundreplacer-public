using System.Collections.Generic;

namespace GTFOSoundReplacer.UI
{
    /// <summary>预设管理的空实现（占位），t5 落地后替换。所有操作仅记录日志。</summary>
    public class NullPresetManager : IPresetManager
    {
        public string LastImportedName => "";
        public IReadOnlyList<string> ListPresetNames()
        {
            return new List<string>();
        }

        public void Save(string name)
        {
            Log("预设保存尚未实现（t5 落地）: " + name);
        }

        public bool Load(string name)
        {
            Log("预设加载尚未实现（t5 落地）: " + name);
            return false;
        }

        public bool Delete(string name)
        {
            Log("预设删除尚未实现（t5 落地）: " + name);
            return false;
        }

        public string Export(string name)
        {
            Log("预设导出尚未实现（t5 落地）: " + name);
            return null;
        }

        public bool Import(string filePath)
        {
            Log("预设导入尚未实现（t5 落地）: " + filePath);
            return false;
        }

        private static void Log(string message)
        {
            if (Plugin.Instance != null)
                Plugin.Instance.Log.LogInfo(message);
        }
    }
}
