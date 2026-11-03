# 5号：C++ SDK、AI桥接、评测平台与沙箱

先读[总README](../../../README.md)、[契约](../../CONTRACTS.md)、[架构](../../ARCHITECTURE.md)、[规则台账](../../RULES.md)。你负责最主要的信任边界；先做一帧往返和隔离验证，再做平台页面。

## 你负责什么

- C++17 SDK、样例AI、受控stdin/stdout协议、进程生命周期和异常诊断。
- `ai.cpp`提交、统一编译、任务状态、worker评测、结果与产物查询。
- Ubuntu部署、低权限、编译/运行隔离、CPU/内存/输出限制和清理。
- 选手/裁判权限、队伍数据隔离、源码和日志可见性。

主目录：`sdk/cpp/`、`server/`、`deploy/`、`game/Assets/Game/Integration/Ai/`、`fixtures/protocol/`、`fixtures/sandbox/`。

**分担约定**：1号负责headless入口、构建以及调度与裁判集成；3号负责结果账本/回放内容；4号协助页面布局。你不独自承担完整游戏和所有服务器功能。

## 第一批工作

1. 不等Unity工程：准备Ubuntu 24.04测试环境；锁定GCC、编译参数、运行依赖与隔离执行器版本，提交可复现的小型验证。
2. 与1号冻结JSON Lines fixture，用一个最小C++17程序收一帧、回一条合法动作；记录实际编译和运行命令。
3. 建`feat/5-cpp-bridge`，封装`getframeinfo()`和`moveTo`，让1号接入空裁判；然后扩充附录A所有函数。
4. 将样例AI限定为公开Observation，确认不会读取隐藏全图状态；准备两支最小合法样例AI供M1使用。
5. 先以worker/命令行完成一次隔离编译和对局，再做ASP.NET提交和裁判界面；尽早与1号分担集成。

## 正式执行约束

- 不接受预编译二进制；编译源码与执行AI均在隔离环境进行，不以Web API服务身份运行不可信程序。
- 每局每方独立沙箱；两局同种子但进程/存储互不相通。
- 策划案要求10ms/tick、256MB。CPU/墙钟、启动宽限和超限判定属于R11/R12，不自行认定“HTTP 10ms”即可。
- 无网络、最小只读文件系统、受控临时空间、进程/线程限制、系统调用策略、CPU/内存/磁盘/输出上限、整体超时及完整回收。
- 静态扫描只是辅助；不能只搜索危险函数名就声称完成沙箱。
- 容器权限、宿主内核、系统调用过滤与执行器能力必须有实际拒绝样例；不使用特权容器或挂载宿主敏感目录作为捷径。
- 开发阶段Windows直接运行受信任样例AI可用于协议测试；此路径不得接收第三方提交，也不计为正式隔离验收。

## 平台最小交付

采用ASP.NET Core/.NET10单体、SQLite持久化队伍/提交/任务/结果、磁盘保存产物。首版不用分布式队列；并发数实测后配置。

提交页包含源码上传、编译状态和诊断、历史提交；裁判页选择双方提交、种子和冻结规则后发起比赛；结果页显示两局分账、异常原因和有权限的回放入口。

开始使用状态：queued / compiling / ready / running / completed / failed；实际状态机在首个服务PR中定稿，重启后不静默遗失任务。源码哈希、比赛输入、编译器/镜像、结果版本须关联。

## 验证与验收

- C#/C++同fixture往返一致；畸形JSON、超长行、重复/过期动作、AI崩溃不会拖垮裁判。
- 用合法程序、编译错误、死循环、内存增长、输出洪泛、尝试联网、读宿主文件、开子进程/线程逐项验证限制。
- 每个超限样例留下拒绝原因、退出状态和清理证据；其他并行比赛与平台继续工作。
- 上传大小/扩展名/归属验证；普通选手无法读取他人源码或发起裁判操作。
- 干净Ubuntu机器执行部署文档后，提交源码→编译→双局→结果/日志/回放完整通过。
- 记录目标服务器配置、并发和10ms预算统计；不把Windows协议演示当正式Linux评测通过。

