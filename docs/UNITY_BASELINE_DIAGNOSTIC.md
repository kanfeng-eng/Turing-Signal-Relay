# Unity 6000.3.26f1环境诊断

检查日期为2026-11-04。团队ProjectVersion保持6000.3.26f1。

2026-11-07阶段7再次创建全新空工程，使用-enablePackageManagerTraces及独立Editor/UPM日志复验，Editor退出1，project:list-packages及project:update-dependencies返回500。详细日志仍未给出可确认的根因。证据位于artifacts/stage7/baseline-trace，结果见[阶段7记录](STAGE7_VALIDATION.json)。当前构建继续使用获准的6000.6.8f1独立副本。

## 已复现

原版Editor在UPM解析阶段退出1，错误为The "path" argument must be of type string. Received undefined。此前6000.3.28f1也出现相同错误。许可证激活后问题继续存在。

## 本轮对照

| 对照 | 控制变量 | 结果 |
| --- | --- | --- |
| 最小子进程环境 | 保留Windows和用户目录必需环境变量 | 相同错误 |
| 显式缓存路径 | 给UPM缓存根、NPM缓存和Git LFS缓存指定新目录 | 相同错误 |
| 英文工程和工作目录 | 工程与进程工作目录同时改到英文临时路径 | 相同错误 |

实验使用隔离目录及子进程环境，未修改原工程版本、全局环境和Windows安全配置。原有项目、英文空工程、独立缓存和另一补丁的历史对照也未解除该问题。

## 状态与后续输入

根因尚未确认，当前证据将阻断定位在本机6.3工具链的UPM初始化阶段。无法据此归因于比赛源码。按investigate流程，三组路径/环境假设被否定后停止猜测性修改。

需要新的UPM异常栈、官方工具链修复或另一台机器的同版本复现结果，才能进一步缩小根因。项目继续使用获准的6000.6.8f1独立副本开发和验证，原版本导入、Play及双平台构建继续保留待验状态。

原始证据保存在忽略目录artifacts的baseline-audit工程、baseline-ascii-cwd.json及历史unity-fresh/unity-ascii日志中。最初诊断脚本的路径编码错误已纠正，表中结果均来自正确路径的重新执行。
