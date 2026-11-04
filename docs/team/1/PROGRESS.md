# 1号进度：Bootstrap基础交付

更新：2026-11-04。分支：`feat/1-bootstrap`，[草稿PR #1](https://github.com/kanfeng-eng/Turing-Signal-Relay/pull/1)。**当前交付是时钟和工程基础，不是正式比赛游戏，也不代表1号全部任务完成。**

## 版本决定

按用户最新要求，团队基线保持`6000.3.26f1`，ProjectVersion.txt与原提交完全相同。撤回未提交的6000.3.28f1调整；不把团队升级到6000.6.8f1。新版仅在脚本生成的独立副本开发，源码通过原版API静态编译检查；原版实际运行兼容性仍待补验。

## 已实现

- 最小Unity工程、稳定.meta、Bootstrap场景及构建场景列表。
- Core/World/Rules/Presentation/Integration/Editor和Checks的7个asmdef。
- 纯C# DTO草案、10Hz固定时钟、暂停/单步/完成停止、快照复制。
- 临时IMGUI调试界面，供4号后续替换。
- bootstrap JSONL快照写入与完整顺序验证；headless入口`--bootstrap-replay <路径>`。
- Windows/Linux Dedicated Server构建入口；默认检查原版本。脚本明确启用的新版副本可产出开发验证包，日志标明其用途。
- `tools/run-unity-check.ps1`核对实际Editor版本、等待进程退出，并验证完成标记；新版副本构建前先执行场景和回放检查。

## 本次已验证

| 检查 | 结果与边界 |
| --- | --- |
| 纯C#检查 | 通过：固定tick、暂停/单步、非法参数、结束停止、快照隔离、9000次回调 |
| 原版6000.3.26f1 API静态编译 | 通过；包含本次构建脚本改动；不代表原版Editor实际导入 |
| 新版未带副本开关 | 脚本拒绝，未启动原工程 |
| 6000.6.8f1独立副本 | 实际导入、asmdef编译、加载Bootstrap场景、Unity回放往返和未知版本拒绝均通过，日志有BOOTSTRAP_CHECKS_PASSED |
| 6000.6.8f1 Editor Play | 实际打开独立副本Bootstrap场景，进入Play并看到tick递增和运行状态；原工程未被新版打开 |
| Windows开发包 | 实际构建成功，进程退出0且有BOOTSTRAP_BUILD_COMPLETED StandaloneWindows64 |
| Windows headless运行 | 实际退出0，有BOOTSTRAP_COMPLETED ticks=9000；独立核对JSONL头+9001份快照，tick连续0..9000，最终completed=true，双边零分 |
| Windows客户端界面 | 实际观察tick递增；暂停293后保持不变，单步294，恢复继续递增，重启从tick 0重新运行 |
| Linux Server开发包 | 实际构建成功，退出0且有BOOTSTRAP_BUILD_COMPLETED StandaloneLinux64；入口和UnityPlayer.so均核对为ELF64 x86_64 |
| 两份开发副本源码一致性 | 所有Assets文件逐字节等于原仓库源码；未为新版改写玩法代码或升级包依赖 |
| Ubuntu实际运行 | 未执行；本机未安装WSL、未提供Ubuntu目标 |

验证命令与副本开发操作见[环境准备](../../ENVIRONMENT.md#临时使用新版开发基线保持不变)。

```powershell
./tools/verify-bootstrap.ps1 -Compiler 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
./tools/verify-unity-api.ps1 -EditorDirectory 'D:\kanfe\Unity\6000.3.26f1\Editor'
```

## 本地证据位置

以下均在忽略目录artifacts中，不提交二进制和含本机信息的原始日志：

- Windows副本：`artifacts/development-6000.6.8f1-d0ea8490b83048a5b5fa9eaa4f7f63b0/`，game为可打开的开发副本，artifacts/windows为可执行包，artifacts下有unity-check.log、unity-windows.log、player-headless.log和bootstrap.jsonl。
- Linux副本：`artifacts/development-6000.6.8f1-b2aea4480a8e4278a25c340f272a5b80/`，artifacts/linux为裁判基础包，artifacts下有unity-check.log、unity-linuxserver.log。
- 原版/补丁排查日志：`artifacts/unity-cli-checks.log`、`unity-ascii-checks.log`、`unity-fresh-checks.log`、`unity-check.log`；新版对照日志：`unity-fresh66-checks.log`、`unity-source66-checks.log`。
- 原Editor自动生成但尚未验收的默认设置已保留在`artifacts/diagnostics/original-editor-generated-settings/`，不混入本次源码提交。
- 可分享的开发验证包位于`artifacts/delivery/`，版本与实际测试状态写在包内说明中。

## 原版阻碍与排查结论

Unity Hub及Personal许可证已由用户激活，启动权限问题已解除。6000.3.26f1和6000.3.28f1在本机均于UPM包解析阶段返回HTTP 500，错误为`The "path" argument must be of type string. Received undefined`。独立缓存、纯英文路径、原版空工程也能复现；6000.6.8f1空工程和同源码副本通过。

这些对照仅定位到当前机器的6.3工具链组合，尚未证明具体根因，不能宣称Unity 6.3在其他机器普遍不可用。保留原版本，不继续无目的更换补丁或修改规则源码。

## Ubuntu接手验证

将开发验证Linux包完整解压到Ubuntu x86_64，在目录中执行（本机尚未执行）：

```bash
chmod +x ./TuringSignalRelay
./TuringSignalRelay -batchmode -nographics -logFile ./bootstrap.log --bootstrap-replay ./bootstrap.jsonl
```

必须退出0、日志含BOOTSTRAP_COMPLETED ticks=9000，并核对回放头+9001份快照。这只验证零分时钟基础，不代表AI对战、正式比赛裁判或安全沙箱。

## 交接和未完成项

- 2/3/5号审查DTO；实体结构、移动精度、R15顺序尚未冻结；MatchEngine尚不执行AI动作、移动或计分。
- 正式1-C组合入口等待2号World/3号Rules；1-D动作/事件审计回放等待正式协议；1-F MatchRunner等待5号MatchJob和沙箱契约。
- 4号继续场景、地图与观战界面；当前仅为临时IMGUI。
- 原版实际导入、Play、Windows/Linux构建仍须解决UPM后重新验收；新版结果不替代这些检查。
- 正式Build Profiles和默认项目设置待原版Editor正常生成并审查，当前使用BuildPipeline入口。
- 五人独立拉取验收尚未进行；不合并main，PR维持草稿。
