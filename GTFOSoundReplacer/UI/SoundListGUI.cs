using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Il2CppInterop.Runtime.Attributes;
using GTFOSoundReplacer.Audio;
using GTFOSoundReplacer.Presets;
using GTFOSoundReplacer.Catalog;

namespace GTFOSoundReplacer.UI
{
    /// <summary>
    /// 游戏内 GUI 列表界面（IMGUI/OnGUI）。按 F8 呼出/隐藏（状态来自 HotkeyListener）。
    /// 行内提供启用、音量、替换音试听、原音试听及音库事件分配。
    /// 初始化时获取目录、替换映射和预设管理器。
    /// </summary>
    public class SoundListGUI : MonoBehaviour
    {
        // 注：依赖改为私有字段而非 public 属性，避免 Il2CppInterop 对 MonoBehaviour
        // 暴露自定义托管类型（ISoundCatalog/SoundReplacer/IPresetManager）产生 unsupported-type 警告。
        private ISoundCatalog Catalog;
        private SoundReplacer Replacer;
        private IPresetManager Presets;

        private Rect _windowRect = new Rect(40, 40, 820, 600);
        private Rect _pickerRect = new Rect(240, 140, 440, 480);
        private bool _windowPositioned;
        private Vector2 _scroll = Vector2.zero;
        private string _search = "";
        private bool _showTranslation = false;
        private int _previewStatusVersion;
        private string _temporaryStatus = "";
        private long _statusExpiresTick;
        private string _presetName = "";
        private string _bankEventIdText = "";
        /// <summary>分配音库事件的目标音效 ID，由行内「选择音频」按钮设置。</summary>
        private uint _pickerEventId;
        private bool _showBankPicker;
        private string _bankStatus = "";

        // 搜索词或启用筛选变化时重算列表缓存。
        private string _cachedSearch = null;
        private bool _showOnlyEnabled = false;
        private bool _cachedShowOnlyEnabled = false;
        private SoundCategory[] _categoryOrder;
        private readonly Dictionary<SoundCategory, List<SoundEntry>> _groups = new Dictionary<SoundCategory, List<SoundEntry>>();

        // 分页：过滤结果按分类顺序展平，每页 50 条。
        private const int PageSize = 50;
        private int _currentPage = 0;
        private readonly List<SoundEntry> _flatEntries = new List<SoundEntry>();
        private readonly List<SoundCategory> _flatCategories = new List<SoundCategory>();

        private void Awake()
        {
            if (Catalog == null)
                Catalog = SoundCatalog.LoadDefault();
            if (Replacer == null)
                Replacer = SoundReplacer.Instance;
            if (Presets == null)
                Presets = PresetManager.Instance ?? (IPresetManager)new NullPresetManager();

            _categoryOrder = (SoundCategory[])Enum.GetValues(typeof(SoundCategory));
        }

        private void OnGUI()
        {
            if (HotkeyListener.Instance == null || !HotkeyListener.Instance.GuiVisible)
                return;

            // 每帧在 GUI 事件处理前最后覆盖一次光标解锁，确保 IMGUI 可交互（GTFO 会锁定光标）。
            HotkeyListener.EnsureCursorUnlocked();

            var currentCatalog = CatalogService.Current;
            if (!ReferenceEquals(Catalog, currentCatalog))
            {
                Catalog = currentCatalog;
                _cachedSearch = null;
            }

            CenterWindowOnce();
            if (_previewStatusVersion != PreviewService.StatusVersion)
            {
                _previewStatusVersion = PreviewService.StatusVersion;
                _temporaryStatus = PreviewService.Status;
                _statusExpiresTick = System.Diagnostics.Stopwatch.GetTimestamp()
                    + 5 * System.Diagnostics.Stopwatch.Frequency;
            }

            GUILayout.BeginArea(_windowRect);
            DrawHeader();
            bool showTranslation = GUILayout.Toggle(_showTranslation, "显示翻译");
            if (showTranslation != _showTranslation)
            {
                _showTranslation = showTranslation;
                _temporaryStatus = showTranslation ? "翻译已开启" : "翻译已关闭";
                _statusExpiresTick = System.Diagnostics.Stopwatch.GetTimestamp()
                    + 3 * System.Diagnostics.Stopwatch.Frequency;
            }
            DrawToolbar();
            DrawSearchBar();
            DrawSoundList();
            // Scroll view takes the remaining height; this final row stays at the window's bottom right.
            GUILayout.BeginHorizontal();
            // FlexibleSpace is a throwing unstripping stub in GTFO; use the existing native Label/ExpandWidth path.
            GUILayout.Label("", GUILayout.ExpandWidth(true));
            string status = System.Diagnostics.Stopwatch.GetTimestamp() < _statusExpiresTick
                ? _temporaryStatus : CatalogService.Status;
            GUILayout.Label(status, GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (_showBankPicker)
                DrawBankPicker();
        }

        [HideFromIl2Cpp]
        private void CenterWindowOnce()
        {
            if (_windowPositioned) return;
            _windowPositioned = true;

            // 首次可见时把窗口移到屏幕中央（Screen 未就绪时保持默认 40,40）。
            if (Screen.width <= 0 || Screen.height <= 0) return;
            float x = (Screen.width - _windowRect.width) / 2f;
            float y = (Screen.height - _windowRect.height) / 2f;
            _windowRect.x = x < 0 ? 0 : x;
            _windowRect.y = y < 0 ? 0 : y;
        }

        [HideFromIl2Cpp]
        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("SoundBank 音效替换", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("停止试听", GUILayout.Width(80)))
                Replacer?.StopPreview();
            if (GUILayout.Button("X", GUILayout.Width(28)))
                HotkeyListener.Instance.SetGuiVisible(false);
            GUILayout.EndHorizontal();
        }

