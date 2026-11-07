# 网页端 ChatGPT 与 Desktop Commander Remote 交接提示词

以下正文可直接复制到新对话。交接核对日期为2026-11-07，后续执行前重新检查工作区和服务现场。

---

你要接手“电波接力 · Turing Signal Relay”项目，通过Desktop Commander Remote访问我的开发电脑，继续开发、复验和维护。5号正式平台已在当前Ubuntu通过本轮验收，先读取现有代码与记录，按剩余发布门槛推进。每完成一项用实际退出码、结果文件、状态哈希或服务状态验证。需要我处理登录或提供连接信息时再联系我。

最新一轮已补齐⑤完整玩法与⑥正式沙箱。先读docs/STAGES_5_6.md与docs/STAGE56_VALIDATION.json。台站缓存与预备沙箱回收两项回归均已先复现再修复，完整规则76项、正式扩展13项及SIGKILL自动恢复通过。下面保留此前平台验收路径作为历史证据。

## 一、已经确认的要求

- 当前由我个人承担原1—5号全部开发，编号表示模块。
- 游戏为C# + Unity、2D俯视、仅AI控制单位，支持比赛、观战与回放。
- 规则采用signal-relay-1.0，R01—R18全部选A，完整模式为relay-full-v1、协议2。R19另行确认只能取己方兵站和中央公共器材。
- Unity工程基线为6000.3.26f1。6000.6.8f1用于独立副本开发与验证，原基线的UPM导入问题仍影响正式基线发布。
- **正式裁判宿主已经选定.NET10。** Worker直接编译引用Unity工程的同一份纯C#规则源码。此前实测结果哈希与Unity原生裁判一致。我已明确选择此方案。
- 平台采用ASP.NET Core Identity、EF Core10、SQLite、Razor Pages，账号由裁判统一创建队伍和选手。
- 本轮通过SSH隧道访问127.0.0.1的5188端口。
- 已授权在本项目范围安装nsjail依赖、创建低权限服务账号、配置cgroup和systemd。服务器上还有其他业务，操作范围限定在本项目。
- 旧代码与素材归我所有。新增模块先查GitHub可复用实现，固定版本、许可和摘要，并记录采用理由。

工程原则是先说明关键假设，遇到实质歧义再集中确认，复用已有代码，做与任务相关的最小修改，完成前运行相应验证。

## 二、首先定位正确工作区

本机为Windows，仓库路径为以下位置。

```text
C:\Users\kanfe\Desktop\图灵杯
```

Git远程与分支如下。

```text
https://github.com/kanfeng-eng/Turing-Signal-Relay
feat/1-bootstrap
```

5号实现之前的已提交基点为下面的提交。本轮交付沿用同一分支，实际最新提交通过git log和远程核对。

```text
896da1ca045e896137ddc1687b743f1305230a55
Implement C++ AI match loop and full relay gameplay rules
```

**先核对本机工作区与远程分支最新状态。** 5号新实现及验收记录随本轮提交交付。若本机仍有改动，先审阅并保留。私有凭据、验证产物、旧模型和原策划案位于本机，使用当前工作区可直接复验。

先查看Desktop Commander Remote实际提供的工具和连接设备，确认操作系统与上述路径。若连接到的是Linux容器，应先核实存放本机工作区的设备连接。

在Windows用PowerShell核对现场。

```powershell
Set-Location -LiteralPath 'C:\Users\kanfe\Desktop\图灵杯'
git status --short
git branch --show-current
git log -1 --format='%H %s'
Get-Content README.md
Get-Content docs/PLATFORM.md
Get-Content deploy/README.md
```

本地还有未跟踪的原始策划案DOCX。工作区内的artifacts、旧工程和完整模型属于本地资料或验证产物。提交时按明确文件路径选择改动，保留这些资料。

## 三、当前代码地图

