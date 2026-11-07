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
| 5 C++ JSON | [nlohmann/json v3.12.0](https://github.com/nlohmann/json/tree/55f93686c01528224f448c19128836e7df245f72)，MIT | 已纳入单头文件、许可与SOURCE摘要，MSVC和Ubuntu GCC实际编译、双局及畸形/超限测试通过 |
| 5 Linux 隔离 | [google/nsjail](https://github.com/google/nsjail/tree/d8415bc8eea43cdd413daba25af63127019a1189)，Apache-2.0 | 已在Ubuntu构建并接入，编译/运行分离策略、cgroup计量和14项沙箱检查通过 |

后续功能继续按具体任务检索。本轮已接入移动、占用、跨河队列与平报分账，代码复用现有整数A*、tick约定、规范摘要和观战播放器。

## 当前验证入口

运行 tools/verify-world.ps1，验证整数地形耗时、加权最短路径、旋转对应、阻挡、不可达、完整 96×96 查询、覆盖边界和旧日志回归。

运行 tools/run-unity-check.ps1 的 Check 动作，验证实际导入、现有 Bootstrap 与观战界面，包括迁入圆环的闭合网格。Unity 原基线的 UPM 问题仍按独立诊断记录跟踪。

## 路线①②③的补充检查

- [Unity ECSGalaxySample](https://github.com/Unity-Technologies/ECSGalaxySample/tree/36909aa9eb41784fe0a3d02d1a20f75bd400771e)的确定性说明采用固定步长和可控随机源，样例需要ECS运行时。本轮复用已有纯C#结构，不引入样例依赖或复制其源码。
- [microsoft/RulesEngine](https://github.com/microsoft/RulesEngine/tree/2310a6167170d5c51fc726fbc120f484dcb00395)为MIT许可业务规则工作流库，README介绍外部JSON与表达式求值。本轮平报是固定整数时效公式和tick状态转换，直接在现有Core/Rules边界实现，无需增加规则表达式运行时。
- 两岸坐标、地图布局和脚本由本轮按批准约束编排，源文件为fixtures/world/map-v1.json和Integration/RelayScenario.cs。旧反诈GameManager的Time.time计分与NPC意识值没有迁入新平报规则。

## 路线④⑤的检查与采用

开发前读取nlohmann/json v3.12.0的固定提交55f93686c01528224f448c19128836e7df245f72，实际复制json.hpp和LICENSE.MIT至sdk/cpp/third_party/nlohmann，SHA256见SOURCE.json，由verify-source.py核验。SDK及管道预算逻辑由当前项目实现，没有复制第三方进程管理或业务代码。

完整规则继续核对microsoft/RulesEngine固定提交2310a6167170d5c51fc726fbc120f484dcb00395，本项目采用已确认的整数tick规则，沿用当前小型状态机。新通信图BFS、物资、连续译电和计分直接接现有引擎，无新增运行时依赖。

Unity后台DTO序列化查阅[ToJson](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/JsonUtility.ToJson.html)和[FromJson](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/JsonUtility.FromJson.html)官方说明，只传递线程独占的普通Serializable数据，不访问Unity对象。

Ubuntu原机无现代g++，本轮从[xPack GNU GCC v15.2.0-1](https://github.com/xpack-dev-tools/gcc-xpack/releases/tag/v15.2.0-1)下载官方Linux x64工具链至用户家目录，未变更系统软件。分发包SHA256为f6dcc3815299a23b2b4372223c4ca5f1b5a0543ed097d57eb67e0753b7c484e0，工具链不纳入仓库。

## 5号正式平台与沙箱的采用记录

用户授权项目专用运行环境后，正式沙箱使用Ubuntu系统GCC13.3.0，C++17/O2/static及固定SDK。前述xPack版本保留为此前可信程序验证记录。具体编译器版本、源码/二进制/SDK摘要和策略摘要随编译记录保存。

nsjail源码固定为d8415bc8eea43cdd413daba25af63127019a1189，Kafel固定为76d0f41bf3eb5c4008713d64b9767b461a9129a3，两者均采用Apache-2.0。构建脚本下载上游归档并校验SHA256，保留上游LICENSE及实际主机依赖清单。项目的Python受限helper、两份策略和资源回收逻辑在deploy/sandbox内维护。构建与验收入口见[部署手册](../deploy/README.md)。

ASP.NET Core Identity及EF Core SQLite采用官方NuGet 10.0.14包。包内nuspec标记MIT许可，来源固定到dotnet/dotnet的0427b9abbead3fa64c6655eb4290c537397131fa提交。各.NET工程保留packages.lock.json中的版本与内容摘要，复用账号哈希、角色、Data Protection、事务与迁移能力。比赛规则仍直接引用本项目纯C#源码。[官方源码](https://github.com/dotnet/dotnet/tree/0427b9abbead3fa64c6655eb4290c537397131fa)

nsjail与Kafel原始归档摘要分别为b051697310426332b2b60706ce99c0009dd8f77413fe637e35b8bedbe12281fb和b9a99b91af7abc84664803709072ee7f6a787dd3f39686484503154d93ef8f33，校验值写入deploy/sandbox/build-nsjail.sh。14项沙箱和完整平台实测摘要见[验收记录](PLATFORM_VALIDATION.json)。

⑤⑥补齐沿用现有nsjail helper、cgroup目录与规则状态机。本轮核对内核memory.max、memory.peak和memory.events语义，新增实际逐页驻留增长、预备沙箱回收及正式违规回归，结果见[⑤⑥验收](STAGE56_VALIDATION.json)。[内核官方文档](https://docs.kernel.org/admin-guide/cgroup-v2.html)
