# 技术栈、工程边界与联调手册

本文件把已有分工落实到技术和产物。所有代码路径、类名和命令入口均为**待实现约定**，当前只存在协作文档。命名可在对应首个PR中简化，但不得让两人各实现一套相同职责。

## 1. 全队共同的技术选择

| 层次 | 实现方式 | 原因及边界 | 锁版本的人 |
| --- | --- | --- | --- |
| 游戏工程 | Unity 6000.3.26f1、基础2D模板、C# | 一套工程产出Windows客户端和Linux Server；不使用Phaser | 1 |
| 权威逻辑 | 普通C#类/集合，固定tick，整数或已冻结固定点 | Core/World/Rules不引用UnityEngine，便于确定性验证；不要求另建.NET10游戏核心 | 1，2/3审查 |
| 地图显示 | Tilemap绘制地形；SpriteRenderer绘制单位；LineRenderer绘制链路 | 数据由裁判提供，显示组件不参与判分 | 4，1审核包 |
| 界面 | uGUI Canvas、TextMeshPro、按钮/滑条、简单折线图 | 先用原生组件；不引入UI框架、付费图表或另一个Unity UI体系 | 4 |
| JSON | Unity端使用官方Newtonsoft Json包；平台使用System.Text.Json | 两者靠统一字段/fixture对齐，不要求同一库；不使用对象多态自动实例化不可信JSON | 1/5 |
| C++ JSON | nlohmann/json，固定版本与许可，SDK内部使用 | 避免自己手写JSON解析器；选手只见C++函数，不手拼JSON | 5 |
| 网页与API | ASP.NET Core 10，Razor Pages，同源表单与REST API | 比较过独立SPA：本项目只有少量管理表单，Razor减少单独Node/前端构建链；无需React/Vue | 5，4做布局 |
| 账号与数据库 | ASP.NET Core Identity，EF Core 10 + SQLite | 队伍/权限/提交/任务持久化；不自制密码哈希、不先搭MySQL/Redis | 5 |
| worker | 独立.NET 10 Worker进程，BackgroundService，有界并发 | Web API只登记任务；worker领取任务并调用裁判/沙箱，避免在上传请求中跑比赛 | 5；1做MatchRunner |
| 隔离 | 优先验证nsjail，Linux namespaces/cgroups/seccomp等 | 正式选型以宿主POC为准；容器打包不替代沙箱；权限不足时提出替代对比，不删隔离 | 5 |
| 观战传输 | 首版UnityWebRequest轮询授权快照HTTP端点 | 最少额外依赖，先跑通；轮询速率是展示参数，独立于10tick/s；实测不足再讨论推送 | 4/5 |
| Linux运维 | Ubuntu24.04 x64、systemd、Nginx HTTPS反向代理、独立数据目录 | Web与worker不同服务身份；不依赖服务器桌面或Unity Editor | 5 |
| 验证 | Unity Test Framework/EditMode；必要的PlayMode；平台用.NET测试项目；C++用fixture程序 | 测规则/边界/跨语言契约，不为了覆盖率重复实现细节 | 各人负责自己模块 |

除Unity Editor已锁补丁外，其他包由首次真正引入者核实兼容版本，在manifest/lockfile/csproj/global.json等适用位置锁定，并记录许可；这里没有声称所有依赖已经安装或组合测试通过。

## 2. 三个运行环境不能混为一谈

1. **Windows开发机**：打开Unity、开发画面和逻辑；可运行自己写的受信任C++样例测试管道。不接收未知选手源码作为无隔离本地进程。
2. **Ubuntu评测服务器**：运行Web、独立worker、Linux裁判和AI沙箱；Linux裁判无图形界面。不得拿Windows的exe直接部署。
3. **Windows观战机**：运行Unity客户端，读取已授权的在线快照或离线回放；Unity客户端不要求安装服务器数据库或nsjail。

正式CPU限制、内存及超限判罚仍按[规则台账](RULES.md)定稿。nsjail总时长限制或cgroup CPU配额并不能直接证明每tick10ms的计量已经完成，5号须另给测量与熔断证据。

## 3. 目录细化与唯一负责人

| 目标目录 | 唯一主责 | 首批内容 |
| --- | --- | --- |
| game/Assets/Game/Core | 1 | MatchConfig、MatchState、CommandBatch、Snapshot等共享数据与确定性工具；MatchEngine组合入口放Integration |
| game/Assets/Game/World | 2 | GridMap、Pathfinder、MovementSystem、CrossingQueue、VisibilitySystem |
| game/Assets/Game/Rules | 3 | LinkSystem、MessageSystem、ResourceSystem、ConstructionSystem、ScoreSystem |
| game/Assets/Game/Presentation | 4 | MapView、UnitView、LinkView、ObserverHud、ReplayPlayer |
| game/Assets/Game/Integration/Headless | 1 | Linux裁判启动、比赛清单读入、结果退出 |
| game/Assets/Game/Integration/Replay | 1 | ReplayWriter、ReplayReader、快照/动作/检查点文件IO |
| game/Assets/Game/Integration/Ai | 5 | AiProcessBridge、协议序列化、超时/退出诊断 |
| game/Assets/Game/Editor | 1 | BuildGame及构建配置 |
| sdk/cpp | 5 | 头文件、SDK传输实现、样例AI、构建/fixture验证 |
| server/Platform | 5 | Program、数据实体/迁移、API、Identity、PageModel |
| server/Platform/Pages、wwwroot | 4负责布局；5负责业务 | .cshtml、CSS、少量JS归4；.cshtml.cs、鉴权和数据库归5 |
| server/Worker | 5维护worker；1维护MatchRunner | 任务领取、编译/运行流程、裁判进程适配；见下一节 |
| deploy | 5 | nsjail编译/运行配置、systemd服务、Nginx示例和部署README |
| fixtures | 谁产出谁维护 | world/rules/protocol/replay/ui/sandbox各有固定输入和期望结果 |