| 位置 | 已实现内容 |
| --- | --- |
| game/Assets/Game/Core、World、Rules | 确定性状态、地图移动、完整规则、计分与摘要 |
| game/Assets/Game/Integration/Ai/AiProcessBridge.cs | 长期进程、受限管道、CPU/墙钟预算、cgroup计量与清理回调 |
| game/Assets/Game/Integration/Ai/RelayAiSession.cs | 双AI握手、逐tick交互、超时/故障、有效动作与部分回放 |
| server/Evaluation/ContestData.cs | Identity用户、队伍、提交、比赛、产物索引、上传事务及幂等比赛请求 |
| server/Evaluation/ContestDatabase.cs、Migrations | 初始迁移、旧初始库结构核对后登记迁移历史 |
| server/Evaluation/SandboxClient.cs | 固定helper协议、隔离编译、运行准备、取消与回收 |
| server/Evaluation/EvaluationProcessor.cs | 持久化队列、单机正式双局、结果/回放校验、快照与重启恢复 |
| server/Worker/EvaluationWorker.cs | 单实例锁、SIGTERM处理、遗留运行回收、持续消费任务 |
| server/Platform/ContestApi.cs | 登录会话、CSRF、归属检查、提交、比赛、下载、日志与观战令牌 |
| server/Platform/Pages | 登录、提交、历史、裁判、结果与退出页面 |
| deploy/sandbox | 固定nsjail构建、Root拥有的受限helper、编译/运行策略与安装脚本 |
| deploy/systemd、deploy/install-services.sh | 平台和Worker服务、版本目录及切换 |
| deploy/backup.py | SQLite与文件备份、逐文件摘要、恢复到新目录及编译缓存恢复 |
| tools/check-platform.py | HTTP权限与真实上传到双局结果的检查 |
| tools/check-sandbox.py、fixtures/sandbox | 真实隔离、违规、CPU计量、编译配额与清理检查 |
| tools/check-platform-operations.py | 文件违规判负、三次CPU超时判负、运行中重启恢复检查 |
| tools/check-platform-queue.py | 同一比赛请求幂等/冲突及同队6路并发上传限额 |

原server/Platform/Pages/DemoPageModel.cs已删除，页面已接真实服务。旧Bootstrap调度入口仍保留用于回归，正式评测入口为Worker.dll serve。

## 四、已经验证到哪一步

请区分已经验证的证据和待收尾检查。

| 项目 | 当前证据 |
| --- | --- |
| 旧地图/平报回归 | 52项检查，原9000tick状态摘要保持一致 |
| 完整规则 | 当前76项检查，包含台站缓存、整局任务表及附录668分账 |
| 可信本地AI管道 | Windows9项进程检查，含崩溃、畸形、超长、超时与清理 |
| 旧Worker | 24项子进程回归通过 |
| 真实平台全流程 | 当前部署已通过HTTP上传、隔离编译、双局9000tick、越权、防伪和产物摘要检查 |
| 沙箱 | Ubuntu14项检查通过，包含文件、网络、进程/线程、停滞、内存、输出、CPU计量、编译内存/时间和取消清理 |
| 数据库迁移 | 新库迁移及重复运行通过，服务器已有初始库已登记初始迁移并保留数据 |
| 备份恢复 | 37文件旧库备份恢复至独立目录，迁移、7个账号、5份源码和3份产物及登录/下载通过；发布演练另生成163文件备份 |
| 故障与重启 | 文件违规、三次CPU超时、最终快照、日志归属、运行中重启后第二次尝试通过 |
| 并发与幂等 | 6路同队上传接受5份、拒绝1份，同一请求重复和冲突拒绝通过 |
| Unity真实平台 | 4项授权快照通过，正常双局和两种故障终局，拒绝无效令牌及重复帧 |
| 发布与回滚 | 同一构建切换新版本目录再回滚通过，两次health及数据库计数一致 |
| 当前部署 | 两个服务active，health为200，空闲时运行cgroup已回收 |

本地主要证据路径如下。

```text
artifacts/stage5/http-auth-second/CHECKS.json
artifacts/stage5/http-full-second/CHECKS.json
artifacts/stage5/http-full-second/result.json
artifacts/stage5/http-full-second/replay-1.json
artifacts/stage5/http-full-second/replay-2.json
artifacts/stage5/http-final/CHECKS.json
artifacts/stage5/http-operations-final2/CHECKS.json
artifacts/stage5/http-delivery/CHECKS.json
artifacts/stage5/operations-delivery/CHECKS.json
artifacts/stage5/queue-final/CHECKS.json
artifacts/stage5/queue-delivery/CHECKS.json
artifacts/stage5/restore-stage5-checks.json
artifacts/stage5/deploy-cycle-stage5-checks.json
artifacts/stage5/observer-platform-unity.log
artifacts/stage5/observer-platform-delivery.log
artifacts/stage5/observer-network-final.json
artifacts/stage5/sandbox-budget-checks.json
artifacts/stage5/legacy-worker-checks/CHECKS.json
artifacts/stage5/full-regression/relay.result.json
artifacts/stage5/migration-fresh.log
artifacts/stage5/migration-repeat.log
artifacts/stage5/package-audit.log
artifacts/stage5/build-candidate.log
artifacts/stage5/reviewed/
artifacts/stage5/final-publish/
```

