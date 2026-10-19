# 构建与检查

插件工程为GTFOSoundReplacer/GTFOSoundReplacer.csproj，默认构建脚本为仓库根目录build.ps1。产物位于GTFOSoundReplacer/bin/Release/GTFOSoundReplacer.dll。

构建需要MSBuild、.NET 6运行时，以及游戏安装目录中的BepInEx core和interop程序集。依赖路径通过-GTFO、-MSBuild和-Net6Framework指定。GTFO_GAME_PATH、NET6_FRAMEWORK_PATH也可提供对应目录。脚本会从PATH或Visual Studio Installer查找MSBuild，并尝试查找已安装的.NET 6运行时。

工程使用传统csproj直接引用运行时程序集。项目中的v4.7.2用于MSBuild框架路径解析，插件运行于.NET 6；构建不会生成误导性的.NET Framework目标属性。

执行测试：

```powershell
pwsh -NoProfile -File GTFOSoundReplacer/tests/run-all.ps1
```

测试直接编译生产代码，覆盖音量、映射、音效目录、预设文件操作、路由状态和播放器提交边界。各组在独立进程运行，避免Add-Type重复定义。测试不启动GTFO，也不证明Harmony原生钩子或Wwise音频线程的实际行为。

1.0.2已通过构建和180项代码检查，尚未进行本版本的实机验收。测试覆盖与限制见[测试说明](../GTFOSoundReplacer/tests/README.md)。