asmdef依赖由1号统一配置：共享DTO放Core；World和Rules依赖Core；规则推进在Integration组合World/Rules；Presentation依赖Core及必要的只读加载适配器。**避免让Core为了调用World/Rules而反向引用，造成程序集循环。** 原有“MatchEngine/Step”是对外行为名称，涉及组合的实际入口可落在Integration中。

## 4. 最容易重叠的工作，明确拆开

### 1号与5号：评测执行

- **5号**负责提交入库、任务领取/状态持久化、隔离编译、AI沙箱启动配置/诊断和资源计量。提供已校验的比赛清单、沙箱启动入口以及结果保存方法。
- **1号**实现`server/Worker/MatchRunner.cs`：接收清单，启动Linux裁判，接通可信管道，等待结果，收集退出码和产物；双局并行、换边聚合与超时后清理调用也在此适配层。不能把沙箱内部实现复制过来。
- **5号**在worker的BackgroundService中调用1号的MatchRunner，把执行状态和结果落库；**3号**产出分账数据结构与聚合规则，**1号**调用，不各自写胜负算法。
- Linux裁判负责固定tick与权威状态；AI桥接只提交候选动作。每方/每局沙箱独立，不靠直接启动选手二进制绕过5号沙箱入口。
- 首个跨模块PR固定一份`MatchJob`/`MatchRunResult` JSON，包含版本、种子、双方提交哈希、规则/地图输入、退出原因和产物相对路径。路径来源与权限由worker校验，不允许选手提供宿主任意路径。

### 3号与1号、4号：回放

- 3号定义游戏事件和逐项得分数据，例如报文交付、压制开始/结束、建设完成。
- 1号把权威动作、事件、快照/检查点写文件，并实现统一读取模型；3号验证内容与分账一致。
- 4号用读取模型播放，做时间轴/跳转/镜头；无需自己实现JSON解析和裁判重演。

### 4号与5号：页面和在线观战

- 5号先提供PageModel字段/假数据和页面路由；4号只改布局和样式；5号接真实数据、表单处理、登录/权限、CSRF保护。
- 5号提供`GET /api/matches/{id}/snapshot`：含版本、tick、快照、比赛状态；仅授权观众/裁判可读。全图快照不复用成AI帧入口。
- 4号用UnityWebRequest非阻塞拉取，按tick丢弃过期响应；断流显示提示，恢复后更新到最新状态。不得因客户端暂停而阻塞裁判。

## 5. 首轮联调的交接清单

| 先交付的人 | 交付物 | 接收人现在就能做什么 |
| --- | --- | --- |
| 1 | 共享DTO与空快照、帧/命令fixture；可打开Unity工程 | 2/3写纯逻辑；4画假数据；5实现跨语言读写 |
| 2 | 固定地图、一个单位初态、一条路径和预期移动结果 | 1接tick；3放报文；4显示地图/单位 |
| 3 | 一份平报的状态、交付动作和逐项账本；R01等裁定请求 | 1跑得分流程；4显示分数；5显示结果字段 |
| 4 | ObserverRoot.prefab、模拟快照演示、页面静态布局 | 1挂到Bootstrap；5接页面真实字段 |
| 5 | SDK样例AI、一帧往返fixture、沙箱启动与诊断约定 | 1接入AI和worker；2/3看合法动作行为；4演示本地AI对战 |

M0之前可并行写文档/fixture/独立C++程序，不各自新建完整Unity工程。需要别人尚未完成的数据时，用同契约的fixture替代，并在PR写明后续接入点。

## 6. 每个人的最小学习范围

| 编号 | 先学会 | 暂时不用学 |
| --- | --- | --- |
| 1 | C#类/集合、asmdef、生命周期、固定tick、Editor构建、进程和文件IO | 美术制作、训练AI模型 |
| 2 | 二维数组、A*、稳定排序、队列、整数时间、可见性集合 | 网页登录、Linux部署 |
| 3 | 图遍历、状态机、事件、计分精度、边界测试 | Shader、数据库运维 |
| 4 | Scene/Prefab、Tilemap、Canvas/TMP、镜头、只读数据绑定、Razor/HTML/CSS基础 | 裁判算法、沙箱实现 |
| 5 | C++进程IO、JSON、ASP.NET/Identity/EF、Linux权限及隔离、部署诊断 | Unity美术和寻路实现 |

“使用AI补全”可以帮助写代码，不取消编译、边界测试和独立复现。一次只要求AI实现一张任务卡；每个编号指南附有可直接改写的任务提示。

## 官方参考

- [Unity Newtonsoft Json](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.0/manual/index.html)、[uGUI中的TextMeshPro](https://docs.unity.cn/Packages/com.unity.ugui@2.0/manual/TextMeshPro/TMPObjects.html)。
- [ASP.NET Core Razor Pages](https://learn.microsoft.com/en-us/aspnet/core/razor-pages/?view=aspnetcore-10.0)、[后台服务](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0)、[EF Core SQLite](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/)。
- [nlohmann/json集成](https://json.nlohmann.me/integration/)、[nsjail项目](https://github.com/google/nsjail)。

这些资料说明所选工具的用法，不代表本项目已通过对应环境验收。首个实现PR仍须记录真正使用的包版本和可复现命令。
