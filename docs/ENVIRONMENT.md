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
- 若锁定补丁出现项目相关阻断，1 号先做最小复现，再提出团队统一补丁变更；禁止各自换版本。

## 目标运行入口（尚未实现）

| 场景 | 计划入口 | 落地负责人 |
| --- | --- | --- |
| Windows 开发演示 | Bootstrap 场景中的本地对战启动面板 | 1、4、5 |
| 离线回放 | 观战客户端打开 replay 文件/目录 | 4 |
| Ubuntu 裁判 | Linux Server 可执行文件读取比赛清单 | 1 |
| 提交和评测 | ASP.NET 平台 + 隔离 worker | 5 |

在对应代码落地的同一个 PR 中，将这里替换成实际路径和可复制命令。当前没有项目启动命令；不要运行想象中的脚本。

官方依据：[Unity 平台要求](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html)、[Dedicated Server 构建](https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server-build.html)、[.NET LTS](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)。

## 按编号安装，避免全员安装所有服务器工具

| 编号 | 在全员必装之外准备什么 | 不必提前准备什么 |
| --- | --- | --- |
| 1 | Linux Server构建模块、.NET10 SDK、Ubuntu构建验证环境 | 页面前端工具链、美术软件 |
| 2 | Unity Test Framework（工程包统一引入），C#编辑器 | EF数据库、nsjail、Web平台运行环境 |
| 3 | Unity Test Framework、规则fixture编辑环境 | C++编译器（除非本人要做完整AI联调）、运维环境 |
| 4 | 工程统一的Tilemap/uGUI/TMP包；参与页面布局时安装.NET10 SDK | React/Vue/Node、数据库服务器、AI训练环境 |
| 5 | .NET10 SDK、Ubuntu24.04/GCC/CMake、nsjail验证环境；部署时systemd/Nginx | 3D建模软件、额外Unity联网框架 |

1号统一引入Unity侧Newtonsoft Json等工程包，5号统一锁定服务端Identity/EF Core和C++ JSON依赖；其他成员拉取锁文件后恢复环境，不手工复制DLL。详细用途见[技术手册](IMPLEMENTATION_GUIDE.md)。
