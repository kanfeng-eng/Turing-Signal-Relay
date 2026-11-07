# 5号模块使用与开发

平台采用ASP.NET Core Identity、EF Core 10和SQLite，账号由裁判创建。独立Worker执行隔离编译与双局比赛，网页请求保存任务后立即返回。Ubuntu服务监听127.0.0.1的5188端口，通过SSH隧道访问。

2026-11-07用户已确认正式裁判宿主采用.NET10，直接引用Unity工程同一份纯C#规则源码。当前平台实现已部署，正常双局、故障判负、重启恢复、并发限额与备份恢复均已通过实测，摘要见[平台验收记录](PLATFORM_VALIDATION.json)。接手入口见[交接提示词](HANDOFF_CHATGPT_DESKTOP_COMMANDER.md)。

⑤完整玩法与⑥正式沙箱补齐后的当前部署版本及扩展验收见[最新记录](STAGE56_VALIDATION.json)。此前平台验收文件保留原构建与测试现场。

阶段7已使用完整交付服务包更新部署，并再次通过真实上传至双局结果检查。当前发布目录及主DLL摘要见[阶段7记录](STAGE7_VALIDATION.json)，此前记录保留各自验证时的版本。

## 使用顺序

1. 在本机终端建立SSH隧道，保持终端运行。服务器地址与登录用户使用自己的部署配置。

```text
ssh -N -L 5188:127.0.0.1:5188 ubuntu@YOUR_SERVER
```

2. 打开 http://127.0.0.1:5188/Login ，使用初始化裁判账号登录。裁判台可以创建队伍及选手账号，同一队伍名称的账号共享提交。
3. 用选手账号上传UTF-8编码的ai.cpp，最大256 KiB。可以直接将sdk/cpp/examples/courier.cpp或transporter.cpp复制为ai.cpp，编译环境已经提供SDK与示例辅助头文件。
4. 提交历史显示等待编译、正在编译、可参加比赛或编译失败。每队最多同时存在5个等待或正在编译的提交。
5. 裁判选择两支不同队伍已编译的提交和随机种子，发起双局。A队首局红方、次局蓝方，两局使用同地图、种子和批准规则。
6. 结果页提供总分、双局得分、判胜依据、选手故障原因、JSON结果、回放和本队运行日志。裁判可以访问双方运行日志。
7. 结果页生成一小时有效的本场观战令牌。将快照URL和令牌填入Unity观战界面。第一局使用默认URL，第二局在URL后添加?game=2。令牌用于该场的只读快照。

比赛完成与选手故障分别记录。选手故障会保留已得分和部分回放，双局胜负遵循整场判负规则。基础设施故障最多重试一次，第二次失败显示需要裁判处理。

## 实现入口

| 目录或文件 | 内容 |
| --- | --- |
| server/Evaluation/ContestData.cs | Identity用户、队伍、提交、比赛、产物索引、上传与幂等发起操作 |
| server/Evaluation/ContestDatabase.cs、Migrations | 初始迁移、已有初始数据库的结构核对和升级 |
| server/Evaluation/SandboxClient.cs | 固定特权helper调用、编译摘要验证、运行准备、取消和清理 |
| server/Evaluation/EvaluationProcessor.cs | 持久化任务领取、编译状态、正式双局、可信回放校验、结果落库和重启恢复 |
| server/Worker/EvaluationWorker.cs | 单实例Worker、SIGTERM取消、遗留运行回收及持续处理任务 |
| server/Platform/ContestApi.cs | 选手/裁判权限、队伍归属、防伪验证、源码/结果/日志下载和观战令牌 |
| server/Platform/Pages | 登录、提交、历史、裁判台、结果及退出页面 |
| deploy/sandbox | 固定nsjail构建、编译/运行两份系统调用策略、受限特权helper和安装脚本 |
| deploy/systemd、deploy/backup.py | 两个服务、备份、校验和恢复到新目录 |

Windows可构建和开发页面，正式Worker运行在已安装沙箱的Ubuntu上。工程使用.NET 10 SDK，依赖版本与内容摘要由packages.lock.json固定。

```powershell
dotnet restore server/Platform/Platform.csproj --locked-mode
dotnet build server/Platform/Platform.csproj
dotnet build server/Worker/Worker.csproj
$env:TSR_DATA = "$PWD/runtime-data/contest"
dotnet run --project server/Platform -- migrate
dotnet run --project server/Platform -- create-judge judge
dotnet run --project server/Platform -- --urls http://127.0.0.1:5188
```

create-judge从标准输入读取初始密码的一行，要求至少12字符并包含大小写字母、数字与符号。数据库与密钥位于TSR_DATA，默认runtime-data/contest。CLI和网页共享同一数据库配置。已有EnsureCreated初始库会先与初始迁移的完整结构比较，一致后登记迁移历史，保留已有数据。

Ubuntu部署步骤见[部署手册](../deploy/README.md)。完整数据模式为relay-full-v1，旧Bootstrap调度CLI继续作为进程回归入口。

## API契约

以下接口使用登录Cookie。写接口同时携带GET /api/csrf取得的防伪令牌，通过X-CSRF-TOKEN请求头提交。源码提交采用multipart/form-data，字段source与note。

