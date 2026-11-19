# ⑤完整玩法与⑥正式沙箱

本页对应用户本轮编号。⑤为完整玩法，对应实现路线的批次4；⑥为隔离编译与正式Worker，对应批次5。平台、数据库与部署沿用已经接通的实现。机器结果见[本轮验收记录](STAGE56_VALIDATION.json)。

## ⑤完整玩法

规则采用已批准的signal-relay-1.0及R19，功能集relay-full-v1。已有通信网络、五类任务、器材与建设、压制、天气、补偿、枢纽和双局计分继续使用同一份纯C#源码。

本轮补齐台站缓存边界。原实现查找缓存持有节点时要求节点处于active，导致受压制节点的缓存丢失持有关系；未连接节点的缓存也可能再次经过地面收集逻辑，被附近覆盖节点转发。现在缓存持有关系在节点存在期间保留，仅由有效且已连通的持有节点发送。未译密报在压制解除后重新累计50 tick。

FullChecks中新增四项缓存回归。修复前明确出现FULL_RULES_FAIL，修复后通过。另增加整局任务数量、种子旋转配对、通播双目标及库存上限检查。完整检查共76项，原52项、附录668分账、9000 tick脚本及C++双局继续通过。

```powershell
./tools/check-local-match.ps1 -Dotnet 'D:\kanfe\dotnet10\dotnet.exe' -OutputDirectory "$PWD/artifacts/NEW_FULL_GAME"
```

输出必须包含LOCAL_MATCH_CHECKS_PASSED。正常双局各9000 tick，结果与回放可再次重演。Windows本地程序用于可信样例验证，正式资源限制在Ubuntu检查。

## ⑥正式沙箱

正式编译与运行继续采用已固定版本的nsjail、两份seccomp策略、低权限身份、命名空间及cgroup v2。源码256 KiB，编译30秒/512 MiB，运行256 MiB/单进程，逐tick 10ms CPU及100ms墙钟，累计三次超时判整场负。

内存违规样例改为每次分配4 MiB并逐页写入，使驻留量实际增长。实测峰值265199616字节，约252.9 MiB，随后分配失败并退出86。该次未触发oom_kill，测试核对memory.max，报告保留memory.peak与memory.events。配置核对包含256 MiB内存、零交换和单进程上限。资源统计语义参考内核官方文档。[cgroup v2文档](https://docs.kernel.org/admin-guide/cgroup-v2.html)

Worker启动时改从Root管理的runs目录回收项目沙箱。旧逻辑依赖比赛目录中的启动脚本，预备沙箱创建后、启动脚本写入前中断会留下资源。本轮先构造该状态复现遗留，再验证新Worker会删除运行目录与cgroup。

在空闲服务器上可运行独立回归。参数为已就绪提交ID和新的报告路径，脚本会检查队列空闲并重启项目Worker。

```bash
sudo bash tools/check-worker-prepared-recovery.sh READY_SUBMISSION_ID NEW_REPORT_JSON
```

完整沙箱检查继续使用以下入口，输出必须出现SANDBOX_CHECKS_PASSED。

```bash
sudo -u tsr-worker python3 tools/check-sandbox.py --output NEW_OUTPUT --stress
```

## 正式链路扩展验收

平台检查新增extended模式，覆盖文件访问、联网、进程、线程、墙钟超时、CPU超时、内存增长、输出超限、畸形JSON、未来tick、动作数超限，以及双方同时违规。验证实际整场判负、最终快照、日志归属与回放，最后进行一次正常比赛的中断恢复。

```text
python tools/check-platform-operations.py --url http://127.0.0.1:5188 --credentials PRIVATE_JUDGE_JSON --previous-checks PREVIOUS_FULL_CHECKS_JSON --extended --output NEW_OPERATIONS_OUTPUT
```

出现WORKER_RESTART_REQUIRED后才执行一次受控重启或崩溃注入。本轮使用SIGKILL结束Worker主进程，由systemd自动恢复，再验证第二次尝试正常完成双局。平台、恢复及源码版本摘要以机器验收记录为准。

## 发布边界

Unity工程继续保持6000.3.26f1，本轮实际导入在6000.6.8f1独立源码副本验证。原版本UPM导入和干净Ubuntu主机从零部署仍是整体发布门槛。部署、回滚与备份操作见[部署手册](../deploy/README.md)。