        // ---------------- 工具条 ----------------

        [HideFromIl2Cpp]
        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label("全局音量", GUILayout.Width(64));
            float global = PluginConfig.GlobalVolume != null ? PluginConfig.GlobalVolume.Value : 1f;
            float newGlobal = GUILayout.HorizontalSlider(global, 0f, 2f, GUILayout.Width(120));
            if (Math.Abs(newGlobal - global) > 0.0001f && PluginConfig.GlobalVolume != null)
                PluginConfig.GlobalVolume.Value = newGlobal;

            if (GUILayout.Button("全部启用", GUILayout.Width(80)))
                SetAllEnabled(true);
            if (GUILayout.Button("全部禁用", GUILayout.Width(80)))
                SetAllEnabled(false);

            GUILayout.EndHorizontal();

            DrawPresetBar();
        }

        [HideFromIl2Cpp]
        private void DrawPresetBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("预设", GUILayout.Width(40));
            _presetName = GUILayout.TextField(_presetName, GUILayout.Width(140));
            if (GUILayout.Button("保存", GUILayout.Width(48))) RunPresetAction("保存");
            if (GUILayout.Button("加载", GUILayout.Width(48))) RunPresetAction("加载");
            if (GUILayout.Button("删除", GUILayout.Width(48))) RunPresetAction("删除");
            if (GUILayout.Button("导出", GUILayout.Width(48))) RunPresetAction("导出");
            if (GUILayout.Button("导入", GUILayout.Width(48))) RunPresetAction("导入");
            GUILayout.EndHorizontal();
        }

        [HideFromIl2Cpp]
        private void ReportPresetStatus(string message, bool failed = false)
        {
            _temporaryStatus = message;
            _statusExpiresTick = System.Diagnostics.Stopwatch.GetTimestamp()
                + 5 * System.Diagnostics.Stopwatch.Frequency;
            if (Plugin.Instance == null) return;
            if (failed) Plugin.Instance.Log.LogWarning("[Preset] " + message);
            else Plugin.Instance.Log.LogInfo("[Preset] " + message);
        }

        [HideFromIl2Cpp]
        private void RunPresetAction(string action)
        {
            string input = _presetName.Trim();
            if (Presets == null || Presets is NullPresetManager)
            {
                ReportPresetStatus("预设管理尚未就绪", true);
                return;
            }
            if (input.Length == 0)
            {
                ReportPresetStatus(action == "导入" ? "请先填写要导入文件的完整路径" : "请先填写预设名称", true);
                return;
            }
            try
            {
                switch (action)
                {
                    case "保存":
                        Presets.Save(input);
                        ReportPresetStatus("已保存预设：" + input);
                        break;
                    case "加载":
                        if (Presets.Load(input))
                        {
                            _cachedSearch = null;
                            ReportPresetStatus("已加载预设：" + input);
                        }
                        else ReportPresetStatus("加载失败：未找到或无法读取预设，请检查名称", true);
                        break;
                    case "删除":
                        bool deleted = Presets.Delete(input);
                        ReportPresetStatus(deleted ? "已删除预设：" + input : "删除失败：未找到预设或无法删除", !deleted);
                        break;
                    case "导出":
                        string path = Presets.Export(input);
                        if (string.IsNullOrEmpty(path)) ReportPresetStatus("导出失败：请检查预设名称和文件", true);
                        else
                        {
                            ReportPresetStatus("已导出到 presets/exports：" + input);
                            Plugin.Instance?.Log.LogInfo("Preset exported: " + path);
                        }
                        break;
                    case "导入":
                        string file = input.Trim('"');
                        if (!File.Exists(file)) ReportPresetStatus("导入失败：找不到文件，请填写完整路径", true);
                        else if (Presets.Import(file))
                        {
                            _presetName = Presets.LastImportedName;
                            ReportPresetStatus("已导入：" + _presetName + "，点击加载应用");
                        }
                        else ReportPresetStatus("导入失败：文件格式无效或无法写入，请查看日志", true);
                        break;
                }
            }
            catch (Exception e)
            {
                Plugin.Instance?.Log.LogWarning("[Preset] " + action + " failed: " + e);
                ReportPresetStatus(action + "失败，请查看日志", true);
            }
        }

        [HideFromIl2Cpp]
        private void SetAllEnabled(bool value)
        {
            if (Replacer == null || Replacer.Map == null) return;
            foreach (KeyValuePair<uint, ReplacementEntry> kv in Replacer.Map.Entries)
            {
                if (kv.Value != null) kv.Value.Enabled = value;
            }
        }

        /// <summary>该 eventId 是否有「已启用」的替换项（供「只看已启用」过滤）。</summary>
        [HideFromIl2Cpp]
        private bool IsEnabledReplacement(uint eventId)
        {
            ReplacementEntry entry = GetEntry(eventId);
            return entry != null && entry.Enabled;
        }

        // ---------------- 搜索 ----------------

        [HideFromIl2Cpp]
        private void DrawSearchBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("搜索", GUILayout.Width(40));
            _search = GUILayout.TextField(_search, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("清除", GUILayout.Width(48)))
                _search = "";
            bool newFilter = GUILayout.Toggle(_showOnlyEnabled, "只看已启用");
            if (newFilter != _showOnlyEnabled)
                _showOnlyEnabled = newFilter;
            GUILayout.EndHorizontal();
        }

        // ---------------- 列表 ----------------

        [HideFromIl2Cpp]
        private void DrawSoundList()
        {
            RefreshListCache();

            // 展平结果分页：每页 50 条，边界夹取保证页码合法。
            int total = _flatEntries.Count;
            int pageCount = total == 0 ? 1 : (total + PageSize - 1) / PageSize;
            if (_currentPage < 0) _currentPage = 0;
            if (_currentPage >= pageCount) _currentPage = pageCount - 1;

            int start = _currentPage * PageSize;
            int end = start + PageSize;
            if (end > total) end = total;

            _scroll = GUILayout.BeginScrollView(_scroll);

            if (total == 0)
            {
                GUILayout.Label("（无匹配结果）");
            }
            else
            {
                // 仅绘制当前页的行；分类标题在页面内出现新分组时插入，保留原分组展示。
                SoundCategory lastCat = default;
                bool first = true;
                for (int i = start; i < end; i++)
                {
                    SoundCategory cat = _flatCategories[i];
                    if (first || cat != lastCat)
                    {
                        GUILayout.Space(4);
                        GUILayout.Label(CategoryName(cat) + " (" + GroupCount(cat) + ")");
                        lastCat = cat;
                        first = false;
                    }
                    DrawRow(_flatEntries[i]);
                }
            }

            GUILayout.EndScrollView();

            DrawPager(pageCount, total);
        }

        [HideFromIl2Cpp]
        private void RefreshListCache()
        {
            // 首次绘制或筛选条件变化时重算过滤与分组。
            if (_cachedSearch == _search && _cachedShowOnlyEnabled == _showOnlyEnabled) return;
            _cachedSearch = _search;
            _cachedShowOnlyEnabled = _showOnlyEnabled;

            _groups.Clear();
            _flatEntries.Clear();
            _flatCategories.Clear();
            if (Catalog == null) return;

            foreach (SoundEntry e in Catalog.Search(_search))
            {
                if (_showOnlyEnabled && !IsEnabledReplacement(e.EventId))
                    continue;

                if (!_groups.TryGetValue(e.Category, out List<SoundEntry> list))
                {
                    list = new List<SoundEntry>();
                    _groups[e.Category] = list;
                }
                list.Add(e);
            }

            // 按稳定顺序展平（分类顺序 + 组内加入顺序），供分页使用。
            foreach (SoundCategory cat in _categoryOrder)
            {
                if (!_groups.TryGetValue(cat, out List<SoundEntry> group) || group.Count == 0)
                    continue;
                foreach (SoundEntry se in group)
                {
                    _flatEntries.Add(se);
                    _flatCategories.Add(cat);
                }
            }

            // 搜索词/过滤结果变化时回到第 1 页。
            _currentPage = 0;
        }

        [HideFromIl2Cpp]
        private void DrawPager(int pageCount, int totalCount)
        {
            bool canPrev = _currentPage > 0;
            bool canNext = _currentPage < pageCount - 1;

            GUILayout.BeginHorizontal();

            // 第一页无上一页、最后一页无下一页：用 GUI.enabled 置灰并禁用点击。
            GUI.enabled = canPrev;
            if (GUILayout.Button("上一页", GUILayout.Width(80)))
                _currentPage--;
            GUI.enabled = true;

            GUILayout.Label("第 " + (_currentPage + 1) + "/" + pageCount + " 页 · 共 " + totalCount + " 条结果", GUILayout.ExpandWidth(true));

            GUI.enabled = canNext;
            if (GUILayout.Button("下一页", GUILayout.Width(80)))
                _currentPage++;
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }

        [HideFromIl2Cpp]
        private int GroupCount(SoundCategory category)
        {
            if (_groups.TryGetValue(category, out List<SoundEntry> group))
                return group.Count;
            return 0;
        }

        [HideFromIl2Cpp]
        private void DrawRow(SoundEntry se)
        {
            ReplacementEntry entry = GetEntry(se.EventId);

            GUILayout.BeginHorizontal();

            // 名称、ID 及已分配的音库事件
            string label = se.GetDisplayName(_showTranslation) + "  [" + se.EventId + "]";
            if (entry != null && entry.BankEventId != 0)
                label += "  → bank:" + entry.BankEventId;
            // 注：GUILayout.MinWidth 在 GTFO 的 IL2CPP 下会触发 ObjectCollectedException，
            // 改用已验证可用的 ExpandWidth（让标签填满剩余宽度、控件右对齐）。
            GUILayout.Label(label, GUILayout.ExpandWidth(true));

            // 启用 / 禁用
            bool enabled = entry != null && entry.Enabled;
            bool newEnabled = GUILayout.Toggle(enabled, "启用");
            if (newEnabled != enabled)
                GetOrCreateEntry(se.EventId).Enabled = newEnabled;

            // 音量滑块 0–200%
            float vol = entry != null ? entry.Volume : 1f;
            float newVol = GUILayout.HorizontalSlider(vol, 0f, 2f, GUILayout.Width(80));
            if (Math.Abs(newVol - vol) > 0.0001f)
                GetOrCreateEntry(se.EventId).Volume = newVol;

            // 试听（替换音 / 原音）
            if (GUILayout.Button("▶", GUILayout.Width(26)))
                PreviewReplacement(se, entry);
            if (GUILayout.Button("原", GUILayout.Width(26)))
                PreviewOriginal(se);

            // 选择音频，打开音库事件面板
            if (GUILayout.Button("选择音频", GUILayout.Width(74)))
                OpenBankPicker(se.EventId);

            // 未替换提示
            if (IsUnassigned(entry))
                GUILayout.Label("未替换", GUILayout.Width(52));

            GUILayout.EndHorizontal();
        }

        // ---------------- 音库事件分配 ----------------

        [HideFromIl2Cpp]
        private void DrawBankPicker()
        {
            GUILayout.BeginArea(_pickerRect);
            DrawBankPickerContent();
            GUILayout.EndArea();
        }

        [HideFromIl2Cpp]
        private void DrawBankPickerContent()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("分配音库事件（ID " + _pickerEventId + "）", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("X", GUILayout.Width(28)))
                _showBankPicker = false;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("Wwise SoundBank 音库");

            if (GUILayout.Button("加载音库目录"))
                LoadAllBanks();

            GUILayout.BeginHorizontal();
            GUILayout.Label("事件ID", GUILayout.Width(48));
            _bankEventIdText = GUILayout.TextField(_bankEventIdText, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("分配音库事件", GUILayout.Width(104)))
                AssignBankEvent();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("（.bnk 放 BepInEx/Assets/SoundBank/；事件 ID 见 bank 导出的 Wwise_IDs 表）");
            if (!string.IsNullOrEmpty(_bankStatus))
                GUILayout.Label(_bankStatus);

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("清除替换", GUILayout.Width(90)))
                ClearReplacement(_pickerEventId);
            if (GUILayout.Button("关闭", GUILayout.Width(60)))
                _showBankPicker = false;
            GUILayout.EndHorizontal();
        }

        [HideFromIl2Cpp]
        private void OpenBankPicker(uint eventId)
        {
            _pickerEventId = eventId;
            _showBankPicker = true;

            ReplacementEntry entry = GetEntry(eventId);
            _bankEventIdText = (entry != null && entry.BankEventId != 0)
                ? entry.BankEventId.ToString()
                : "";
        }

        [HideFromIl2Cpp]
        private void AssignBankEvent()
        {
            if (uint.TryParse(_bankEventIdText, out uint eventId) && eventId != 0)
            {
                ReplacementEntry entry = GetOrCreateEntry(_pickerEventId);
                entry.BankEventId = eventId;
                _showBankPicker = false;
            }
        }

        [HideFromIl2Cpp]
        private void ClearReplacement(uint eventId)
        {
            ReplacementEntry entry = GetEntry(eventId);
            if (entry != null)
                entry.BankEventId = 0;
            _bankStatus = "";
            _showBankPicker = false;
        }

        [HideFromIl2Cpp]
        private void LoadAllBanks()
        {
            if (Replacer == null)
            {
                _bankStatus = "错误：替换核心未初始化";
                return;
            }

            try
            {
                string dir = DirectoryManager.SoundBankDirectory;
                string[] banks = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.bnk") : new string[0];
                int loaded = 0;
                foreach (string bank in banks)
                {
                    if (Replacer.SoundBank.LoadBank(bank))
                        loaded++;
                }
                _bankStatus = "扫描完成：找到 " + banks.Length + " 个 .bnk，成功加载 " + loaded + " 个";
                if (Plugin.Instance != null)
                    Plugin.Instance.Log.LogInfo("[Bank] 目录 " + dir + "：找到 " + banks.Length + "，加载 " + loaded);
            }
            catch (Exception e)
            {
                _bankStatus = "加载失败：" + e.Message;
            }
        }

        // ---------------- 试听 ----------------

        [HideFromIl2Cpp]
        private void PreviewReplacement(SoundEntry se, ReplacementEntry entry)
        {
            if (entry == null || Replacer == null) return;
            if (entry.BankEventId == 0)
                return;

            float global = PluginConfig.GlobalVolume != null ? PluginConfig.GlobalVolume.Value : 1f;
            float volume = entry.EffectiveVolume(global);
            PreviewService.Request(entry.BankEventId, AudioHook.ListenerPosition(), volume, false, "替换音");
        }

        [HideFromIl2Cpp]
        private void PreviewOriginal(SoundEntry se)
        {
            if (Replacer == null) return;
            PreviewService.Request(se.EventId, AudioHook.ListenerPosition(), 1f,
                se.Category == SoundCategory.Voice, "原音");
        }

        // ---------------- 辅助 ----------------

        [HideFromIl2Cpp]
        private ReplacementEntry GetEntry(uint eventId)
        {
            if (Replacer == null || Replacer.Map == null) return null;
            Replacer.Map.TryGet(eventId, out ReplacementEntry entry);
            return entry;
        }

        [HideFromIl2Cpp]
        private ReplacementEntry GetOrCreateEntry(uint eventId)
        {
            ReplacementEntry entry = GetEntry(eventId);
            if (entry != null) return entry;

            entry = new ReplacementEntry
            {
                EventId = eventId,
                Enabled = true,
                Volume = 1f,
                MuteOriginal = PluginConfig.MuteOriginalDefault != null ? PluginConfig.MuteOriginalDefault.Value : true,
            };
            Replacer.Map.Set(eventId, entry);
            return entry;
        }

        private static bool IsUnassigned(ReplacementEntry entry)
        {
            return entry == null || entry.BankEventId == 0;
        }

        private static string CategoryName(SoundCategory category)
        {
            switch (category)
            {
                case SoundCategory.Weapon: return "武器";
                case SoundCategory.Enemy: return "怪物";
                case SoundCategory.Door: return "门与设施";
                case SoundCategory.UI: return "UI";
                case SoundCategory.Ambience: return "环境";
                case SoundCategory.Player: return "玩家";
                case SoundCategory.Voice: return "对白";
                case SoundCategory.Item: return "道具与工具";
                case SoundCategory.Music: return "音乐";
                default: return "其他";
            }
        }
    }
}
