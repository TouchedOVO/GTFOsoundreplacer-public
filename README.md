# GTFOSoundReplacer

GTFO 的游戏内音效替换插件，使用 BepInEx 6 IL2CPP。按 F8 打开列表，为游戏音效分配 Wwise SoundBank 事件，试听、调整音量并保存预设。SoundBank 中制作的随机播放可以保留。

当前源码版本为 1.0.2，已通过构建和180项代码检查，尚未进行本版本的实机验收。更新内容见[版本记录](CHANGELOG.md)，检查方法见[构建说明](docs/BUILD_VERIFY.md)。

## 安装与使用

1. 为 GTFO 安装 BepInExPack GTFO。
2. 将 `GTFOSoundReplacer.dll` 放入 `GTFO/BepInEx/plugins/GTFOSoundReplacer/`。
3. 将自定义 `.bnk` 放入 `GTFO/BepInEx/Assets/SoundBank/`。
4. 启动游戏，按 F8，找到目标音效并点击选择音频。
5. 点击加载音库目录，填入自定义事件的数字 ID，分配音库事件后启用该行。

列表支持分类、搜索、分页和启用筛选。行内 ▶ 试听替换事件，原 试听游戏事件。预设以 JSON 保存，正常退出时自动保存当前映射，下次启动恢复。

音频需要先通过 Wwise 制作成 `.bnk`。预设只保存映射，分享时需另附有权分发的 SoundBank。详细操作见[用户文档](docs/用户文档.md)和[SoundBank 使用指南](docs/SoundBank使用指南.md)。

## 使用范围

插件覆盖经过 `CellSoundPlayer.Post` 四个无回调重载的事件，列表中的部分音乐、对白可能采用其他播放入口。替换使用共享发声对象，多个声音的输出音量会相互影响；音量设为 1 的播放仍可能继承此前的对象增益。原音试听也使用这个对象。自定义事件的空间效果取决于素材和播放设置，不能保证与游戏原声一致。

## 构建

主工程为 `GTFOSoundReplacer/GTFOSoundReplacer.csproj`，需要 MSBuild、.NET 6 运行时，以及已安装 BepInEx 的 GTFO 目录。构建脚本会从 PATH 或 Visual Studio 安装信息中查找 MSBuild：

```powershell
.\build.ps1 -Configuration Release -GTFO '<GTFO目录>'
```

可通过 `-MSBuild '<MSBuild.exe路径>'` 指定构建工具，通过 `-Net6Framework '<.NET 6运行时目录>'` 指定运行时。产物为 `GTFOSoundReplacer/bin/Release/GTFOSoundReplacer.dll`。代码回归检查从仓库根目录运行：

```powershell
pwsh -NoProfile -File GTFOSoundReplacer/tests/run-all.ps1
```

发布包结构见 [Thunderstore 打包说明](thunderstore/PACKAGING.md)。
