# 路线④ C++闭环与⑤完整玩法

2026-11-07。这里的④⑤为本轮实现路线，分别对应 C++ AI 最小闭环和完整玩法。原五人分工中的账号、上传、持久化任务、沙箱仍有后续工作。

完整配置为 fixtures/protocol/relay-full-config.json，协议2，featureSet 为 relay-full-v1。旧配置继续保持 movement-plain-report-v1，旧9000 tick场景摘要不变。R01—R18批准文档保持原字节摘要，新增用户确认的R19单独记录在 fixtures/rules/addendum-v1.json，完整配置同时绑定两个摘要。R19禁止取敌方兵站器材，己方仓库和中央公共器材可取。

## 一键本地复验

安装.NET 10 SDK及Visual Studio C++17 x64工具链，从仓库根目录执行。

```powershell
./tools/check-local-match.ps1 -Dotnet 'D:\kanfe\dotnet10\dotnet.exe' -OutputDirectory "$PWD/artifacts/my-full-check"
```

输出目录必须不存在。脚本编译两个样例及故障程序，运行旧52项、完整65项规则检查、9项真实进程检查、完整9000 tick脚本、C++双局换边、畸形JSON与累计3超时判罚，并从保存文件重新重演回放。最后必须看到 LOCAL_MATCH_CHECKS_PASSED 与 CHECKS.json。Windows输出明确标记 cpuBudgetEnforced=false。

## 单独运行与Ubuntu

```text
dotnet run --project tools/RelayRunner/RelayRunner.csproj -- full-check ROOT NEW_OUTPUT
dotnet run --project tools/RelayRunner/RelayRunner.csproj -- ai ROOT RED_AI BLUE_AI NEW_OUTPUT --full-rules
dotnet run --project tools/RelayRunner/RelayRunner.csproj -- series ROOT TEAM_A_AI TEAM_B_AI NEW_OUTPUT --full-rules
dotnet run --project tools/RelayRunner/RelayRunner.csproj -- verify REPLAY NEW_RESULT_FILE
```

Ubuntu使用同样CLI，加 `--strict-cpu` 强制 /proc/PID/schedstat 的纳秒级主线程CPU计量。每方10ms CPU、100ms墙钟兜底，2秒初始化。要求无额外线程，正式线程限制待沙箱强制。每局产生relay.replay.json、relay.result.json、ai.report.json和各方stderr。series额外产生series.result.json，记录两局阵营到队伍映射、总分、单局最高、急报/总攻令次数和判胜原因。

可先 `dotnet publish tools/RelayRunner/RelayRunner.csproj -c Release -r linux-x64 --self-contained false -o NEW_RUNNER`，将runner、fixtures和sdk复制到Ubuntu用户目录，用.NET10运行时执行RelayRunner.dll。C++编译见[SDK说明](../sdk/cpp/README.md)。CLI输出“FORFEIT”表示已生成有效故障结果，调用方须读取结果状态，不能用退出码0判断比赛正常完成。

## Unity裁判与回放

使用现有 run-unity-check.ps1 的 Delivery 动作，在6000.6.8f1独立源码副本验证和构建。原game/的6000.3.26f1不变。Windows Player或Linux Dedicated Server支持以下参数，路径含空格时按所在shell引用。

```text
TuringSignalRelay -batchmode -nographics
  --relay-config FULL_CONFIG --relay-map MAP
  --relay-red-ai RED_AI --relay-blue-ai BLUE_AI
  --relay-replay NEW_REPLAY --relay-result NEW_RESULT
```

去掉两个AI参数使用确定性规则脚本。AI参数必须成对提供。程序日志中必须存在 RELAY_CPP_COMPLETED，且结果显示 completed=true、forfeit=false才是正常完成。Linux原生桥接同样启用严格CPU计量。另有 NEW_RESULT.ai.json 保存运行诊断。

Windows客户端从正常“打开回放”入口加载relay.replay.json，支持全图/双方视角、时间轴与得分。正式快照已适配器材、预留、施工任务、固定中继、链路、天气、通播点与枢纽进度。异常终局显示故障原因并保留已得原始分。

## 代码入口与规则

| 文件 | 实现 |
| --- | --- |
| Integration/Ai/AiProcessBridge.cs | 长期AI进程、限长UTF-8管道、墙钟/CPU预算、迟到帧丢弃、日志截断、取消清理 |
| Integration/Ai/RelayAiSession.cs | .NET和Unity共用握手、双侧请求、三超时/崩溃判负、完整或部分回放 |
| Integration/Headless/RelayAiHeadlessHost.cs | Unity后台AI对战、主线程保存校验产物并退出 |
| Integration/RelaySeries.cs | 同种子/地图/规则检查、换边、总分→单局最高→急报次数→平局、整场违规判负 |
| Rules/RadioNetwork.cs | 双端半径取小、天气半径、BFS连通、覆盖和自动交付 |
| Rules/FullMessageSystem.cs | 急报、总攻令、密报连续译电、双点连续通播、期限与半分账本 |
| Rules/FullRuleSystem.cs | 架台、压制、器材库存、公共空投、补偿、建造预留/退款、枢纽与结余 |
| Core/RelayFullStateHash.cs | 完整附加状态规范摘要，旧功能集摘要保持兼容 |
| Editor/FullStageChecks.cs | Unity完整9001帧回放、天气、链路实体和UI渲染，以及真实AI文件加载 |

所有事件随机流由种子及事件序号决定，AI行为不消耗事件随机数。通信员架台20tick、译电50tick，断网允许继续本地译电，撤台/压制重置未完成进度。通播双目标需连续30tick覆盖。工兵架桥60tick耗3器材，中继50tick耗2器材，中断退回预留，前3桥/4中继计分。兵站每250tick加2至6，中央兵站tick3250首次刷新，空投tick1200独立4器材。终局人数/网络/器材三个枢纽条件独立计分，结余按携带加指挥所库存封顶10分。

## 验收证据与边界

机器结果见[本轮验收记录](STAGE45_VALIDATION.json)。Windows/.NET和Ubuntu/.NET实际双局的各局状态摘要一致，Windows原生和Ubuntu原生9000tick结果也一致。65项完整检查包含真实规则函数构成的附录668分账、期限、连续进度、压制恢复、建造退款、兵站归属、双局比较与违规。

本轮通过本地可信C++程序闭环。尚未完成不可信代码的强隔离、隔离编译、源码上传与账号、持久化正式评测任务、裁判基础设施故障重试及正式服务授权。现有演示平台/Worker的Bootstrap路径不自动变成完整C++生产评测。下一步按实现路线中的“隔离编译与正式Worker”推进。

Unity6000.3.26f1 API静态编译通过，原版UPM实际导入故障仍未解决；6000.6.8f1验证包属于开发构建，原基线发布验收继续保留。
