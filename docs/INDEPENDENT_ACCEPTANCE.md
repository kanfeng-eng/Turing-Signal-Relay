# 1、4号独立开发与验收

本轮范围包含工程验证/打包、可信裁判异常处理、完整展示回放、镜头交互及多尺寸界面。正式地图、移动、计分、AI桥和沙箱由对应模块接入。

## 一键验证并打包

在仓库根目录运行以下Windows命令。需要Python、.NET10 SDK、指定Unity Editor及其Linux Dedicated Server模块。输出目录必须是新目录。

```powershell
./tools/verify-and-package.ps1 -EditorDirectory 'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor' -ApiEditorDirectory 'D:\kanfe\Unity\6000.3.26f1\Editor' -Dotnet 'D:\kanfe\dotnet10\dotnet.exe' -DevelopmentCopy
```

正常使用团队6000.3.26f1时，将EditorDirectory设为该版本，省略DevelopmentCopy，ApiEditorDirectory可使用默认值。新版验证创建独立副本，原工程版本文件保持6000.3.26f1。

流程顺序为源码/Meta/程序集/文档检查、纯C#检查、服务端发布和24项调度回归、Windows/Linux构建、原版API静态编译、观战交互与长回放检查、真实HTTP回归、Windows进程回归、压缩包完整性和架构检查。构建与Editor检查按顺序执行。

默认使用公开源码及已获用户所有权确认的选定精灵。追加 IncludeLocalArt 可在独立构建副本包含本地原始模型与修复材质。两种模式均生成输入文件摘要。

成功输出DELIVERY.json、BUILD-INFO.json、source-manifest.json、Windows ZIP、Linux tar.gz和各项检查日志。失败写FAILURE.json并保留现场。只有全部步骤通过才生成最终DELIVERY.json。BUILD-INFO记录源码提交、工作区修改状态、编辑器版本和素材模式。

## Ubuntu实测

验证工具使用Paramiko，通过已固定主机密钥的SSH连接，在登录用户家目录内创建独立验证目录。依赖可用python -m pip install paramiko安装。服务器需要Python3.12和.NET10/ASP.NET Core运行时，运行时路径指向服务器现有dotnet文件。

```powershell
python tools/check-ubuntu-package.py --delivery '<交付目录>' --host '<服务器地址>' --user ubuntu --known-hosts '<已有known_hosts文件>' --remote-root '<家目录中的既有目录>' --runtime '<服务器dotnet绝对路径>'
```

密码经隐藏输入读取，仅保存在内存。工具核对上传摘要后执行Linux裁判10项进程检查、24项调度回归、真实裁判双局调度和四页/CSS的HTTP检查，并核对Windows/Ubuntu最终状态哈希。Web测试监听回环临时端口，结束时回收进程。结果下载为UBUNTU.json及两份详细检查记录。

服务器正式平台部署仍由5号处理。该工具维护用户目录内的测试产物。

## 4号的完整展示回放

ObserverAcceptanceChecks动态生成9001帧、96×96地图、移动实体、链路开断、可见性、天气、事件和分数变化的展示fixture。数据用于检查表现与播放流程。

每轮验收包括三次加载、百次随机跳转、逐tick播放和4倍速终局一致性。当前工程门槛为每次加载少于60秒、保留托管内存低于256MiB、三次加载的净增长低于32MiB。报告分别记录托管内存、Unity分配量和可用的进程统计；一键流程另以250毫秒间隔采样Editor进程工作集峰值。

连续相同地形和可见性数组在加载后共享。展示帧按只读使用，更新通过新帧和新回放完成。

交互检查覆盖单位点选、拖动、缩放、小地图中心/偏移定位、单位跟随、事件跟随、阵营过滤、回放切换缓存清理和令牌掩码。布局覆盖960×600、1024×768、1280×800、1920×1080及2560×1440五种宽高比，采用960×600逻辑布局。Windows高分屏另检查实际窗口。

## 1号的调度回归

Worker.Checks使用独立可信测试进程。24项检查包含正常v1/v2回放、退出码、缺产物、畸形结果、摘要/最终哈希错误、配置不一致、回放帧篡改/重复/截断、未知版本、启动失败、开始前取消、运行中取消、退出后取消、超时子进程回收、并发目录占用及已有产物保护。

Worker对结果和回放逐帧校验，并使用Core的BootstrapStateHash验证规范状态。该调度器启动可信Unity裁判，选手AI隔离仍需5号的沙箱适配。

## 原版工具链

本机6000.3.26f1的UPM错误仍可复现，三组对照见[环境诊断](UNITY_BASELINE_DIAGNOSTIC.md)。这项正常工作流验收继续标记为受阻。6000.6.8f1独立副本的执行结果单独记录。
