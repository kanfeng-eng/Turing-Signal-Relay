# 电波接力 · Turing Signal Relay

第十九届图灵杯“强军·电波 2027”比赛系统。GitHub 仓库：[kanfeng-eng/Turing-Signal-Relay](https://github.com/kanfeng-eng/Turing-Signal-Relay)。

**当前状态（2026-11-03）：需求与五人协作文档已建立，GitHub 远程仓库已创建；尚无 Unity 工程、服务端代码或可运行游戏。本文中的模块路径和启动入口是后续实现约定，不代表已经存在。**

## 1. 已确认的目标

- 五人通过 GitHub 协作，成员编号固定为 1、2、3、4、5。
- C# + Unity，2D 俯视；不使用 Phaser。
- 正式比赛游戏、本地 C++17 AI 对战、实时观战、离线回放。
- Ubuntu 上的服务器评测、源码提交管理、编译和运行沙箱。
- 单位只接受 AI 指令；观众可操作镜头、暂停离线回放等，不可控制单位。
- 不制作预热竞猜网页。提交管理页面属于正赛平台，仍在范围内。
- 基本玩法：96×96 地图、180°旋转对称、每局 9000 tick、每秒 10 tick、双局换边、公平计分。

## 2. 技术基线

以下是本次制定的工程方案；规则争议另见[规则台账](docs/RULES.md)，不视为用户已确认的赛事规则。

| 用途 | 选择 |
| --- | --- |
| 开发系统 | Windows 11 x64 为团队主验收环境；Windows 10 机器须先通过相同工程的打开和构建检查 |
| Unity | Unity 6.3 LTS，统一锁定 `6000.3.26f1`，禁止个人升级 |
| 客户端 | Unity 2D，先用基础精灵和 UI，不以商业素材作为联调前提 |
| 正式裁判 | 同工程构建 Linux Dedicated Server，共用 C# 规则核心 |
| 服务器 | Ubuntu Server 24.04 LTS，x86_64；当前未部署验证 |
| 提交平台 | ASP.NET Core / .NET 10 LTS；单体服务、SQLite、磁盘产物，先支持单机部署 |
| AI | C++17 SDK；提交 `ai.cpp`，正式环境统一编译，不接收用户可执行文件 |
| 隔离 | 独立低权限评测 worker，编译和运行均隔离；具体沙箱执行器由 5 号提交小型验证后定稿 |
| 对接 | 版本化 JSON Lines，C++ SDK 封装传输；UTF-8；命令、回放协议见接口文档 |

不增加人类玩家联网对战框架。服务端是权威裁判，客户端仅显示快照；客户端和服务器不各写一套规则。

官方核实依据（2026-11-03）：[Unity 补丁版本](https://unity.com/releases/editor/whats-new/6000.3.26f1)、[Unity 系统要求](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html)、[Linux Server 构建方式](https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server-build.html)、[.NET 支持周期](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)。补丁选择是可复现基线，不声称是最新补丁；实际构建验证前不宣称兼容通过。

## 3. 按编号开始工作

| 编号 | 主责 | 第一项任务 | 上手文档 |
| --- | --- | --- | --- |
| 1 | 集成负责人、tick 主循环、共享类型、构建 | 创建统一 Unity 工程，合入最小 tick 演示 | [1 号 README](docs/team/1/README.md) |
| 2 | 地图、移动、A*、渡口与过桥队列 | 给出对称且可达的测试地图与移动演示 | [2 号 README](docs/team/2/README.md) |
| 3 | 通信链路、报文、资源建设与计分 | 给出最小送报得分和逐项分账测试 | [3 号 README](docs/team/3/README.md) |
| 4 | Unity 画面、观战、回放查看器 | 用模拟快照画出地图和单位 | [4 号 README](docs/team/4/README.md) |
| 5 | C++ SDK、AI 进程、提交平台、Linux 沙箱 | 完成隔离的 C++ 编译与一帧指令往返 | [5 号 README](docs/team/5/README.md) |

5 号任务较重：1 号承担 headless 构建和 worker 调度集成；3 号承担结算明细与回放数据；4 号承担提交/裁判页面的简单布局；5 号主攻协议桥接与隔离。交接边界写入各自指南。

## 4. 拉取后的操作顺序

### 工程初始化前

拉取仓库后，先阅读本 README → [环境准备](docs/ENVIRONMENT.md) → 自己编号的 README。可以安装环境、拆 Issue、编写协议样例和测试设计。

**只有 1 号创建 `game/` Unity 工程；其余人不得各建一个工程后互相覆盖。** M0 合入前，2—5 号在各自约定目录准备独立内容，4 号可先画 UI 草图，不提交另一个 Unity 工程。

### 克隆与建立任务分支

下面的克隆地址可直接使用；推送任务分支前需要仓库拥有者邀请你成为协作者。

```powershell
git clone https://github.com/kanfeng-eng/Turing-Signal-Relay.git
cd Turing-Signal-Relay
git switch -c feat/1-bootstrap
```

将 `1-bootstrap` 换成自己的编号与任务，例如 `2-pathfinding`。日常分支、PR、Scene 协作见 [GitHub 协作约定](CONTRIBUTING.md)。

M0 合入后：Unity Hub → Add project from disk → 选择 `game/` → 用锁定版本打开 → 打开 `Assets/Game/Scenes/Bootstrap.unity` → Play。此入口由 1 号实现，当前尚不存在。

首次打开成功标准：Console 无编译错误、能看到 tick 计数递增、暂停恢复正常。后续在同场景选择本地两支样例 AI 进行演示，控制项由 1、4、5 号在 M1 接通。

## 5. 约定的目标目录

以下是目标结构，当前只交付文档及 Git 配置，未创建游戏目录。

```text
README.md / CONTRIBUTING.md
docs/
  ENVIRONMENT.md / ARCHITECTURE.md / CONTRACTS.md / RULES.md / MILESTONES.md
  team/1..5/README.md
game/                             # 1号唯一初始化
  Assets/Game/
    Core/                         # 1号：纯C#类型、tick、命令入口
    World/                        # 2号：地图、移动、可见性
    Rules/                        # 3号：链路、任务、计分
    Presentation/                 # 4号：画面、镜头、UI
    Integration/                  # 5号：AI进程与数据桥；headless入口由1号负责
    Editor/                       # 1号：构建入口
    Tests/                        # 按负责人分子目录
    Scenes/ / Prefabs/ / Art/      # 4号主责；Bootstrap场景由1号集成
  Packages/ / ProjectSettings/
sdk/cpp/                          # 5号：C++17 SDK与样例AI
server/                           # 5号：提交API、worker；4号协助页面
deploy/                           # 5号：Ubuntu部署与沙箱配置
fixtures/                         # 各负责人提供可复现输入、期望结果
```

## 6. 工作顺序与验收

1. **M0 环境和契约**：全员同版本打开工程，协议样例一致；规则疑点登记。
2. **M1 最小闭环**：两支 C++ AI → 移动 → 送一份平报 → 得分 → 保存并查看回放。
3. **M2 完整规则**：地形、单位能力、四类报文、天气、终局与换边计分；阻塞规则全部定稿。
4. **M3 Ubuntu 评测**：上传源码 → 隔离编译 → 双局评测 → 日志、回放与结果可查。
5. **M4 正式发布**：冻结版本及配置、完整 9000 tick、重放一致、异常用例和恢复流程通过。

每项验收的证据见[里程碑](docs/MILESTONES.md)。没有给定交付日期，因此不虚构排期；1 号在 M0 收集工时后补充实际日期。

## 7. 先读这些约定

- [架构与模块归属](docs/ARCHITECTURE.md)
- [共享接口草案与冻结办法](docs/CONTRACTS.md)
- [原文规则、矛盾与待确认项](docs/RULES.md)
- 原始依据：本地审阅的《第十九届中兴图灵杯人工智能程序设计大赛策划案（1）.docx》第 4 章及附录 A—C；原件未随公开仓库发布，规则摘录和疑点见规则台账。

AI 可以辅助编码，但提交者必须能够解释关键状态变化、运行验证并审查依赖。完成标志是可复现验收证据，不是“代码已经生成”。
