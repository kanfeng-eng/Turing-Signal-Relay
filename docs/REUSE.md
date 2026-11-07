# 复用与迁移记录

用户于 2026-11-07 确认旧代码和素材均为其所有。完整旧工程保留在本地 `anti_fraud _new`，项目仅纳入实际使用的模块与精灵。原报告记录旧工程未通过 Play Mode，迁入模块需要在当前工程重新验证。

## 已迁入

| 原模块 | 当前入口 | 修改及用途 |
| --- | --- | --- |
| Anti-fraud Ring.cs | game/Assets/Game/Presentation/ObserverRangeRing.cs | 保留 36 段三角函数圆，改为可被 uGUI 地图裁剪的图形，补齐末段闭环，选中单位显示视野范围 |
| Anti-fraud DebugUIManager.cs | game/Assets/Game/Presentation/ObserverEventLog.cs | 提取限长日志，保存截断结果，以比赛 tick 替代 Time.time，限制单条文本，回退或切换来源时清空，绑定观战侧栏 |
| 已修复的旧模型渲染图 | game/Assets/Game/Resources/LegacySprites | 13 张已有精灵及导入配置随项目提供，选中单位、报文和设施沿用现有资源加载入口 |

原代码文件摘要见 fixtures/rules/legacy-sources.json，模型摘要与所有权确认见 game/Assets/Game/Art/LegacyManifest.json。这里只使用自有代码和素材，不自动给它们套用第三方许可。

## GitHub 开发前检查

以下均为 2026-11-07 读取的仓库和固定提交。候选通过相关模块的兼容性验证后才引入。候选的许可证、依赖和来源声明随实际引入文件保留。

| 模块 | 候选与提交 | 结论 |
| --- | --- | --- |
| 2 地图寻路 | [roy-t/AStar](https://github.com/roy-t/AStar/tree/3d57efaa83c8cd217e4973fd4738c57e2f694bd2)，MIT | 已读 Velocity.cs，使用 float 速度。新规则要求逐边整数 tick 与旋转对应的稳定同价路径，当前新增小型纯 C# 格网 A*，不引入此依赖或复制其源码 |
| 4 二维显示 | [Unity-Technologies/2d-extras](https://github.com/Unity-Technologies/2d-extras/tree/8de698ac55b7ed01ec95a43e714c6358505b8110) | 现有运行时 uGUI 地图已经支持快照、迷雾和点击。当前范围圆与日志复用自有旧代码，后续地图编辑工具如需引入此仓库，须核对具体文件许可 |
| 5 C++ JSON | [nlohmann/json](https://github.com/nlohmann/json/tree/b54267d2a74c085ff2b05b01e369ecb7dd70cec6)，MIT | 候选，尚未接入 SDK，需要验证编译、帧长和解析异常 |
| 5 Linux 隔离 | [google/nsjail](https://github.com/google/nsjail/tree/d8415bc8eea43cdd413daba25af63127019a1189)，Apache-2.0 | 候选，尚未作为正式沙箱验收，需要目标 Ubuntu 的 namespaces、cgroups、seccomp 与资源计量验证 |

后续 1、3、5 号模块进入具体实现前继续针对当前任务检索。当前 C# 格网寻路只完成路径查询、地形边耗时和覆盖距离，跨河调度、占用推进及正式比赛组合仍需接入。

## 当前验证入口

运行 tools/verify-world.ps1，验证整数地形耗时、加权最短路径、旋转对应、阻挡、不可达、完整 96×96 查询、覆盖边界和旧日志回归。

运行 tools/run-unity-check.ps1 的 Check 动作，验证实际导入、现有 Bootstrap 与观战界面，包括迁入圆环的闭合网格。Unity 原基线的 UPM 问题仍按独立诊断记录跟踪。
