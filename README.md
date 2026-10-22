# GTFOSoundReplacer

GTFO 的游戏内音效替换插件。按 **F8** 查找游戏音效，换成自己的 Wwise SoundBank 声音，试听、调整音量，并保存为可分享的预设。

**当前版本：1.0.4** · [下载 DLL](https://github.com/TouchedOVO/GTFOsoundreplacer-public/releases/download/v1.0.4/GTFOSoundReplacer.dll) · [更新记录](CHANGELOG.md)

## 1.0.4 更新

- 修复部分角色语音无法试听的问题。
- 新增“停止试听”，循环声音可以随时停止；关闭 F8 界面也会停止试听。
- 播放中点击其他音效，会停止上一段并切换到新声音；连续点击时以最新选择为准。
- 试听使用独立通道，避免与实际替换音共用音量设置。

## 安装与开始使用

1. 安装适用于 GTFO 的 **BepInEx 6 IL2CPP**。
2. 下载上方 DLL，放入 `GTFO/BepInEx/plugins/GTFOSoundReplacer/`。升级前退出游戏，替换旧 DLL，确保只安装一份。
3. 将自己的 `.bnk` 音库放入 `GTFO/BepInEx/Assets/SoundBank/`。
4. 启动游戏，按 F8 搜索目标音效，点击“选择音频”→“加载音库目录”。
5. 填入自定义事件的数字 ID，分配后勾选“启用”。点击“▶”试听替换音，点击“原”试听游戏原音。

插件只需下载一个 DLL。自定义音库需自行准备；MP3、WAV 等音频需要先用 Wwise 制作成 `.bnk`，见 [SoundBank 使用指南](docs/SoundBank使用指南.md)。升级会保留已有预设和配置。

## 查找音效与中文名称

支持按名称、数字 ID 和分类查找，也支持中文搜索。勾选“显示翻译”可查看中文名称，默认不勾选。

中文名称会在后台下载并保存到本地。网络不通时继续使用已有翻译；没有翻译仍可按数字 ID 查找。GitHub 访问不稳定时，可[手动下载 zh-CN.json](https://github.com/TouchedOVO/GTFOsoundreplacer-public/blob/1.0.4/catalog/zh-CN.json)，放入 `BepInEx/plugins/GTFOSoundReplacer/catalog/` 后重启游戏。翻译仍为粗翻，欢迎核对，见 [翻译说明](catalog/README.md)。

下载地址可以更换或关闭，具体见 [配置与排查](docs/用户文档.md#配置与排查)。

## 预设与试听提示

预设支持保存、加载、导入和导出；正常退出时自动保存当前设置，下次启动恢复。强制结束可能丢失尚未保存的修改，重要设置请先手动保存。

导入时，在预设名称框粘贴 JSON 文件的完整路径，点击“导入”，成功后再点击“加载”。分享预设时，还需附上有权分发的音库。

角色语音的原音试听默认使用角色 01 的探索状态，实际游戏中的声音可能随角色和状态变化。部分音乐、对白可能无法替换；自定义声音的音量、定位和混响也可能与原声不同。界面双指针问题留待后续版本调整。

完整操作见 [用户文档](docs/用户文档.md)。

TouchedOVO
