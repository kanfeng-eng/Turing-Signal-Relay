# 1号：集成负责人、核心与构建

先读[总README](../../../README.md)、[环境](../../ENVIRONMENT.md)、[契约](../../CONTRACTS.md)、[里程碑](../../MILESTONES.md)。本指南是完整任务说明；当前bootstrap分支实现与待验证项见[进度记录](PROGRESS.md)。

## 你负责什么

- 唯一初始化Unity工程，锁定Editor/包、Meta序列化、asmdef和Build Profiles。
- Core数据类型、固定tick主循环、命令入口、稳定顺序、规则版本和状态哈希。
- Bootstrap接线，Windows客户端及Linux headless入口、构建与日志/回放文件出口。
- 跨模块集成、PR审查安排、与用户确认规则；协助5号把worker接到裁判进程，避免所有服务任务压给5号。

主目录：`game/Assets/Game/Core/`、`Editor/`、`Integration/Headless/`、`Tests/Core/`；独占集成入口 `Scenes/Bootstrap.unity`。维护`ProjectSettings`、`Packages`和顶层文档。目录由你在M0创建。

## 第一批工作

1. 仓库已创建且地址见总README；与仓库拥有者确认其余四人的GitHub账号并邀请团队，核对每人能拉取并推送自己的任务分支。
2. 安装指定Unity版本，在`game/`创建2D工程；创建Bootstrap场景，只显示tick并允许暂停/恢复。
3. 建Core/World/Rules/Presentation/Integration各模块asmdef，避免核心依赖UnityEngine或循环引用。
4. 与2、3、5号冻结坐标、固定点精度、tick顺序和Observation/Command/Snapshot基础类型；合入fixture。
5. 提交工程后通知其他成员拉取，不允许他们重新生成ProjectSettings。
6. 创建Windows和Linux Server构建配置，Ubuntu实测一个不含AI的裁判启动/结束，补充真实命令。

建议首分支：`feat/1-bootstrap`。首PR不夹带完整规则、复杂网络框架或其他成员模块。

## 与其他人怎么接

| 对方 | 你提供 | 对方提供 |
| --- | --- | --- |
| 2 | 地图/单位基础类型、tick入口 | 稳定地图、移动/可见性更新 |
| 3 | 状态和动作管线、事件出口 | 规则推进、得分账本 |
| 4 | 只读Snapshot/Event、集成场景 | 观战Prefab、回放查看器 |
| 5 | Command/Observation、比赛清单/结果模型、headless进程 | AI bridge/SDK、worker调用 |

不要替其他负责人重写模块；通过小PR接入。涉及规则的未定语义由3号登记，用户定稿。

## 怎么证明完成

- M0：别人从远程拉取，指定版本打开无编译错误，Bootstrap tick递增；Windows/Linux均有启动日志。
- M1：固定种子跑两支样例AI，经2号移动、3号交报产生得分，4号能打开你写出的回放。
- M4：Windows/Linux相同输入得到相同哈希；Linux双局换边9000tick完成，产物记录版本与校验值。

首PR写清实际存在的场景和菜单，不保留想象中的启动命令。每个里程碑组织一次其他成员独立复现。

## 技术栈：你实际要用哪些工具

| 技术 | 在你的工作中做什么 | 要交付的东西 |
| --- | --- | --- |
| Unity Hub/Editor 6000.3.26f1 | 创建2D工程、设置Scene/Prefab/构建目标 | 可由别人拉取打开的game目录 |
| C#普通类、集合、整数时间 | 状态类型、命令批次、固定tick和确定性排序 | Core共享数据；Integration中的MatchEngine组合入口 |
| Unity asmdef | 将数据、逻辑、表现和适配器分开编译 | 无循环引用的程序集依赖 |
| MonoBehaviour | 只把Unity生命周期接到引擎、显示调试tick | BootstrapController；不在它里面堆所有规则 |
| Editor脚本/BuildPipeline | 构建Windows Player和Linux Server | BuildGame、实际构建日志和输出说明 |
| Newtonsoft Json + System.IO | 读比赛清单、写回放/结果 | ReplayWriter/ReplayReader及版本检查 |
| .NET10 + Process/异步IO/CancellationToken | worker启动可信裁判、收集日志、等待退出、清理 | server/Worker/MatchRunner.cs（与5号接口审查） |
| Unity Test Framework | 验证tick、命令生效和跨平台结果 | EditMode回归；少量启动PlayMode检查 |

