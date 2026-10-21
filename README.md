# GTFOSoundReplacer

GTFO 的游戏内音效替换插件。按 **F8** 查找游戏音效，为它分配自己的 Wwise SoundBank 事件，试听、调整音量，并保存为可分享的预设。

**当前版本：1.0.3** · [下载 DLL](https://github.com/TouchedOVO/GTFOsoundreplacer-public/releases/download/v1.0.3/GTFOSoundReplacer.dll) · [版本记录](CHANGELOG.md)

## 可以做什么

- 按名称、数字 ID 或分类查找音效，支持中文搜索。
- 勾选“显示翻译”查看中文名称；默认不勾选。
- 为游戏事件分配自定义音库事件，分别试听原音和替换音，调整启用状态与音量。
- 保存、加载、导入和导出预设；正常退出后自动保存，下次启动恢复。

## 安装与开始使用

1. 安装适用于 GTFO 的 **BepInEx 6 IL2CPP**。
2. 下载上方 DLL，放入 `GTFO/BepInEx/plugins/GTFOSoundReplacer/`。升级时退出游戏，替换旧 DLL，并确保没有重复安装。
3. 将自己的 `.bnk` 音库放入 `GTFO/BepInEx/Assets/SoundBank/`。
4. 启动游戏，按 F8 搜索目标音效，点击该行“选择音频”，再点“加载音库目录”。
5. 填入自定义事件的数字 ID，分配后启用该行。用“▶”试听替换音，用“原”试听游戏原音。

只需下载一个插件 DLL。目录缓存和翻译文件会在运行时创建；自定义音库仍需自行准备。MP3、WAV 等音频需先用 Wwise 制作成 `.bnk`，具体见 [SoundBank 使用指南](docs/SoundBank使用指南.md)。

## 中文名称与网络

插件会在后台下载中文名称，并保存到本地。下载失败时继续使用已有翻译；没有翻译时仍可按数字 ID 查找音效。部分英文原名取决于本地可用的名称资料。

默认下载地址写在 DLL 中作为初始配置，**可以更换**：退出游戏，在 `BepInEx/config/dev.gtfo.GTFOSoundReplacer.cfg` 的 `[Catalog]` 下修改 `TranslationUrl`。留空会关闭下载，只使用本地文件。升级不会覆盖已有配置值；从测试版升级、原配置留空的用户需要自行填写地址。

```ini
[Catalog]
TranslationUrl = https://raw.githubusercontent.com/TouchedOVO/GTFOsoundreplacer-public/1.0.3/catalog/zh-CN.json
```

GitHub 访问不稳定时，可手动将 `zh-CN.json` 放入 `BepInEx/plugins/GTFOSoundReplacer/catalog/`，然后重启游戏。翻译仍为粗翻，欢迎核对和改进，见 [翻译说明](catalog/README.md)。

## 预设与使用提示

导入时，在预设名称框粘贴 JSON 文件的完整路径，点击“导入”；成功后会自动填入预设名，再点击“加载”应用。操作结果会短暂显示在界面右下角。预设只记录替换设置，分享时还需另附有权分发的音库。

部分音乐、对白可能无法被当前播放入口覆盖。音量和空间效果也可能与游戏原声不同；多个替换音与试听共享发声对象，音量可能互相影响。F8 界面仍可能出现双指针，界面调整留待后续版本。

完整操作、配置和排查见 [用户文档](docs/用户文档.md)。源码构建与检查见 [构建说明](docs/BUILD_VERIFY.md)。
