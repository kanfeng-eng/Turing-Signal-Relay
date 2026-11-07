# 电波接力 · Turing Signal Relay

第十九届图灵杯“强军·电波 2027”比赛系统，使用C#规则核心、Unity 2D观战客户端和C++17选手AI。仓库地址为[kanfeng-eng/Turing-Signal-Relay](https://github.com/kanfeng-eng/Turing-Signal-Relay)。

## 公网访问

浏览器直接打开[平台登录页](http://111.231.101.147/Login)，由裁判分配队伍账号后上传ai.cpp、参加评测和下载结果。HTTP以明文传输登录信息与令牌，使用本项目专用密码。

观看游戏画面时，下载并完整解压[Windows客户端](http://111.231.101.147/downloads/TuringSignalRelay-windows.zip)，运行TuringSignalRelay.exe，再打开examples/game-1.replay.json。在线比赛在平台结果页生成观战地址与令牌，填入Unity侧栏连接。选手接口见[C++17 SDK下载](http://111.231.101.147/downloads/TuringSignalRelay-sdk.zip)。

发布版本为1.0.0 RC1，工程与构建统一使用Unity6000.6.8f1。公网发布与剩余验证范围见[本轮发布记录](docs/PUBLIC_RELEASE.json)。

## 首次使用

| 你要做什么 | 阅读与运行入口 |
| --- | --- |
| 看一场完整比赛 | [如何玩这个游戏的使用报告](docs/如何玩这个游戏的使用报告.md)，从Windows包加载examples中的两局回放 |
| 写AI参加比赛 | [C++ SDK](sdk/cpp/README.md)，用裁判分配的账号提交UTF-8的ai.cpp |
| 理解架构和实现 | [完成这个游戏的技术报告](docs/完成这个游戏的技术报告.md) |
| 查看最终发布裁定 | [发布验收报告](docs/发布验收报告.md)与[机器记录](docs/STAGE8_VALIDATION.json) |
| 构建与部署 | [发布说明](docs/RELEASE.md)和[Ubuntu部署手册](deploy/README.md) |
| 接手继续开发 | [ChatGPT与Desktop Commander Remote交接提示词](docs/HANDOFF_CHATGPT_DESKTOP_COMMANDER.md) |

将TuringSignalRelay-windows.zip完整解压到一个新目录，运行TuringSignalRelay.exe。在右侧输入examples/game-1.replay.json的完整路径并点击“打开回放”，再点击“播放”。另一局为game-2.replay.json。客户端提供单步、倍速、时间轴、阵营视角、镜头拖动、缩放及单位跟随。

选手通过AI程序控制单位，观战客户端用于查看状态与回放。正式比赛由裁判安排两支不同队伍的就绪提交，第二局自动换边。账号、上传、在线观战与故障处理的逐步操作见使用报告。

## 当前状态

更新于2026-11-07。当前由用户个人承担全部五个模块。

用户已取消原Unity版本限制，当前采用已验证的6000.6.8f1统一构建，并按用户选择启用服务器IP的HTTP访问。干净Ubuntu从零安装继续单独列为待验项，本次采用RC预发布。此前版本调整前的⑧结论保留于[验收报告](docs/发布验收报告.md)。

本轮5份交付包位于本机`artifacts/release-http/delivery`，源码基点为干净提交cbb4135，包含Windows观战、Linux原生裁判、正式服务、SDK和源码。原阶段7的5份包继续保留各自构建来源与摘要。操作见[发布说明](docs/RELEASE.md)。

用户本轮“⑤完整玩法、⑥正式沙箱”的实现、边界修复与扩展验收见[⑤⑥说明](docs/STAGES_5_6.md)和[机器记录](docs/STAGE56_VALIDATION.json)。完整规则检查扩展为76项，新增台站缓存与预备沙箱回收回归。

- 完整玩法、本地双AI对战、双局换边和可校验回放已接通，功能集为`relay-full-v1`，协议2。
- 5号的账号、源码提交、隔离编译、持久化评测、下载与观战接口已实现并部署到Ubuntu，当前构建已通过网页上传到双局结果的完整检查。
- Ubuntu的14项沙箱检查、文件违规与三次CPU超时判负、Worker重启恢复、6路并发上传限额及备份恢复实测通过，证据见[平台验收记录](docs/PLATFORM_VALIDATION.json)。
- Unity工程版本和本轮构建版本均为`6000.6.8f1`。使用PublicSource生成的源码副本完成导入与构建，元数据分别记录源码与版本。

开发分支为`feat/1-bootstrap`，本轮程序包源码基点为`cbb4135`。本机工作区为`C:\Users\kanfe\Desktop\图灵杯`。接手前核对Git状态和远程分支最新提交，保留本机验证产物与私有登录资料。

使用网页端ChatGPT与Desktop Commander Remote接手时，全文复制[交接提示词](docs/HANDOFF_CHATGPT_DESKTOP_COMMANDER.md)。其中包含当前证据、剩余任务、工具路径和服务器现场。

## 已确认的产品与技术方案

| 项目 | 当前方案 |
| --- | --- |
| 游戏 | C# + Unity，2D俯视，单位仅接受AI指令 |
| 比赛 | 96×96旋转对称地图，每秒10 tick，每局9000 tick，双局同种子换边 |
| 正式裁判 | .NET10 Worker直接引用Unity工程同一份纯C#规则源码，用户已确认此宿主方案 |
| 客户端 | Unity观战、镜头、迷雾、离线回放和授权快照轮询 |
| AI | C++17 SDK，提交UTF-8的`ai.cpp`，最大256 KiB，由服务器统一编译 |
| 平台 | ASP.NET Core Identity、Razor Pages、EF Core10、SQLite和磁盘产物 |
| 账号 | 裁判统一创建队伍及选手账号，同队共享提交与比赛记录 |
| 隔离 | nsjail、命名空间、seccomp、cgroup v2，编译和运行使用分别配置的策略 |
| 部署 | Ubuntu24.04 x86_64、专用平台/Worker账号、systemd、Nginx公网HTTP与SSH隧道 |
| 规则 | signal-relay-1.0，R01—R18全部批准A，R19采用己方兵站与中央公共器材权限 |

Unity Linux Dedicated Server保留为原生构建与一致性验证入口。平台裁判和Unity客户端共用规则源码，确定性整数状态与动作重演支撑跨宿主校验。

规则依据与确认记录见[规则台账](docs/RULES.md)、[批准选择](docs/RULE_DECISIONS.md)及`fixtures/rules/`。当前范围包括正赛提交评测平台、正式游戏、AI对战、观战与回放。

## 从哪里开始

| 目的 | 入口 |
| --- | --- |
| 接手本机未完成工作 | [ChatGPT + Desktop Commander Remote交接提示词](docs/HANDOFF_CHATGPT_DESKTOP_COMMANDER.md) |
| 开发或使用平台 | [5号平台说明](docs/PLATFORM.md) |
| 安装、更新、备份或恢复服务器 | [Ubuntu部署手册](deploy/README.md) |
| 编写AI、编译样例 | [C++ SDK说明](sdk/cpp/README.md) |
| 理解完整规则与C++闭环 | [路线④⑤实现说明](docs/STAGES_4_5.md) |
| 理解正式数据、移动和平报 | [路线①②③实现说明](docs/STAGES_1_3.md) |
| Unity安装与独立副本验证 | [开发环境](docs/ENVIRONMENT.md) |
| 代码来源、素材和第三方复用 | [复用记录](docs/REUSE.md) |

原编号仍用于定位职责，分别为1号集成裁判、2号地图移动、3号规则计分、4号显示回放、5号SDK与平台。原协作指南保留在[1号](docs/team/1/README.md)、[2号](docs/team/2/README.md)、[3号](docs/team/3/README.md)、[4号](docs/team/4/README.md)、[5号](docs/team/5/README.md)。这些指南中的早期任务规划结合本页当前状态阅读。

## 从源码上手

```powershell
git clone --branch feat/1-bootstrap https://github.com/kanfeng-eng/Turing-Signal-Relay.git
Set-Location Turing-Signal-Relay
dotnet restore server/Platform/Platform.csproj --locked-mode
dotnet restore server/Worker/Worker.csproj --locked-mode
dotnet build server/Platform/Platform.csproj --no-restore
dotnet build server/Worker/Worker.csproj --no-restore
```

Windows开发环境使用.NET10 SDK及C++17 x64工具链，Unity工程位于game。团队Editor版本写在game/ProjectSettings/ProjectVersion.txt。独立副本的导入、构建与版本边界见后文命令和[环境说明](docs/ENVIRONMENT.md)。新机器首先运行源码校验与可信样例检查，再连接正式评测服务。

## 本机快速验证

在仓库根目录的PowerShell执行。本机.NET10 SDK已安装在下面的路径，其他机器可替换为自己的SDK位置。

```powershell
$relayDotnet = 'D:\kanfe\dotnet10\dotnet.exe'
& $relayDotnet build server/Platform/Platform.csproj --nologo
& $relayDotnet build server/Worker/Worker.csproj --nologo
./tools/check-local-match.ps1 -Dotnet $relayDotnet
```

`check-local-match.ps1`需要Visual Studio C++17 x64工具链，会创建新的产物目录，编译样例AI并执行规则、进程、双局和故障检查。完成标志为`LOCAL_MATCH_CHECKS_PASSED`。

仅复验规则、旧Worker或源码结构时使用以下入口。报告与输出目录选择新名字。

```powershell
& $relayDotnet run --project tools/RelayRunner/RelayRunner.csproj -- full-check "$PWD" "$PWD/artifacts/NEW_FULL_CHECK"
& $relayDotnet run --project server/Worker.Checks/Worker.Checks.csproj -- artifacts/NEW_WORKER_CHECK
python tools/verify-source.py --output artifacts/NEW_SOURCE_REPORT.json
git diff --check
```

Unity验证使用独立源码副本，脚本会返回副本位置和完成标志。

```powershell
./tools/run-unity-check.ps1 -EditorDirectory 'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor' -PublicSource -Action Check
```

观战场景为`game/Assets/Game/Scenes/Observer.unity`。完整对局生成的`relay.replay.json`可通过正常打开回放入口加载。

## 使用已部署平台

公网直接打开[平台登录页](http://111.231.101.147/Login)。使用SSH隧道时运行以下命令并保持终端运行，再访问http://127.0.0.1:5188/Login。

```text
ssh -N -L 5188:127.0.0.1:5188 ubuntu@YOUR_SERVER
```

裁判创建队伍账号，选手上传`ai.cpp`并查看编译状态，裁判再选择两支不同队伍的就绪提交发起双局。结果页提供总分、分账、回放和运行日志，也可生成一小时有效的本场只读观战令牌。

正式Worker在Ubuntu执行隔离编译和评测。Windows可用于平台页面开发和受信任样例的本地协议测试。服务器安装、权限、API和配额详情见[平台说明](docs/PLATFORM.md)与[部署手册](deploy/README.md)。

初始裁判凭据保存在本机私有的`artifacts/stage5/initial-judge.json`。交接提示词提供服务器连接信息和凭据文件位置。

## 目录与模块

```text
game/Assets/Game/
  Core/                 共享类型、整数状态与摘要
  World/                地图、路径、占用、过河与视野
  Rules/                通信、报文、资源、建设和计分
  Integration/          比赛引擎、AI管道、回放、双局与headless入口
  Presentation/         Unity观战、镜头、面板与回放播放器
  Editor/               构建与Unity检查
sdk/cpp/                固定依赖、SDK、样例与故障程序
server/
  Evaluation/           共享规则引用、数据库、迁移、沙箱和评测流程
  Platform/             认证、页面、提交、裁判及查询接口
  Worker/               正式评测循环及旧Bootstrap调度入口
  Worker.Checks/        子进程回归
deploy/                 nsjail、策略、systemd、部署和备份恢复
fixtures/               协议、地图、批准规则及沙箱样例
tools/                  编译、检查、构建和打包工具
docs/                   规则、实现说明、验收记录和交接
artifacts/              本机验证产物与私有运行资料
```

## 已有证据与剩余验收

| 范围 | 证据及状态 |
| --- | --- |
| 正式数据与移动平报 | 52项逻辑检查及9000tick，见[阶段记录](docs/STAGE123_VALIDATION.json) |
| 完整规则与本地AI | 当前76项、52项旧逻辑与9项进程检查通过，双局各9000tick，见[⑤⑥记录](docs/STAGE56_VALIDATION.json)；原生对照保留于[此前记录](docs/STAGE45_VALIDATION.json) |
| 旧Worker回归 | Windows与Ubuntu各24项通过，当前记录见[公网发布](docs/PUBLIC_RELEASE.json) |
| 平台正常全流程 | 公网部署通过，位于`artifacts/release-http/public-platform/CHECKS.json` |
| Ubuntu沙箱 | 新一轮14项通过，驻留内存增长至约252.9 MiB后受限，本机报告为`artifacts/stage56/sandbox-checks.json` |
| 数据迁移与备份 | 恢复到独立目录通过，7个账号、5份源码、3份产物及登录/下载核验成功 |
| 故障与恢复 | 文件违规、三次CPU超时判负、最终快照、日志授权和重启后第二次尝试通过 |
| 正式沙箱扩展 | 11种单方违规、双方同时违规及SIGKILL后恢复共13项通过，预备沙箱回收回归通过 |
| 并发与幂等 | 同队6次并发上传仅接受5份，同一请求重复发起及冲突拒绝通过 |
| Unity授权观战 | 独立副本连接正式接口的4项快照检查通过，含正常与故障终局 |
| 发布与回滚 | 同一构建发布到新版本目录再回滚，服务健康与数据库计数一致 |

当前部署与交付摘要见[公网发布记录](docs/PUBLIC_RELEASE.json)，⑤⑥细节见[完整玩法与沙箱验收](docs/STAGE56_VALIDATION.json)，此前账号、队列与备份恢复证据保留在[平台验收记录](docs/PLATFORM_VALIDATION.json)。接手操作与复验顺序见[交接提示词](docs/HANDOFF_CHATGPT_DESKTOP_COMMANDER.md)。

原策划案与完整旧工程保留在本地，已迁入的自有代码和精灵有来源记录。贡献与分支约定见[CONTRIBUTING](CONTRIBUTING.md)。
