# Thunderstore 打包

构建主工程后，将 `GTFOSoundReplacer/bin/Release/GTFOSoundReplacer.dll` 放入以下结构，连同清单、说明和图标压缩为 ZIP：

```text
manifest.json
icon.png
README.md
BepInEx/plugins/GTFOSoundReplacer/GTFOSoundReplacer.dll
```

`README.md` 可使用仓库根目录的说明。`icon.png` 需要准备为 256 × 256 PNG，目前此目录没有提供图标。预设目录、SoundBank 目录和配置文件由插件运行时创建。

发布前确认 manifest 的版本与 DLL 一致，并在目标环境完成构建、回归检查和游戏测试。当前清单版本为 1.0.5。角色语音替换、普通替换启用/禁用和音量、100%复位及语音音量限制已完成实机验收；本目录是打包说明，GitHub Release 发布不代表已上传 Thunderstore。清单保留已知依赖 `BepInEx-BepInExPack_GTFO-3.2.1`；调整依赖前需验证对应运行环境。

发布包只包含插件自身文件。游戏程序集、BepInEx 依赖和本机配置无需打入包。若另附自定义音库或预设，确认有权分发并写明安装位置。
