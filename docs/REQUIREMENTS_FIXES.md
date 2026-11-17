# 策划案要求修正

更新于2026-11-17。依据原始策划案第4章与已批准R01—R19，补齐本轮核对发现的执行、反作弊、观战和配置问题。

## 正式比赛调度与时限

Worker同时启动两局，每局使用独立AI沙箱和产物目录。规则、种子和配置在整场开始时加载，两个规则实例各持有自身状态。一局发生基础设施异常时取消并等待两局清理，再按已有整场重试规则恢复。

整场时限由9000帧、每帧100ms墙钟、两次2秒初始化和120秒宿主开销组成，完整局上限1024秒。nsjail外层增加30秒监督余量，上限1054秒。逐帧10ms CPU、100ms墙钟和累计三次超时判负继续执行。

## 源码扫描与裁判复核

提交保存和隔离编译前生成source-scan.json，记录扫描版本、源码SHA256、行号、类别及命中符号。扫描覆盖文件、网络、进程、线程、身份硬编码和固定移动坐标等提示。

裁判台展示最近20份提交的扫描结果，支持下载源码、填写复核理由和记录结论。扫描命中需由裁判结合源码与运行证据判断，注释、字符串或合法坐标也可能命中。

复核记录保存裁判身份、源码摘要、扫描结果、理由和时间。取消队伍资格会作废该队伍关联比赛成绩，阻止新提交和新比赛，已签发的相关观战快照返回410。历史结果与回放作为审计产物保留。源码“复核通过”记录描述该份源码的审核结论，队伍资格处置以队伍状态为准。

数据库新增SubmissionReviews及Teams.Disqualified，通过EF迁移保留原始数据库，兼容此前尚未登记迁移历史的初始结构。

## 事件倒计时

正式快照从同一份规则参数计算下一事件时刻，覆盖报文刷新、兵站、空投、天气、渡口、枢纽和总攻令。事件发生后切换到未来事件，终局使用-1。在线快照与离线回放使用相同生成入口。

## 参数配置与冻结

平衡参数集中于fixtures/rules/parameters-v1.json，包含任务日程、分值、时限、资源、建设、天气、视野、携带量、单位数量、移动耗时和渡口周期。正式配置relay-full-config.json嵌入参数快照。

修改参数后生成绑定摘要的新配置，在仓库根目录运行以下命令，输出文件使用新路径。

```powershell
& 'D:\kanfe\dotnet10\dotnet.exe' run --project tools/RelayRunner/RelayRunner.csproj -- configure fixtures/protocol/relay-full-config.json fixtures/rules/parameters-v1.json artifacts/NEW_CONFIG.json
```

输入校验限制数值、任务ID范围、半分精度和单位ID范围。参数摘要进入状态哈希，回放保存整份配置并在重演时核验摘要。引擎对配置深拷贝，已开始的比赛使用其配置快照。测试赛可为后续比赛加载新配置，正式参数调整继续按规则审批与冻结流程执行。

旧回放和批准默认值继续使用原始状态摘要，缺少参数字段的旧记录采用批准默认值。地图坐标位于版本化地图fixture，协议版本、类型和标识范围由协议约束。

## 复验入口

```powershell
& 'D:\kanfe\dotnet10\dotnet.exe' run --project tools/RelayRunner/RelayRunner.csproj -- full-check "$PWD" "$PWD/artifacts/NEW_RULE_CHECK"
& 'D:\kanfe\dotnet10\dotnet.exe' run --project server/Worker.Checks/Worker.Checks.csproj -- artifacts/NEW_WORKER_CHECK
python tools/check-platform.py --url http://111.231.101.147 --credentials artifacts/stage5/initial-judge.json --output artifacts/NEW_PLATFORM_CHECK --requirements
python tools/check-platform.py --url http://111.231.101.147 --credentials artifacts/stage5/initial-judge.json --output artifacts/NEW_SLOW_CHECK --requirements --slow
```

requirements检查会创建专用测试队伍，并对其中的队伍执行成绩作废测试。slow检查使用每帧等待50ms的策略，验证超过原总时限后仍可完成9000tick。回归保留76项原完整规则检查，并额外执行可配置参数的9000tick重演、配置篡改拒绝、迁移和复核检查。

当前Unity版本统一为6000.6.8f1。原6.3工具链诊断保留在历史记录中，干净Ubuntu从零安装继续列为发布待验工作。
