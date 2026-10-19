using System;
using System.Collections.Generic;
using System.IO;
using GTFOSoundReplacer.Audio;
using GTFOSoundReplacer.UI;

namespace GTFOSoundReplacer.Presets
{
    /// <summary>
    /// 预设管理实现：落地 <see cref="IPresetManager"/>，用 System.Text.Json 持久化到
    /// presets\*.json，并在退出和启动时保存、恢复当前映射。
    /// </summary>
    public class PresetManager : IPresetManager
    {
        public static PresetManager Instance { get; private set; }

        private const string AutosaveName = "_autosave";

        private readonly ReplacementMap _map;

        public PresetManager(ReplacementMap map)
        {
            _map = map;
        }

        public static PresetManager Initialize(ReplacementMap map)
        {
            Instance = new PresetManager(map);
            return Instance;
        }

        // ---------------- IPresetManager ----------------

        public IReadOnlyList<string> ListPresetNames()
        {
            List<string> names = new List<string>();
            string dir = DirectoryManager.PresetsDirectory;
            if (Directory.Exists(dir))
            {
                foreach (string file in Directory.GetFiles(dir, "*.json"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name != AutosaveName)
                        names.Add(name);
                }
            }
            return names;
        }

        public void Save(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            PresetData data = BuildPreset(name);
            WriteFile(SanitizeName(name), data);
        }

        public bool Load(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            PresetData data = ReadFile(SanitizeName(name));
            if (data == null) return false;
            ApplyPreset(data);
            return true;
        }

        public bool Delete(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string path = GetPath(SanitizeName(name));
            if (!File.Exists(path)) return false;
            try
            {
                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string Export(string name)
        {
            // 生成规范的导出副本，返回绝对路径，保留原预设文件。
            if (string.IsNullOrWhiteSpace(name)) return null;
            string safeName = SanitizeName(name);
            PresetData data = ReadFile(safeName);
            if (data == null) return null;
            try
            {
                string json = PresetCodec.Serialize(data);
                string exportDirectory = Path.Combine(DirectoryManager.PresetsDirectory, "exports");
                Directory.CreateDirectory(exportDirectory);
                string path = Path.Combine(exportDirectory, safeName + ".json");
                File.WriteAllText(path, json);
                return path;
            }
            catch { return null; }
        }

        public bool Import(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;
            try
            {
                string json = File.ReadAllText(filePath);
                PresetData data = PresetCodec.Deserialize(json);
                if (data == null) return false;

                string name = string.IsNullOrWhiteSpace(data.Name)
                    ? Path.GetFileNameWithoutExtension(filePath)
                    : data.Name;

                WriteFile(SanitizeName(name), data);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ---------------- 自动保存 / 恢复 ----------------

        /// <summary>把当前映射表保存为自动保存文件（退出时调用）。</summary>
        public void SaveAutosave()
        {
            try
            {
                WriteFile(AutosaveName, BuildPreset(AutosaveName));
            }
            catch (Exception e)
            {
                if (Plugin.Instance != null)
                    Plugin.Instance.Log.LogWarning("Autosave failed: " + e.Message);
            }
        }

        /// <summary>启动时恢复：优先加载配置的默认预设，其次自动保存文件。</summary>
        public void RestoreState()
        {
            string defaultPreset = PluginConfig.DefaultPreset != null ? PluginConfig.DefaultPreset.Value : "";
            if (!string.IsNullOrWhiteSpace(defaultPreset) && Load(defaultPreset))
                return;

            PresetData data = ReadFile(AutosaveName);
            if (data != null)
                ApplyPreset(data);
        }

        // ---------------- 内部 ----------------

        private PresetData BuildPreset(string name)
        {
            var data = new PresetData { Name = name };
            foreach (KeyValuePair<uint, ReplacementEntry> kv in _map.Entries)
            {
                if (kv.Value != null)
                    data.Entries.Add(kv.Value);
            }
            return data;
        }

        private void ApplyPreset(PresetData data)
        {
            if (data == null || data.Entries == null) return;
            _map.Clear();

            foreach (ReplacementEntry entry in data.Entries)
            {
                if (entry != null && entry.EventId != 0)
                    _map.Set(entry.EventId, entry);
            }
        }

        private void WriteFile(string name, PresetData data)
        {
            DirectoryManager.EnsureRuntimeDirectories();
            string json = PresetCodec.Serialize(data);
            File.WriteAllText(GetPath(name), json);
        }

        private PresetData ReadFile(string name)
        {
            string path = GetPath(name);
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                return PresetCodec.Deserialize(json);
            }
            catch (Exception e)
            {
                if (Plugin.Instance != null)
                    Plugin.Instance.Log.LogWarning("Failed to read preset '" + name + "': " + e.Message);
                return null;
            }
        }

        private static string GetPath(string name)
        {
            return Path.Combine(DirectoryManager.PresetsDirectory, name + ".json");
        }

        private static string SanitizeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "_");
            return name;
        }
    }
}
