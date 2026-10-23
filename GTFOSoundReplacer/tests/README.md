# 自动检查

运行：`pwsh -NoProfile -File GTFOSoundReplacer/tests/run-all.ps1`。

要求 PowerShell 7 及其自带 ref 编译引用程序集（含 System.Text.Json），不依赖游戏安装、NuGet、本机固定路径或插件 DLL。可传 -PowerShellPath 选择启动套件的 pwsh；-Suite Route/Player/Core/Preview/PreviewNative/ReplacementVoice 可单独运行。默认在六个独立进程编译实际生产源码，避免 Add-Type 已加载类型污染结果。

1.0.5 成功断言数（包含循环计数，不代表独立场景或游戏实测次数）：路由42、播放器38、核心57、目录40、预设59、试听状态机37、试听对象30、替换与语音边界39，合计342。

## 范围

- Route 编译真实 RouteBindingState：默认更新后的 Reset→缓存 gain 顺序、连续换代、默认与显式模式、失败重试、释放对象清旧状态。
- Player 编译真实 SoundBankPlayer/ListenerRouting/状态：默认入口即时维护、Post 共用锁、精确 hook 目标签名、异常解锁后另一线程可继续、RTPC/近单位音量原语义、旧 PlayRaw 接口边界、Shutdown 禁止后续原生写入。
- Core 编译真实 Entry/Map/Catalog/模型/Codec：映射与音量边界、目录唯一性与搜索、JSON 往返、新 JSON 无 sourceType；已有 SoundBank 字符串/数字 1/无标记可读；Unsupported 标记、重复字段及错误结构拒绝。
- Catalog 在 Core 进程编译真实 CatalogFiles/CatalogService：BNK-only、去重及多库来源、可选 TXT、结构变化及跳过 DATA、缓存验证、离线注入下载成功/错误保留文件、快照更新。测试不联网，不代表真实 GitHub/游戏 UI 验收。
- Preset manager 编译真实管理器与模型/codec/map：临时文件 Save/Load/Import/Export；验证失败保留当前 map、原件和已有目标文件；规范导出副本保留源文件；自动保存、默认预设优先及既有恢复回退行为。

JSON 使用真实 System.Text.Json。Preset manager 仅替换游戏目录、配置和日志入口，使用真实文件系统与生产逻辑，没有重写 codec。文件写入仅在随机命名的系统临时目录，结束后验证路径边界并清理。

## 限制

Player 使用 Unity/Wwise/Harmony 外部替身，手工反射调用真实 hook；不证明真实 Harmony/IL2CPP detour 能拦截原生调用。锁断言只覆盖测试入口，不证明其他入口都经过该锁。

Route 的队列模型区分接受命令与消费命令，不是真实 Wwise。反例确认提交成功但处理阶段拒绝时，逻辑无法得知实际连接成功。实机仍须延迟 GetListeners、输出增益和听感验收。

音库加载/卸载、内存对齐、非播放动作、自然 Scout、连续跨局、死亡重进及其他声音仍需审查或实机验证。近单位音量提前返回的共享对象增益继承是保留的既有行为，并未实现同时播放的每条音效独立音量。

文件测试覆盖输入验证失败和正常读写，不模拟磁盘写入一半损坏、磁盘满、权限撤销或突然断电，不证明事务或崩溃恢复保证。

## 真实 Unity GUI 兼容检查

build.ps1 编译后会自动调用 UnityGuiCompatibility.ps1，读取目标游戏 BepInEx/core/Mono.Cecil.dll 和 interop 中的程序集元数据，检查 UI、热键组件与 PreviewEmitter 引用的 Unity 方法及其 Unity 包装调用链。遇到抛出 Method unstripping failed 的裁剪占位实现即拒绝构建验收，不执行游戏代码。

此检查需要本机游戏依赖，与 run-all.ps1 的342项离线测试分开。故障版本的 GUILayout.FlexibleSpace 调用已作为真实反例验证能被检出；1.0.5 检查73个不同方法签名通过（包含递归包装调用，不执行这些方法）。不覆盖原生实现、动态分派、实际布局、字体与渲染，仍须实机确认。

Unity兼容检查还会拒绝MonoBehaviour暴露的本插件托管参数/返回值（包括未注册的自定义枚举）。旧DLL六处签名泄漏已作为反例检出；修复DLL保留Awake/OnGUI，辅助方法显式HideFromIl2Cpp。仍须通过游戏启动日志确认警告和错误消失。

预设管理器新增导入结果名称、失败清除旧名称、带引号路径的检查；Import成功仍只登记预设，Load才应用。界面导入反馈和加载恢复已实机验证。

语音原音试听新增角色/强度 Switch 设置顺序及设置失败不提交事件检查。菜单语音、普通音、原音/替换音、循环停止、切换、换局后复用、正常退出及强制结束后重启均有用户与日志验证。


试听原型新增两个套件：Preview 直接编译 PreviewSession，覆盖最新请求合并、晚到/重复/同步/工作线程回调、停止超时、异常退出、零 playing ID、关闭幂等、1000 次快速请求和空闲 10000 次 Tick 零托管分配。PreviewNative 编译真实 PreviewEmitter/路由模块，以 API 替身验证创建/注册计数、上下文重置、注册失败不重建、清理步骤异常、注销失败不虚报成功、关闭后无原生写入、非主线程使用数字 ID 清理及延迟 Unity 销毁。替身不证明实际 Wwise 注销完成、IL2CPP 委托生命周期或原生内存无泄漏，上述实机流程已验证；长期原生资源和未触发的错误分支仍未充分验证。


## 角色语音替换第一版与旧混音选项删除

ReplacementVoice 套件编译真实 ReplacementResolver、SoundReplacer、AudioHook、VoiceRequest、VoiceReplacement，提供外部Unity/Wwise/IL2CPP/Harmony替身，反射调用真实补丁方法；39条断言不是39次实机。覆盖普通替换成功跳过原声、失败/禁用/未分配回退、音量0不提交且跳过原声；语音上下文、cookie和只读回调信息、真实ID、提前/重复/迟到回调、同目标多原事件、嵌套恢复、0返回单次回退、未知异常不重播、128记录上限、原回调异常收尾、完成通知不匹配及Term关闭。旧JSON的true/false字段兼容及不再序列化由Core使用真实System.Text.Json验证。

本套件不执行真实Harmony挂钩或IL2CPP委托转换，也不证明原生回调包释放；这些必须在本机语音替换原型实测。角色语音替换固定单位音量，独立于存储音量及插件全局倍率；额外断言覆盖全局0时语音仍替换、普通事件仍走0音量语义。VO滑条保持100%、提示及普通100%复位按钮已实机验证。
