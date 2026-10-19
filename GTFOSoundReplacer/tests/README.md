# 自动检查

运行：`pwsh -NoProfile -File GTFOSoundReplacer/tests/run-all.ps1`。

要求 PowerShell 7 及其自带 ref 编译引用程序集（含 System.Text.Json），不依赖游戏安装、NuGet、本机固定路径或插件 DLL。可传 -PowerShellPath 选择启动套件的 pwsh；-Suite Route/Player/Core 可单独运行。默认在三个独立进程编译实际生产源码，避免 Add-Type 已加载类型污染结果。

本次结果：路由 42 项、播放器 34 项、核心 54 项、预设管理器 50 项，共 180 项通过。

## 范围

- Route 编译真实 RouteBindingState：默认更新后的 Reset→缓存 gain 顺序、连续换代、默认与显式模式、失败重试、释放对象清旧状态。
- Player 编译真实 SoundBankPlayer/ListenerRouting/状态：默认入口即时维护、Post 共用锁、精确 hook 目标签名、异常解锁后另一线程可继续、RTPC/近单位音量原语义、共享试听、Shutdown 禁止后续原生写入。
- Core 编译真实 Entry/Map/Catalog/模型/Codec：映射与音量边界、目录唯一性与搜索、JSON 往返、新 JSON 无 sourceType；已有 SoundBank 字符串/数字 1/无标记可读；Unsupported 标记、重复字段及错误结构拒绝。
- Preset manager 编译真实管理器与模型/codec/map：临时文件 Save/Load/Import/Export；验证失败保留当前 map、原件和已有目标文件；规范导出副本保留源文件；自动保存、默认预设优先及既有恢复回退行为。

JSON 使用真实 System.Text.Json。Preset manager 仅替换游戏目录、配置和日志入口，使用真实文件系统与生产逻辑，没有重写 codec。文件写入仅在随机命名的系统临时目录，结束后验证路径边界并清理。

## 限制

Player 使用 Unity/Wwise/Harmony 外部替身，手工反射调用真实 hook；不证明真实 Harmony/IL2CPP detour 能拦截原生调用。锁断言只覆盖测试入口，不证明其他入口都经过该锁。

Route 的队列模型区分接受命令与消费命令，不是真实 Wwise。反例确认提交成功但处理阶段拒绝时，逻辑无法得知实际连接成功。实机仍须延迟 GetListeners、输出增益和听感验收。

音库加载/卸载、内存对齐、非播放动作、自然 Scout、连续跨局、死亡重进及其他声音仍需审查或实机验证。近单位音量提前返回的共享对象增益继承是保留的既有行为，并未实现同时播放的每条音效独立音量。

文件测试覆盖输入验证失败和正常读写，不模拟磁盘写入一半损坏、磁盘满、权限撤销或突然断电，不证明事务或崩溃恢复保证。
