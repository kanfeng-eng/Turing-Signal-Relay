# 1号进度：Bootstrap基础交付

更新：2026-11-03。分支：`feat/1-bootstrap`。**这是待目标Unity验证的工程基础，不是1号全部任务完成，也不是正式比赛游戏。**

## 已实现

- game/下的版本锁定、最小包清单、文本序列化配置、Bootstrap场景及构建场景列表。
- Core/World/Rules/Presentation/Integration/Editor的asmdef与稳定.meta；后续成员可在各自目录加代码。
- 纯C# MatchConfig、MatchState、CommandBatch、Observation、Snapshot草案；10Hz时钟、暂停、单步、结束停止。
- BootstrapController用临时IMGUI显示tick和控制按钮。4号接手表现层后替换；当前不提前引入其UI依赖。
- Bootstrap演示回放写入/完整顺序读取检查；headless参数`--bootstrap-replay`跑9000tick并校验最终帧。
- Windows/Linux Server构建菜单，版本不符拒绝构建。
- 跨语言空帧与命令JSON fixture；实体数组目前仅占位，不构成最终SDK结构。

## 当前验证证据

Windows .NET Framework C#编译器已实际编译Models、TickClock、MatchEngine及BootstrapChecks，运行返回0：

```powershell
./tools/verify-bootstrap.ps1 -Compiler 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
```

输出：`PASS: tick timing, pause/step, completion, snapshots, validation, 9000 callbacks.`

这个检查仅证明纯C#逻辑，不证明Unity导入、Scene反序列化、JsonUtility、GUI或构建可用。Roslyn自带编译器首次尝试因缺省基础程序集引用失败，改用Windows内置编译器后通过。

## 环境阻碍

本机仅找到Unity2020安装，没有6000.3.26f1。官方Release API列出目标版本及安装包，但以下地址实际下载返回HTTP404：

```text
https://download.unity3d.com/download_unity/de8167e7440c/Windows64EditorInstaller/UnitySetup64-6000.3.26f1.exe
```

未用旧版本打开或升级工程。Unity导入/Play、Unity内回放检查、Windows构建、Linux构建与Ubuntu运行均 **未验证**。当前game为源文件搭建的最小项目；首次目标Editor导入会补齐默认ProjectSettings和包锁，需审查后提交。

## 安装目标Editor后接着做

1. Unity Hub添加game/，使用6000.3.26f1，确认Console没有编译错误。
2. 打开Assets/Game/Scenes/Bootstrap.unity，Play；tick递增，Pause停止，Step增加1，Resume继续，Restart归零。
3. 菜单Turing Signal Relay → Run Bootstrap Checks，必须输出BOOTSTRAP_CHECKS_PASSED。这一步额外验证Unity回放读写和未知版本拒绝。
4. 菜单Build Windows Bootstrap生成artifacts/windows/TuringSignalRelay.exe；实际运行并查看GUI。
5. 安装Linux Dedicated Server模块后Build Linux Server Bootstrap，产物位于artifacts/linux/。
6. Ubuntu上运行下列命令（尚未实际执行）：

```bash
chmod +x ./TuringSignalRelay
./TuringSignalRelay -batchmode -nographics -logFile ./bootstrap.log --bootstrap-replay ./bootstrap.jsonl
```

成功必须有退出码0、BOOTSTRAP_COMPLETED ticks=9000、9001份快照（含初始帧）及回放头。该结果只有零分bootstrap时钟，不代表玩法、AI或正式评测。

批处理入口（Editor路径由实际安装位置替换；当前未运行）：

```powershell
& '<Unity.exe路径>' -batchmode -nographics -quit -projectPath "$PWD/game" -executeMethod TuringSignalRelay.Editor.SmokeChecks.Run -logFile "$PWD/artifacts/unity-checks.log"
```

## 交接和未完成项

- 2/3/5号先审查DTO；实体结构、移动精度、R15顺序尚未冻结。当前MatchEngine不接收AI动作，不执行移动/计分。
- 当前bootstrap回放用Unity内置JsonUtility（只有简单字段），与正式Newtonsoft协议/动作审计回放分开；未声称完成1-D正式回放。
- 1-C正式组合入口等待2号World/3号Rules；1-F MatchRunner等待5号MatchJob和沙箱启动契约，尚未实现。
- 真正Build Profiles资产和完整默认项目设置待目标Editor生成；当前仅提供BuildPipeline脚本和场景设置。
- 邀请其他成员需要他们的GitHub账号；尚未发送邀请或消息。

在以上Unity验证通过前维持草稿PR，不合入main让其他成员误以为环境已验收。