服务器上的14项沙箱报告位于下面的位置。

```text
/var/lib/tsr/data/security-checks-budget-proof/CHECKS.json
/home/ubuntu/turing-signal-relay/budget-proof-stage5.log
```

两局正常样例的状态摘要如下，可与Unity原生裁判已有记录对照。

```text
game-1 be0e9ecde5dcc9b8235d2db4cf846d1a93067be7c1dfa382a4bbf2f6d7edcb2e
game-2 545b070bbe9a311db7b3c8ea87fd346b182b357822e9396ec20edea8ca232870
```

最新部署产物位于artifacts/stage56/publish/platform及artifacts/stage56/worker-recovery-publish，较早reviewed和final-publish保留作对照。最新汇总见docs/STAGE56_VALIDATION.json，此前平台记录见docs/PLATFORM_VALIDATION.json。目标Ubuntu现场已验证，干净主机从零安装与原Unity版本导入仍待验收。三个工程已声明linux-x64目标，默认Windows开发还原和Linux发布还原均通过锁文件模式。

## 五、下一步按此顺序推进

### 1. 核对现场与版本

读取git diff并核对远程分支，查看服务、当前发布软链接及数据库中的排队/运行任务。参考本轮已通过的验收记录判断是否需要复验。长任务用进程状态、日志和完成标志跟踪。

### 2. 有相关修改时按范围复验

构建与HTTP检查输出使用新目录。以下均已通过，后续相关修改后继续覆盖。

- 同一requestKey与相同参数返回同一比赛，不同参数复用该key会被拒绝。
- 同队并发上传遵守最多5个待编译任务的事务约束。
- 运行取消及重启后，cgroup与私有运行目录可回收。
- 故障结束时，snapshot保存最终tick与forfeit状态。
- 本队可下载本方运行日志，另一阵营日志的越权访问被拒绝。
- 结果页展示选手故障原因，监督器基础设施错误进入重试流程。

### 3. 故障与重启恢复的复验方法

tools/check-platform-operations.py已通过本轮实测。它先提交文件违规与25ms CPU样例，核对整场判负，再发起正常比赛。

先建立本机5188端口的SSH隧道，再从仓库根目录运行。输出目录使用新名字，凭据文件只供脚本读取。

```powershell
python tools/check-platform-operations.py --url http://127.0.0.1:5188 --credentials artifacts/stage5/initial-judge.json --previous-checks artifacts/stage5/http-delivery/CHECKS.json --output artifacts/NEW_OPERATIONS_CHECK
```

当脚本输出WORKER_RESTART_REQUIRED时，在Ubuntu对tsr-worker执行一次受控重启，然后继续等待。成功标准为该比赛attempts等于2、两局9000tick完成、结果与回放一致、运行资源已回收。

沙箱脚本的cpu-budget-sample证明cgroup实际计量，operations已验证正式链路累计三次超时判负。

### 4. 备份恢复与发布演练记录

已有备份为/var/lib/tsr/backups/stage5-before-migration。恢复测试使用新的数据目录，核对数据库完整性、账号登录、源码与结果下载摘要。旧备份保留了初始库结构，也能用于验证迁移兼容性。

恢复目录/var/lib/tsr/restore-check-stage5已验证，临时tsr-restore-check服务已停止。正式服务继续使用原数据目录。另有/var/lib/tsr/backups/stage5-deploy-cycle备份。已演练同一构建的版本目录发布与回滚，保留新旧发布目录。恢复与切换接口以deploy/backup.py和部署手册为准。

### 5. 剩余发布门槛

- 保持Unity6000.3.26f1基线，继续定位UPM故障并取得原版本正常导入与构建证据。独立副本验证结果见本轮记录。
- 获得独立干净Ubuntu环境后，按部署脚本从零复现安装与完整评测。本轮已验证的是现有目标服务器。
- 后续功能修改后运行对应检查，维护PLATFORM_VALIDATION.json、复用记录和进度。提交前核对凭据排除情况。

