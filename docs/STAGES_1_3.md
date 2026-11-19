# 正式数据、地图移动与平报计分

2026-11-07已完成本轮路线①②③，使用确定性脚本驱动。规则基线为signal-relay-1.0，本轮功能集为movement-plain-report-v1，协议为2。完整通信、建设、其他报文、资源刷新、终局与C++ AI继续按后续路线实现。

## 一条命令复验

Windows仓库根目录运行以下命令，OutputDirectory需为新目录，省略时自动生成。

```powershell
./tools/verify-relay.ps1 -Dotnet 'D:\kanfe\dotnet10\dotnet.exe' -OutputDirectory "$PWD\artifacts\relay-my-check"
```

执行52项逻辑检查，再运行完整9000 tick脚本对局、保存文件并重新读取重演。输出relay.replay.json、relay.result.json、initial.frame.red.json和initial.snapshot.json。重复命令不能覆盖已存在的输出目录。

跨平台CLI直接运行方式如下，使用.NET 10 SDK，不依赖额外NuGet库。

```text
dotnet run --project tools/RelayRunner/RelayRunner.csproj -- check REPOSITORY_ROOT NEW_OUTPUT_DIRECTORY
dotnet run --project tools/RelayRunner/RelayRunner.csproj -- verify INPUT_REPLAY NEW_RESULT_FILE
```

## 本轮实现入口

| 入口 | 行为 |
| --- | --- |
| Core/GridPrimitives.cs、RelayModels.cs | 单位、位置、命令、地图点、资源、报文、快照、观察及半分账本类型 |
| Core/RelayStateHash.cs | 配置、地图、路径、移动中状态、携带物与账本的规范二进制摘要 |
| World/RelayMap.cs、GridPathfinder.cs | 固定地图校验、可达性、静态加权路径与过河边 |
| World/MovementSystem.cs | 目标格预留、单人格占用、冲突轮换、FIFO桥渡队列、阻挡10 tick重寻路与等待/取消 |
| World/VisibilitySystem.cs | 逻辑位置圆形视野、静态地图公开和敌方内部状态过滤 |
| Rules/PlainMessageSystem.cs | 双方配对平报刷新、容量、装载、转交、交付、时效倍率与逐项分账 |
| Integration/RelayMatchEngine.cs | 命令校验及下一tick应用、移动与动作完成、得分和快照 |
| Integration/RelayRecording.cs、RelayScenario.cs | 脚本场景、有效输入记录、逐tick摘要重演与结果 |
| Presentation/ObserverSnapshotBuilder.cs、ObserverPlayback.cs | 权威状态显示和正常打开回放入口 |
| Editor/RelayStageChecks.cs | Unity完整场景、回放读写、UI初始化、损坏回放拒绝检查 |

源码路径均相对于game/Assets/Game。正式数据与原Bootstrap协议1分别保留，已有Bootstrap验证继续可用。

## 地图与计时

固定地图由本轮按批准约束编排，公开在fixtures/world/map-v1.json。rows按y从0向上排列，数字0/1/2/3分别表示草地、道路、河流、山地。地图包含两条对称河流、配对铁索桥和渡口、六个待建桥位置，以及双方总部、三报点和两兵站。预留建造桥在本轮保持未建成。

桥渡口用两岸之间的专用过河边表示。寻路代价使用静态通行耗时，实际等待由移动调度处理。普通格移动期间保留起点逻辑位置并预留终点，到达时一次更新位置，禁止迎面交换。过河每条边一次一人，已进入者在渡口关闭或收到wait后仍完成过河。

帧n提交的命令在n+1开始生效，任务从开始时刻计满所需tick。例如道路通信员一格耗时9 tick，帧0命令在帧1起步，于帧10到达。拾取、转交、交付各耗时1 tick，帧200的拾取命令于201开始、202完成。相同任务的重复指令保留进度，非法指令保留现有任务。

双方各7个可移动单位从己方总部出发，总部另以静态设施表示。设施格允许多人停留，其他格保持单人占用。初始器材为0，本轮只提供资源状态类型和初始库存，尚未启用器材刷新与建设动作。

## 协议与回放

配置样例见fixtures/protocol/relay-config.json，配套relay-frame.json、relay-command.json和relay-snapshot.json。配置固定10Hz、最长9000 tick，rulesHash必须匹配已批准文档摘要。

支持moveTo、wait、loadMsg、transferMsg、deliverMsg。动作使用unitId，移动使用x/y，装载和转交使用messageId，转交还需targetUnitId。wait使用ticks。每方一批最多128动作，同单位只处理第一条，随后才验证动作是否合法。批次的协议、side、tick或结构错误在推进前整体拒绝。

通信员容量1报文，运输员容量3报文，工兵不携报文。运输员装载指定报文时，按确定顺序继续装同格同方报文至容量。转交须同方同格且接收者有容量。交付须回到己方总部。平报每方30份，首刷200 tick，每300 tick刷新一次，双方点位旋转配对。龄期不超过600/900 tick分别为30/20半分，之后10半分，保留至本局结束。

relay-replay包含配置、地图与摘要、初始状态摘要及逐tick命令/摘要。每帧batches为数组，空数组明确表示无命令，避免Unity与.NET对嵌套null对象的差异。读取时重演整局并核对每帧，缺帧、篡改、未知版本、错side或错tick均拒绝。读取失败保留原观战记录和当前帧。

## 在Unity和构建中运行

Editor菜单选择Turing Signal Relay → Run Movement and Plain Report Scenario，生成场景回放与结果并检查原生UI。生成路径会写入RELAY_STAGE123_CHECKS_PASSED日志。Unity团队版本仍为6000.3.26f1，当前实际验证使用6000.6.8f1独立副本。

已有Windows构建可使用以下选项执行相同场景，所有路径均需替换为实际路径，输出文件必须不存在。

```text
TuringSignalRelay.exe -batchmode -nographics -logFile PLAYER_LOG --relay-config CONFIG_JSON --relay-map MAP_JSON --relay-replay NEW_REPLAY_JSON --relay-result NEW_RESULT_JSON
```

正常退出码为0，日志包含RELAY_SCENARIO_COMPLETED。打开Windows观战窗口，在回放文件路径框粘贴生成的relay.replay.json并点击打开回放，可播放、暂停、单步、跳转和切换视角。

## 验收结果

52项逻辑检查、既有World及Bootstrap回归通过。完整场景共9000 tick，生成每方30份平报，合计32次交付，双方各190半分，即95分。地图摘要为f077dbe5e3c3d6913b2537d35f0967e1d2e128f68a0e121ae2ff2e9f3ae49359，最终状态摘要为0e79c061531bb5d5ca0f3ca6dfda012d3d040270332e55280c35412030252ac2。

.NET、Unity Editor和构建后的Windows Player得到相同摘要，Unity生成的回放经.NET重新读取重演也通过。Unity正常回放入口加载9001帧并初始化界面通过，损坏回放保留原记录通过。原团队版本API静态编译通过，原版本UPM导入故障仍待解决。本轮没有重新执行Ubuntu或Linux Player验收。