遇到系统权限或内核限制时，给出实际错误和替代执行器比较；不要关闭隔离措施以通过演示。

## 技术栈：明确拆成SDK、平台、worker、部署

| 技术 | 用来完成什么 | 交付物 |
| --- | --- | --- |
| C++17/GCC、标准输入输出 | 编译选手代码、帧循环、动作缓冲 | turing_sdk.hpp/cpp、两支样例ai.cpp |
| nlohmann/json | SDK内部解析帧/生成动作JSON | 固定版本依赖和许可，跨语言fixture |
| Unity侧C#、Newtonsoft Json、进程管道 | 可信AI桥接、收发/解析/诊断 | AiProcessBridge、可见帧往返演示 |
| .NET10/ASP.NET Core/Razor Pages | 提交与裁判API、页面后端 | server/Platform，4号负责页面布局 |
| ASP.NET Core Identity | 登录、选手/裁判角色、密码存储 | 认证与角色策略；队伍归属还须逐资源检查 |
| EF Core10 + SQLite | 保存队伍、提交、编译、比赛、状态与产物索引 | DbContext、迁移和数据库恢复说明 |
| .NET10 Worker/BackgroundService | 领取任务、执行编译/比赛、持久化状态 | server/Worker；1号实现其中的MatchRunner |
| nsjail、namespaces/cgroups/seccomp | 隔离不可信编译/AI执行 | 编译/运行分开的策略与拒绝用例 |
| Ubuntu/systemd/Nginx | 服务运行、HTTPS入口、目录权限与恢复 | deploy配置和经实际验证的部署README |

JSON不手写解析；密码不自定义哈希算法；上传请求里不直接编译或等待15分钟比赛；不先搭分布式队列。nsjail是首选POC对象而非已验证的安装成果，需要记录宿主内核、权限、cgroups模式和实际限制效果。

## 文件级任务表

下面均为待实现位置，不是现成可运行入口。

| 任务卡 | 文件/内容 | 输入 → 输出 | 验收 |
| --- | --- | --- | --- |
| 5-A 协议fixture | fixtures/protocol、sdk/cpp的最小程序 | 一行frame JSON → 一行commands JSON | C#与C++解析同字段，坏数据可诊断 |
| 5-B SDK | sdk/cpp/include/turing_sdk.hpp、src/turing_sdk.cpp、examples | 选手函数调用 → 帧末动作批次 | 两支样例能用getframeinfo和moveTo，不手拼JSON |
| 5-C 游戏侧桥接 | game/Assets/Game/Integration/Ai/AiProcessBridge.cs | 过滤帧+受控进程管道 → 候选动作/诊断 | stdout协议、stderr日志；读写无死锁、异常可回收 |
| 5-D 隔离执行 | deploy/sandbox/compile.cfg、run.cfg及测试程序 | 源码/二进制+冻结资源策略 → 编译结果/受控运行 | 编译攻击和运行攻击均不能越界 |
| 5-E 平台和存储 | server/Platform/Program.cs、Data、Models、Endpoints/Pages | 授权提交 → 持久化记录/查询接口 | 登录、队伍归属、上传限制、编译诊断可查 |
| 5-F worker流程 | server/Worker/Program.cs、EvaluationWorker.cs | 持久化任务 → 编译/比赛/结果状态 | 有界并发、重启恢复；调用1号MatchRunner |
| 5-G 运维 | deploy/systemd、nginx、README.md | 干净Ubuntu+配置 → 可用平台 | 另一成员按真实命令部署并完成一次评测 |

5-D应尽早与5-A/5-B并行推进，不等网页做完才发现目标机器无法隔离；5-E前可以用受控命令行验证编译到对局流程。