| 方法与路径 | 权限和结果 |
| --- | --- |
| GET /api/submissions | 本队最近100个提交，裁判可查全部队伍 |
| POST /api/submissions | 本队提交ai.cpp，返回202及提交ID |
| GET /api/submissions/{id} | 本队或裁判查询编译状态与诊断 |
| GET /api/submissions/{id}/source | 本队或裁判下载经摘要核验的源码 |
| POST /api/matches | 裁判发起，JSON字段teamASubmissionId、teamBSubmissionId、seed、requestKey |
| GET /api/matches | 有权限的最近100场比赛 |
| GET /api/matches/{id} | 比赛状态、尝试次数、正常或故障结果 |
| GET /api/matches/{id}/artifacts | 结果与回放的名称、字节数及SHA256 |
| GET /api/matches/{id}/artifacts/{name} | 下载result、replay-1或replay-2，读取时核验摘要 |
| GET /api/matches/{id}/logs/{game}/{side} | game为1/2，side为0/1，选手仅能读取自己的阵营日志 |
| POST /api/matches/{id}/spectator-token | 本场相关队伍或裁判生成有效期3600秒的只读令牌 |
| GET /api/matches/{id}/snapshot?game=1 | 本场相关队伍/裁判Cookie，或Authorization头中的Bearer观战令牌 |

requestKey使用32位十六进制GUID。同一key和相同参数返回同一比赛，不同参数复用同一key会被拒绝。队伍间源码、编译诊断和运行日志分别检查归属。

快照使用已有ObserverSnapshotResponse协议1，包含真实tick、status和snapshot。未开始时返回202，有状态后返回200。status为running、completed或forfeit，故障结束也保存最终tick。网页与API中比赛completed表示评测产物已完成，胜负与违规细节在series.result中。

## 隔离与配额

运行环境固定为静态x86_64 ELF，只读文件系统中提供本方程序。nsjail创建网络、挂载、PID、IPC、UTS及cgroup命名空间，使用65534低权限身份，seccomp限制系统调用。普通文件打开、联网、创建线程和子进程触发拒绝。静态运行库启动所需的内存、信号和元数据调用由运行策略列出。

| 项目 | 参数和执行位置 |
| --- | --- |
| 源码 | 256 KiB，Web和特权helper分别校验 |
| 编译 | 30秒、512 MiB、64进程、单文件16 MiB，私有临时目录 |
| 编译命令 | GCC 13，C++17、O2、pipe、static及固定SDK包含路径，精确版本保存在编译记录 |
| 运行 | 每方独立cgroup，256 MiB、1进程，独立只读根目录 |
| 单tick | 10ms CPU与100ms墙钟，累计三次超时判整场负 |
| CPU统计 | cgroup v2的usage_usec，统计实际沙箱子进程，排除sudo和nsjail监督器 |
| 初始化 | 每方2秒，比赛内使用长期进程 |
| 协议 | 输入2 MiB，stdout单行64 KiB、128动作，stderr全局最多保存1 MiB |
| 清理 | cgroup.kill终止沙箱成员，回收监督器，再删除运行cgroup与私有根目录 |

nsjail日志保存在Root拥有的专用审计文件中，AI stderr另行保存。监督器基础设施错误会走重试流程。正式Worker每次启动会回收已有运行目录关联的遗留cgroup，再恢复数据库中未完成的任务。首次编译和重试的产物分别保留。

## 验证

```text
python tools/check-platform.py --url http://127.0.0.1:5188 --credentials PRIVATE_JUDGE_JSON --output NEW_OUTPUT
sudo -u tsr-worker python3 tools/check-sandbox.py --output NEW_OUTPUT --stress
```

PRIVATE_JUDGE_JSON含username和password，仅在本机私有位置保存。平台检查会创建专用测试队伍，执行真实上传、隔离编译、双局、越权与产物摘要检查。沙箱检查会运行受限违规样例和编译压力测试。

tools/check-platform-operations.py用于验证文件违规、三次CPU超时及Worker重启恢复。它输出WORKER_RESTART_REQUIRED后，在服务器执行一次systemctl restart tsr-worker，再等待检查完成。本轮三项均通过，正常双局及恢复后的双局状态摘要一致。

追加--extended可执行11种单方违规、双方同时违规和恢复检查，共13项。运行边界、内存驻留压力及预备沙箱回收的最新结果见[⑤⑥验收](STAGE56_VALIDATION.json)。

tools/check-platform-queue.py验证同一请求的幂等与冲突拒绝、同队6路上传仅允许5个待编译任务。执行前等待队列空闲并暂停Worker，结束后恢复Worker。

```text
python tools/check-platform-operations.py --url http://127.0.0.1:5188 --credentials PRIVATE_JUDGE_JSON --previous-checks PREVIOUS_FULL_CHECKS_JSON --output NEW_OPERATIONS_OUTPUT
python tools/check-platform-queue.py --url http://127.0.0.1:5188 --credentials PRIVATE_JUDGE_JSON --previous-checks PREVIOUS_FULL_CHECKS_JSON --output NEW_QUEUE_OUTPUT
```

本轮已将旧初始数据库备份恢复到独立目录，核对账号、源码、比赛与产物记录，完成迁移兼容、裁判登录及逐项下载摘要检查。临时恢复测试服务已停止，正式服务继续使用原数据目录。完整证据与当前部署主程序摘要见[平台验收记录](PLATFORM_VALIDATION.json)。

Unity原基线6000.3.26f1的UPM导入故障仍影响该基线的发布验收。当前Windows观战开发构建与Ubuntu正式评测的集成采用已验证的协议和共享规则代码。
