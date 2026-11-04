# 1号与4号联合进度

更新于2026-11-04，分支`feat/1-bootstrap`，见[草稿PR #1](https://github.com/kanfeng-eng/Turing-Signal-Relay/pull/1)。用户同时承担1号和4号。

## 当前交付

| 任务 | 已实现和验证 | 继续完成所需输入 |
| --- | --- | --- |
| 1-A 工程 | Bootstrap/Observer场景、预制体、7个asmdef、Windows/Linux构建入口 | 全员在团队版本独立拉取验收 |
| 1-B 共享模型 | Bootstrap配置、时钟快照、Observer展示帧及格式验证 | 2/3/5号冻结正式动作、坐标、事件与协议 |
| 1-C 组合推进 | 10Hz固定时钟、暂停/单步、结束停止、9000 tick | 2号World、3号Rules及批准后的R15顺序 |
| 1-D 文件 | Bootstrap v2 JSONL、逐帧SHA256、结果摘要、v1读取；4号查看器读取9001帧 | 正式动作/事件审计回放 |
| 1-E 双平台 | Windows客户端、Linux Dedicated Server；Ubuntu实际运行通过 | 团队6000.3.26f1完整构建和运行复验 |
| 1-F 调度 | .NET10可信裁判双进程调度、超时回收、产物摘要校验、目录占用保护 | 5号MatchJob、AI桥和沙箱适配 |
| 4号表现 | 原生uGUI地图、链路、掩码、镜头、面板、曲线、小地图、离线回放与在线轮询 | 正式地图/规则帧、授权观战接口 |
| 4号网页 | Razor提交、历史、裁判和结果4页，演示数据明确标注，后台操作禁用 | 5号账号、提交、任务和结果服务 |

MatchEngine当前推进时钟与零分状态。正式AI动作、移动、交报、计分和双局换边仍需其他模块接入。

## 版本与实现选择

- 团队基线保持`6000.3.26f1`，ProjectVersion.txt与既有提交相同。`6000.6.8f1`仅用于脚本创建的独立副本。
- 本机6000.3.26f1与6000.3.28f1在UPM阶段报HTTP500，纯英文路径、独立缓存和空工程均复现。6.6副本通过。具体工具链根因仍待定位。
- 原版Unity API静态编译通过。uGUI使用固定2.0.0包，静态检查引用6.6副本中导入的同包UI程序集，原版实际兼容性待验。
- 观战使用原生Canvas、RawImage/Image、Text和MaskableGraphic。地图以纹理绘制，布局用C#维护。
- 从旧目录选取13个FBX，重新配置材质并生成256×256透明精灵。素材许可待补，模型、材质和派生贴图保留本地。公开代码无素材时使用基础标记。

## 实际验证

| 检查 | 结果 |
| --- | --- |
| 纯C#核心 | 固定时钟、暂停/单步、快照隔离、9000次回调、独立哈希参考值通过 |
| 6.6副本导入 | 编译、场景加载、v1/v2回放、非法/损坏回放拒绝通过 |
| 原生UI | 14个按钮、3个输入框、时间轴、地图纹理初始化通过 |
| 公开源码副本 | 缺少私有旧素材，sprites=0时仍通过UI和9001帧检查 |
| Windows真实进程 | 默认/重复9000 tick、配置3 tick、7项参数和文件保护检查通过 |
| Ubuntu真实进程 | Ubuntu24.04.4 x86_64，实际执行同10项检查通过 |
| 跨平台哈希 | 最终状态SHA256均为`47683abf831ae7c667b7b3f7f657355ba55fc8da133145ba98bccc4f9b073423` |
| .NET调度器 | 两局独立成功、异常退出、缺结果、超时回收、同目录并发占用保护通过 |
| Razor页面 | 编译与Ubuntu四页/CSS HTTP200通过，响应HTML在1280/375宽度离线渲染和布局检查通过 |

原生窗口操作记录及最终证据路径见[观战交接](../../OBSERVER.md)和[机器验证记录](VALIDATION.json)。平台布局检查使用Ubuntu实际响应的HTML与内联CSS，在线后台流程尚待5号接入。Windows本地网页预览启动曾被自动审批以`blocked by policy`拒绝。

## 复现

```powershell
./tools/verify-bootstrap.ps1 -Compiler 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
./tools/run-unity-check.ps1 -EditorDirectory 'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor' -DevelopmentCopy -Action Windows
./tools/run-unity-check.ps1 -EditorDirectory 'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor' -DevelopmentCopy -Action LinuxServer
./tools/verify-unity-api.ps1 -EditorDirectory 'D:\kanfe\Unity\6000.3.26f1\Editor' -UiAssembly '<独立副本>\game\Library\ScriptAssemblies\UnityEngine.UI.dll'
```

对生成的客户端或Ubuntu裁判执行以下检查，output需为新目录。

```text
python tools/check-bootstrap-headless.py --player <裁判可执行文件> --output <新检查目录>
```

Ubuntu使用`python3`。服务端运行方式见[Worker说明](../../../server/Worker/README.md)，观战操作与页面入口见[观战交接](../../OBSERVER.md)。

Ubuntu验证目录位于用户目录`turing-signal-relay/validation-20261104`，运行时安装在该目录。HTTP检查绑定回环临时端口，完成后结束服务进程。服务器未配置正式公网平台。

## 待完成的联合验收

1. 接入2号地图、移动和可见性，接入3号规则、计分与事件，按批准规则冻结状态顺序。
2. 与5号完成正式AI进程、MatchJob、沙箱、真实在线观战及平台后台联调。
3. 原版本UPM恢复后，补齐团队版本导入、Play、Windows/Linux构建与Ubuntu复验。
4. 其他成员独立拉取验收，并补充素材来源许可。PR保持草稿。
