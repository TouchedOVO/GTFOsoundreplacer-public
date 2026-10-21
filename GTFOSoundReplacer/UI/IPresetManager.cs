using System.Collections.Generic;

namespace GTFOSoundReplacer.UI
{
    /// <summary>
    /// 预设管理抽象（t5 实现）。GUI 只依赖此接口，实现与持久化细节解耦。
    /// </summary>
    public interface IPresetManager
    {
        IReadOnlyList<string> ListPresetNames();

        /// <summary>Actual saved name of the latest successful import; empty after a failed attempt.</summary>
        string LastImportedName { get; }

        /// <summary>把当前映射表保存为指定名称的预设。</summary>
        void Save(string name);

        /// <summary>加载指定名称的预设到当前映射表。</summary>
        bool Load(string name);

        bool Delete(string name);

        /// <summary>导出预设文件，返回导出路径。</summary>
        string Export(string name);

        /// <summary>从文件导入预设（返回是否成功）。</summary>
        bool Import(string filePath);
    }
}
