# 中文事件标签

`zh-CN.json` 是独立的、以十进制事件 ID 为键的翻译表，不嵌入 DLL。当前是待人工审核的草稿，不是对白字幕。

编辑每条记录的 `zhCN`。`name` 保留原始事件名；`category` 取 Weapon、Enemy、Door、UI、Ambience、Player、Voice、Item、Music、Other。`reviewNote` 仅用于审核，运行时忽略。保留变体字母、数字和 2D/3D，避免不同声音层失去区别。语音录音编号无法反推内容，须听音后补充。

3121 个唯一 ID 对应 4408 次音库事件出现，同一 ID 只翻译一次。生成时验证 ID 集合、原名唯一性和名称 hash；插件启动不做逐名 hash。

顶层 `schemaVersion` 固定为 1、`locale` 为 zh-CN；修改内容后可更新 `revision`。中文标签允许有重复（相近语义），事件身份始终由 ID 确定。

审核阅读快照见 `docs/音频事件中文翻译审核表.md`。JSON 是后续修改的准本，快照不会自动回写。提交由用户手动处理；目前未上传到公开仓库。发布后将 raw JSON 的 HTTPS 地址填入插件 Catalog/TranslationUrl；未发布时可直接复制到插件 catalog/zh-CN.json 进行离线测试。

## 已确认的翻译格式

- 标签分隔符使用下划线 _。
- 关卡编号保持完整，如 R8E1、R8A2；Rundown 编号如 R8、R06 也不拆开。
- expedition 统一译为“远征”，例如 音乐_远征_成功_R8E1。
