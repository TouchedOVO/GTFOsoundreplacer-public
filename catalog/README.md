# GTFO 音效中文名称

这份翻译表为 GTFOSoundReplacer 的音效列表提供中文名称，方便查找武器、敌人、环境、音乐和对白相关的声音。配合支持外部翻译的插件版本，可以用中文或英文搜索音效。

**当前为粗翻草稿，仍在人工校对。** 部分名称按英文事件名翻译，可能与实际听感存在差异；只有录音编号的对白暂时保留编号，等待核听。

## 下载与使用

[下载中文翻译文件（zh-CN.json）](https://raw.githubusercontent.com/TouchedOVO/GTFOsoundreplacer-public/1.0.5/catalog/zh-CN.json)

当前版本 **1.0.5** 已支持自动下载和本地翻译。按 F8 勾选“显示翻译”可显示中文名称；中文搜索不受该开关影响。

### 自动下载

在插件配置文件的 `[Catalog]` 部分，将 `TranslationUrl` 设置为：

```ini
[Catalog]
TranslationUrl = https://raw.githubusercontent.com/TouchedOVO/GTFOsoundreplacer-public/1.0.5/catalog/zh-CN.json
```

插件会在启动时尝试获取最新翻译。成功下载后会保存在本地，后续即使网络不可用，也能继续使用已保存的翻译。

### 手动安装

如果自动下载失败，也可以通过浏览器下载文件，保留文件名 `zh-CN.json`，放入：

```text
GTFO/BepInEx/plugins/GTFOSoundReplacer/catalog/
```

目录不存在时可以自行创建。将 `TranslationUrl` 留空即可使用本地文件；更新翻译时，用新文件覆盖旧文件，然后重新启动游戏。

## 帮助改进翻译

发现译名不准确、不自然或难以理解时，欢迎[提交反馈](https://github.com/TouchedOVO/GTFOsoundreplacer-public/issues)或发起 Pull Request。请附上英文原名、建议中文，以及听到该声音时的场景，方便核对。

直接修改文件时，只需编辑对应条目的 `zhCN` 文本，保留条目编号和英文原名。标签之间使用下划线 `_`；关卡编号保持完整，例如 `R8E1`；`expedition` 统一译为“远征”。

示例：`music_expedition_successful_r8e1` → `音乐_远征_成功_R8E1`。
