# 环境准备

## 全员必装

1. Git；编辑器可用 Visual Studio 或 Rider，启用 Unity/C# 支持。
2. Unity Hub，Unity `6000.3.26f1`。从[官方版本页](https://unity.com/releases/editor/whats-new/6000.3.26f1)安装指定版本。
3. Hub 添加 `Linux Dedicated Server Build Support`。1、5 号必须完成 Linux 构建验证；其余成员至少能打开 Windows 工程。
4. Windows 上需要本地运行 C++ AI 的成员安装提供 C++17 的编译器；正式参考编译器使用 Ubuntu 24.04 的 GCC，具体版本和镜像摘要由 5 号在 M0 锁定。

1 号在初始化时使用基础 2D 模板，不引入 HDRP 或第三方框架；先确认默认 Sprite、Tilemap 和 UI 能在工程内运行。将模板实际生成的包版本提交到 `Packages`，不要求其他人手动逐包安装。

## 1、5号及参与平台开发的人

- 安装 .NET 10 LTS SDK；Unity 自带运行时与 .NET SDK 是不同环境，不把 Unity 脚本目标改成 .NET 10。
- 准备 Ubuntu 24.04 x86_64 实机/虚拟机用于开发测试；WSL2 可用于早期工具调试，不能替代正式部署验收。
- Ubuntu 服务器上安装运行产物所需依赖，由 5 号写入部署脚本；正式服务器不需要打开 Unity Editor。
- 首次使用沙箱前 5 号核实宿主内核、安全配置与执行器支持；Windows 普通进程不等于 Linux 沙箱。

环境检查（仅检查工具，不表示项目已构建）：

```powershell
git --version
dotnet --info
```

Ubuntu 工具检查：

```bash
uname -m
cat /etc/os-release
g++ --version
dotnet --info
```

## M0 统一工程检查单（1号执行）

- 在根目录下创建 `game/`；提交 `Assets/`、`Packages/`、`ProjectSettings/`。
- 用 `ProjectSettings/ProjectVersion.txt` 锁定实际 Editor 版本；全员核对。
- 设置 Visible Meta Files 与 Force Text。
- 创建 `Assets/Game/Scenes/Bootstrap.unity` 并加入构建场景列表。
- 创建 Windows 客户端与 Linux Server Build Profiles；开始时优先验证 Mono 后端，不假设 Windows 可交叉生成所有 IL2CPP 目标。
- Editor 编译、Windows Play、Linux Server 启动各留一份日志。Linux 的构建与运行由实际输出证明，不以 Hub 里存在模块为通过。
- 5 人各自从 Git 拉取后独立打开一次，不拷贝任何人的 `Library`。
- 若锁定补丁出现阻断，先做最小复现。当前用户要求保留6000.3.26f1，仅允许6000.6.8f1在独立副本开发验证，不提交新版生成的工程设置和包锁。

## 目标运行入口

bootstrap分支已加入最小场景、构建菜单及headless演示入口，已在6000.6.8f1独立副本完成导入和构建验证，原版仍有UPM阻碍；[1号进度](team/1/PROGRESS.md)记录了安装位置、现有验证命令和阻碍。下表中的正式对战/平台入口仍待后续模块实现。

| 场景 | 计划入口 | 落地负责人 |
| --- | --- | --- |
| Windows 开发演示 | Bootstrap 场景中的本地对战启动面板 | 1、4、5 |
| 离线回放 | 观战客户端打开 replay 文件/目录 | 4 |
| Ubuntu 裁判 | Linux Server 可执行文件读取比赛清单 | 1 |
| 提交和评测 | ASP.NET 平台 + 隔离 worker | 5 |

在对应代码落地的同一个 PR 中补充实际路径、命令和验证结果。不要把bootstrap的零分时钟演示当作正式AI对战；具体已落地入口见1号进度。

官方依据：[Unity 平台要求](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html)、[Dedicated Server 构建](https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server-build.html)、[.NET LTS](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)。

## 按编号安装，避免全员安装所有服务器工具

| 编号 | 在全员必装之外准备什么 | 不必提前准备什么 |
| --- | --- | --- |
| 1 | Linux Server构建模块、.NET10 SDK、Ubuntu构建验证环境 | 页面前端工具链、美术软件 |
| 2 | Unity Test Framework（工程包统一引入），C#编辑器 | EF数据库、nsjail、Web平台运行环境 |
| 3 | Unity Test Framework、规则fixture编辑环境 | C++编译器（除非本人要做完整AI联调）、运维环境 |
| 4 | 工程固定的uGUI 2.0.0包；参与页面布局时安装.NET10 SDK | React/Vue/Node、数据库服务器、AI训练环境 |
| 5 | .NET10 SDK、Ubuntu24.04/GCC/CMake、nsjail验证环境；部署时systemd/Nginx | 3D建模软件、额外Unity联网框架 |

当前Unity JSON读写使用JsonUtility，包清单固定uGUI 2.0.0与UnityWebRequest内置模块。5号统一锁定服务端Identity/EF Core和C++ JSON依赖。详细用途见[技术手册](IMPLEMENTATION_GUIDE.md)，已实现入口见[观战交接](OBSERVER.md)。

## 临时使用新版开发（基线保持不变）

不要用6000.6.8f1直接打开仓库的`game/`。在仓库根目录运行：

```powershell
./tools/run-unity-check.ps1 -EditorDirectory 'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor' -DevelopmentCopy -Action Windows
./tools/run-unity-check.ps1 -EditorDirectory 'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor' -DevelopmentCopy -Action LinuxServer
```

每次命令新建`artifacts/development-6000.6.8f1-<唯一编号>/game`，只复制源码、已跟踪的Packages/ProjectSettings，在副本中改版本；日志和构建产物放在同级`artifacts/`。构建前先导入并执行Bootstrap检查，退出码和完成标志均通过才报告成功。Linux命令需先安装新版对应的Linux Dedicated Server模块。

编辑器开发：打开命令输出的副本`game`路径。完成后只把需要的`.cs`等源码同步回原仓库，使用下次新副本重新验证；不回拷Library、Packages锁或ProjectSettings。新增资源需同时带`.meta`。提交前用原版API静态编译检查；新版验证不等于原版运行兼容性验收。

原版实际检查入口（目前会受UPM故障阻碍）：

```powershell
./tools/run-unity-check.ps1 -EditorDirectory 'D:\kanfe\Unity\6000.3.26f1\Editor' -Action Check
```

不带`-DevelopmentCopy`时脚本拒绝其他版本；构建菜单默认同样拒绝其他版本。新版副本构建由脚本显式传入`-bootstrapDevelopmentCopy`，日志有开发验证警告。正式发布仍须用原版完成构建与运行检查。