## SDK要怎样封装

1. SDK主循环收取受限frame，构造只读FrameInfo，暴露给选手策略函数。
2. 选手调用moveTo/setupStation等函数时先写入本帧动作缓冲，不立即改状态。
3. 帧末统一输出一行commands并flush；普通调试只写stderr，且有限额。
4. 完整覆盖策划案附录A；每个函数文档含参数、适用单位、生效tick、失败处理与样例。
5. SDK不提供文件/网络/线程等工具；真正禁止依靠运行沙箱，不能只靠头文件不暴露函数。

首个C++示例只需要合法往返，不要求智能策略或训练模型。提供CMakeLists及Ubuntu GCC实际编译命令；Windows本地编译器用于受信任样例，正式只认冻结的Ubuntu工具链。

## 平台数据库至少表达什么

| 记录 | 关键字段/关系 | 原因 |
| --- | --- | --- |
| Team及成员关系 | TeamId、UserId、角色 | 每个查询检查本队/裁判权限 |
| Submission | TeamId、源码哈希、受控存储名、时间、状态 | 来源可追踪，不信任上传文件路径 |
| Compilation | SubmissionId、工具链版本、退出原因、诊断、产物哈希 | 编译失败与运行失败分开 |
| Match/Job | 两侧SubmissionId、种子、rulesHash、状态、领取/结束信息 | 防止重复执行/静默丢单，支持恢复 |
| Result/Artifact | MatchId、两局结果、产物相对路径、校验值 | 授权下载与审计 |

这是字段需求，不要求每行独立建一张表或仓储抽象；在首个迁移PR中用满足查询/一致性的最少模型实现。账号采用Identity；仅有角色不足以验证某个Submission属于当前队伍。

## 你和其他编号如何交接

- 给1号：`MatchJob`/`MatchRunResult`样例、可信的沙箱启动适配函数、诊断/终止约定；1号负责裁判进程、双局调度和产物收集。
- 给2号：Observation过滤后的测试输出，请其确认隐藏信息不出现；你不实现第二套视野。
- 给3号：动作与失败码清单、结果页所需字段；胜负和得分来自其结果计算，不在Web层重算。
- 给4号：Razor页面的PageModel假数据、固定路由、字段名和本地启动方式；.cshtml/CSS由4号改，.cshtml.cs/数据库/授权由你改。
- 在线观战提供授权的`GET /api/matches/{id}/snapshot`，内容含版本/tick/状态/快照；worker从裁判受控输出更新快照，不能读选手提供的任意路径。

## 验证顺序与首个PR

首PR交5-A：固定C++依赖/许可、一个SDK最小往返程序、真实编译命令及C#/C++字段对照；同时交Ubuntu隔离POC计划与一项实际拒绝证据（如尚未具备机器，明确标未验证）。

随后按合法AI → 编译失败 → 畸形协议 → 超时 → OOM → 输出洪泛 → 文件/网络/线程/进程拒绝 → 并行任务不受影响进行验证。10ms计量按R11裁定，不能拿整个进程启动耗时或nsjail总时限代替逐tick统计。

平台项目创建后，在文档中补充真实的`dotnet restore/build/test/run`目标和数据库迁移命令；当前不复制带虚构csproj路径的命令。部署脚本首次落地的PR必须给干净Ubuntu复现结果和服务日志。

## 可交给AI的一张任务卡

```text
我是5号。阅读README、CONTRACTS、IMPLEMENTATION_GUIDE和5号指南。
只做5-A最小C++17协议往返：读取一行frame，调用缓冲moveTo，输出一行commands并flush。
用固定版本nlohmann/json并保留许可，复用统一fixture；不新建Unity工程、不写完整Web平台。
给出GCC实际编译/验证结果，测试畸形JSON；开发样例标明受信任，不声称已完成沙箱。
列出下一步与1号AiProcessBridge对接需要的字段和时序问题。
```
