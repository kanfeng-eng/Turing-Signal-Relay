# 接口契约草案 v0.1

**接口实现仍在推进。** 用户个人承担原 1—5 号模块。规则 signal-relay-1.0 已于 2026-11-07 批准，R01—R18 均选 A。规则具体参数见 [批准记录](RULE_DECISIONS.md)，本文件中的历史建议若有冲突，以批准记录为准。

## 命名与坐标

- 阵营 ID：`0` / `1`；SDK 必须读取 `side`，不得写死红蓝。
- 网格坐标范围 `x,y = 0..95`；建议左下为原点，x 向右、y 向上，旋转映射 `(95-x,95-y)`。R03 已批准此约定。
- R03/R04 已批准四方向、整数边耗时、欧氏覆盖和单人格占用。权威规则使用整数，渲染位置可插值。
- `unitId`、`messageId`、`stationId` 在一局内稳定；每局独立命名空间。
- 时间统一整数 tick，`tick=0` 是初始帧，R15 A 已批准命令、移动、链路与交付顺序。
- 计分建议存整数半分 `scoreHalfPoints`，例如 37.5 分存 75；R08 已批准此精度。

## Core 内部边界（建议签名）

| 名称 | 职责 | 主责 |
| --- | --- | --- |
| MatchConfig | 时长、规则版本、种子、地图输入 | 1（参数由3提供） |
| MatchState | 权威状态，不直接对外序列化给AI | 1 |
| Observation | 按 side 过滤后的可见帧 | 2、5，1审查 |
| CommandBatch | side、tick、动作列表 | 1、5 |
| Step(commands) | 推进一次固定 tick | 1 |
| ResolveMovement(state) | 路径与占用，供 Step 调用 | 2 |
| ResolveRules(state) | 链路、任务、得分，供 Step 调用 | 3 |
| Snapshot / Event / ScoreEntry | 展示与审计的只读数据 | 1、3、4 |

M0 先落共享类型和空测试数据，不为每个函数创建一层无用途接口。实际依赖顺序：Core 为基础；World/Rules 只依赖 Core；Integration 中的 MatchEngine/Step 组合调用移动与规则推进；Presentation 读取数据。Core不反向引用World/Rules，避免asmdef循环。文件归属和技术选型见[实施手册](IMPLEMENTATION_GUIDE.md)。

## AI 协议

UTF-8、每行一条完整 JSON；`protocolVersion` 必填。stdin/stdout 由可信桥接器控制。R11 A 已批准帧长、动作数、输出上限及握手预算，R12 A 已批准异常策略。实现应使用同一组限制，解析失败不得拖垮裁判。

帧样例（只展示字段轮廓，完整可编译模型由 1、5 号补充）：

```json
{"protocolVersion":1,"type":"frame","tick":0,"side":0,"units":[],"visibleEnemies":[],"messages":[],"resources":[],"scoreHalfPoints":0}
```

应答样例：

```json
{"protocolVersion":1,"type":"commands","tick":0,"actions":[{"unitId":1,"action":"moveTo","x":12,"y":18}]}
```

- 帧 N 产生的动作最早在 N+1 生效；首帧、末帧和结算的具体时序按 R15 A 并写入 fixture。
- 一单位一 tick 仅接收一个动作；按 R06 先取首条，再验证合法性，后续重复项忽略。
- 己方单位/建筑、可见敌人、报文、资源、地形知识、得分的可见性分别定义；不得把全状态 JSON 发给选手再要求其自行忽略。
- 信任边界校验阵营归属、ID、坐标、能力、阶段、资源和冷却；不得信任客户端提交的分数或 side。
- 5 号提供 C++17 `getframeinfo()`、`move`、`moveTo`、`setupStation`、`withdrawStation`、`suppress`、`buildBridge`、`buildRelay`、`pickup`、`loadMsg`、`deliverMsg`、`wait`。参数按策划案附录 A，补充参数按 R05/R10 A 执行。
- 选手不直接写 JSON；SDK 缓冲函数调用并在帧末统一提交。
- 正式 Ubuntu 执行模型建议每方一个长期存活的 AI 进程，每 tick 往返；R11 A 已批准每方 10ms CPU、100ms 墙钟兜底、2 秒初始化宽限及 256 MiB 内存上限。目标 Ubuntu 资源计量通过前，性能数据标注开发口径。

## 回放与结果

回放头至少记录：`engineVersion`、`protocolVersion`、`replayVersion`、`rulesHash`、`mapHash`、`seed`、两侧 AI 源码哈希、初始状态。

回放主体：每 tick 的有效动作、事件、周期检查点及状态哈希；结尾记录总分、逐项账本、异常终止原因。检查点频率和快照压缩由实际体积测试决定，不提前引入复杂格式。

比赛结果包含两局各自 side 映射、得分、急报送达数、有效性和异常原因；聚合胜负遵循 RULES，不把程序退出成功等同有效比赛。异常局及平局按 R12 A 执行。

## 平台最小能力

建议由 5 号实现以下路径，具体 API Schema 在其首个 PR 中提交；当前不可调用。

| 路径 | 功能 |
| --- | --- |
| POST /api/submissions | 身份验证、队伍归属、文件上限、保存源码和哈希、返回 submissionId |
| GET /api/submissions/{id} | 查询有权限的提交状态和受限编译诊断 |
| POST /api/matches | 裁判权限：指定双方已编译提交、种子和规则版本 |
| GET /api/matches/{id} | 查询排队/运行/结束/失败及双方结果 |
| GET /api/matches/{id}/artifacts | 按权限查询结果、日志、回放下载入口 |

实时观战沿用同一只读快照结构。首版基线：5号提供授权的 `GET /api/matches/{id}/snapshot`，响应含版本、tick、比赛状态和快照；4号用UnityWebRequest非阻塞轮询并丢弃过期响应。轮询频率是展示参数，实测决定；不得影响裁判tick或把观战全图数据发给AI。离线回放先于远程实时观战验收。

## M0 必须提交的 fixture

空帧、可见/隐藏敌人、合法 moveTo、越界动作、重复动作、未来/过期 tick、版本不匹配、畸形 JSON、超长输出、正常结束及错误结束样例。1、5 号用同一份 fixture 验证 C# 和 C++ 序列化，4 号用快照 fixture 独立开发 UI。