Unity脚本不直接使用.NET10专有API；.NET10只用于server。所有组件命名是落地建议，不要求每个数据字段单独建一个文件。

## 文件级任务表（按顺序开PR）

下列是完整任务目标；bootstrap分支中已实现与待验证项以[进度记录](PROGRESS.md)为准。更多目录边界见[技术手册](../../IMPLEMENTATION_GUIDE.md)。

| 任务卡 | 主要位置 | 输入 → 输出 | 本卡验收 |
| --- | --- | --- | --- |
| 1-A 工程基础 | game/Assets/Game/Scenes/Bootstrap.unity、Integration/BootstrapController.cs | 空配置 → 可见tick计数 | 别人从Git打开后Play无编译错误 |
| 1-B 共享模型 | Core/MatchConfig.cs、MatchState.cs、Commands.cs、Snapshots.cs | 固定初态 → 可序列化DTO | 2/3/4/5用同一fixture，无私自复制类型 |
| 1-C 规则组合 | Integration/MatchEngine.cs | 状态+当前接受动作 → 下一tick状态/事件 | 一次Step恰推进1tick；无AI时也能跑满演示 |
| 1-D 文件产物 | Integration/Replay/ReplayWriter.cs、ReplayReader.cs | 权威动作/快照/事件 → 可读取文件 | 4号读同文件成功；未知版本可诊断 |
| 1-E 双平台构建 | Editor/BuildGame.cs、Integration/Headless/HeadlessEntry.cs | 比赛清单 → Linux运行/结果文件 | Ubuntu无桌面启动并正常结束 |
| 1-F worker适配 | server/Worker/MatchRunner.cs | 5号给的MatchJob和沙箱入口 → 退出状态/产物清单 | 两局独立执行，失败能回收，5号能落库 |

### 1-C 的推进器如何拆

1. 输入动作先检查版本、tick、单位归属等公共条件；能力/资源由3号规则检查。
2. 在Integration组合2号的移动推进和3号的规则推进。Core只存基础数据/工具，不反向引用World或Rules。
3. 具体“先移动还是先结算”等顺序按R15批准版本执行；在批准前测试标provisional。
4. 产生只读快照和事件给4号，把按side过滤的数据交5号发给AI。
5. 状态哈希按明确字段和稳定顺序生成，不能用对象GetHashCode或无序JSON序列化结果代替。

### 1-F 与5号的交接

5号提供任务领取/数据库、源码编译、沙箱调用和资源计量；你实现“如何启动裁判并拿回结果”。不在MatchRunner里重新实现登录、编译器或沙箱。正式运行通过5号的沙箱入口启动AI，不能直接Process.Start选手二进制。

## 首个PR具体交什么

- game工程及锁定版本、Bootstrap场景、最小tick显示。
- 一个共享帧fixture与对应DTO说明；2—5号确认可使用。
- README中的实际打开方式；能复现的日志/截图。
- 暂不加入完整报文玩法、最终UI或服务器全部代码。

## 可交给AI的一张任务卡

```text
我是1号。先读README、docs/CONTRACTS.md、docs/IMPLEMENTATION_GUIDE.md和docs/team/1/README.md。
本次只做1-A工程基础和最小tick显示，不实现其他模块。
检查现有Unity版本和文件；如工程已存在则复用。只创建当前任务必需的脚本/场景。
给出实际打开与验证步骤；不能把未运行构建写成通过。涉及R15时先标出未定项。
完成后列出改动、运行结果和下一张任务卡。
```

## 当前开发入口

团队版本继续6000.3.26f1。临时使用6000.6.8f1时，严格按[独立副本步骤](../../ENVIRONMENT.md#临时使用新版开发基线保持不变)操作；当前已验证内容和仍需接手的任务见[进度记录](PROGRESS.md)。
