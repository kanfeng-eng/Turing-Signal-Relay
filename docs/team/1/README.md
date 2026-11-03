# 1号：集成负责人、核心与构建

先读[总README](../../../README.md)、[环境](../../ENVIRONMENT.md)、[契约](../../CONTRACTS.md)、[里程碑](../../MILESTONES.md)。本指南是任务说明，当前尚无工程。

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