新增工作沿用feat/1-bootstrap，提交前逐项确认文件范围，推送后核对远程提交。当前账号仍由裁判统一创建，访问继续使用SSH隧道。

## 六、命令与运行环境

Windows本机.NET10 SDK路径为D:\kanfe\dotnet10\dotnet.exe。PowerShell调用示例如下。

```powershell
$relayDotnet = 'D:\kanfe\dotnet10\dotnet.exe'
& $relayDotnet build server/Platform/Platform.csproj --nologo
& $relayDotnet build server/Worker.Checks/Worker.Checks.csproj --nologo
& $relayDotnet run --project server/Worker.Checks/Worker.Checks.csproj -- artifacts/NEW_WORKER_CHECK
& $relayDotnet run --project tools/RelayRunner/RelayRunner.csproj -- full-check "$PWD" "$PWD/artifacts/NEW_FULL_CHECK"
python tools/verify-source.py --output artifacts/NEW_SOURCE_REPORT.json
git diff --check
```

各输出目录和报告路径应使用新名字。Windows使用python，Ubuntu使用python3。当前源码新增EF迁移，包版本为10.0.14并有锁文件。迁移入口为Platform.dll migrate。

Unity基线Editor位于D:\kanfe\Unity\6000.3.26f1\Editor，新版验证Editor位于C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor。运行新版验证时使用tools/run-unity-check.ps1的DevelopmentCopy选项。

## 七、Ubuntu现场

SSH地址为111.231.101.147，端口22，用户名ubuntu。已核验的主机密钥指纹如下，连接时核对。

```text
SHA256:ttMqPMab4v0HGjOmFAR4zkDgDXaJLmoxufYhvQ/3HgM
```

SSH密码由用户通过安全渠道提供。本机初始裁判账号文件为artifacts/stage5/initial-judge.json，读取时仅用于授权登录，公开文档和提交中保留路径即可。

| 位置 | 当前用途 |
| --- | --- |
| /opt/tsr/current | 当前发布软链接，交接核对指向/opt/tsr/releases/20261107T120200Z |
| /opt/tsr/current/platform/Platform.dll | 平台 |
| /opt/tsr/current/worker/Worker.dll serve | 正式Worker |
| /opt/tsr/dotnet/dotnet | 服务器.NET10运行时 |
| /var/lib/tsr/data | SQLite、源码、诊断、比赛与密钥 |
| /var/lib/tsr-sandbox | 只读AI缓存与私有运行目录 |
| /sys/fs/cgroup/tsr | 项目资源组 |
| /usr/local/libexec/tsr-sandbox | Root拥有的受限helper |
| /opt/tsr/sandbox | nsjail、固定SDK与策略 |
| /etc/tsr/contest.env | 服务数据与源码根目录配置 |

tsr-platform与tsr-worker使用tsr-data组。SQLite及WAL/SHM需要0660，数据目录2770，密钥目录0700。部署中曾发现SQLite默认0640影响worker写入，现已修复并在代码/安装脚本中处理。

当前服务器三个主DLL的SHA256与本机artifacts/stage56/publish/platform及artifacts/stage56/worker-recovery-publish对应文件一致。

```text
Platform.dll   f7359bcd19f97d3dc65de09808c006b5a336aef7022f5091e4eee07d3ad53c4c
Evaluation.dll 7691490027fd1a96841ab51bcdcc3aab6aa0f9cd54174db4b08fdde4447adebb
Worker.dll     e39ba43a198340b662c6dfdbce1611f538e4528b51f9998e63cbe5ffdc94724e
```

先运行只读现场检查。

```bash
readlink -f /opt/tsr/current
sudo systemctl is-active tsr-platform tsr-worker
curl --fail http://127.0.0.1:5188/health
sudo journalctl -u tsr-worker -n 30 --no-pager
sudo find /sys/fs/cgroup/tsr -mindepth 1 -maxdepth 1 -type d
```

本机旧对话曾通过临时SSH转发访问53268端口。接手时建立自己的连接与隧道，服务固定端口为5188。本机artifacts/ssh_tsr_session.py是带getpass的私有辅助脚本，可按工具能力使用。

## 八、交付时的汇报

请简明说明实际完成的功能、验证证据、提交与部署版本，以及仍影响交付的具体问题。遇到失败先定位原因并修复，再继续验证。最终验收同时覆盖正常比赛、选手故障、基础设施恢复和账号/队伍权限。
