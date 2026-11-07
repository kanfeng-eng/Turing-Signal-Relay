# 观战、素材和平台交接

当前由同一人接手1号与4号。团队工程版本为6000.3.26f1，本机使用6000.6.8f1独立副本验证。

## 运行观战客户端

Windows构建入口仍为tools/run-unity-check.ps1的Windows动作。启动生成的TuringSignalRelay.exe，进入Observer场景。编辑器也可以打开Bootstrap场景再Play，其中已接入ObserverRoot预制体。

初始画面加载本地演示数据。顶部控制播放、暂停、单步、0.5/1/2/4倍速与时间轴。右侧显示半分换算后的分数、天气、事件、枢纽进度、得分曲线和小地图。红蓝另有R/B文字，角色用C/E/T区分。地图滚轮缩放，拖动平移，点击单位后可跟随，小地图点击可定位。阵营视角按数据中的可见性掩码显示。

当前界面使用com.unity.ugui 2.0.0，包含Canvas、按钮、滑条、输入框、滚动侧栏、裁剪与原生UI图形。地图采用RawImage地形纹理与Image精灵，曲线采用MaskableGraphic。实现选择以96×96数据图为输入，原指南中的Tilemap/TMP在此版本采用纹理与原生Text替代。字体优先使用系统Microsoft YaHei并提供Segoe UI/Arial回退。

## 回放

侧栏输入文件路径后选择Open replay。

- .json使用observer-recording v1，包含每个tick的完整ObserverFrame。
- .jsonl读取1号Bootstrap v1/v2快照流，并复核原有帧哈希。
- Bootstrap快照只记录时钟与分数。观看它时显示对应字段，地图实体由带位置的ObserverFrame提供。
- 文件加载先验证，再替换当前记录。未知版本、缺帧或损坏内容会报错并保留原记录。
- 现有301帧演示数据位于Resources/ObserverDemo.bytes，是UTF-8 JSON的gzip压缩包。

## 在线快照草案

侧栏输入授权HTTPS地址和Bearer令牌后Connect。本地联调支持http://127.0.0.1等回环地址。轮询超时5秒，正常间隔0.5秒。令牌只在内存保存。

支持原始observer-frame，及以下快照信封。

```json
{"protocolVersion":1,"tick":100,"status":"running","snapshot":{"protocolVersion":1,"type":"observer-frame","tick":100}}
```

上例省略了完整数据，实际snapshot必须通过ObserverFrame.Validate。完整字段定义在Presentation/ObserverFrame.cs。terrain和visibleToMask按y*width+x排列，原点左下。可见性0表示不可见，1为红方，2为蓝方，3为双方。坐标为显示坐标，正式移动精度由World接口提供。

实体kind目前采用0通信员、1工兵、2运输员、3报文图标、4建筑图标的展示约定。实体附带id、side、x/y、label、visibleToMask和suppressed。links通过实体ID连接，connected由数据源提供。eventFocusId供事件跟随，nextEventTick供倒计时。

旧帧和重复帧会丢弃。切换地址或令牌后需重新连接，连接代次用于拒绝前一次请求。Hold display只暂停取帧显示，Return to live恢复轮询。断流时保留最后一帧并显示连接诊断。

暂停/恢复会切换请求代次并取消在途请求，加载离线回放也会取消在线请求。请求释放时清除句柄，防止退出时再次操作已释放对象。快照请求拒绝自动重定向，地址须填写最终端点。该行为使用UnityWebRequest.redirectLimit设置。[Unity API说明](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Networking.UnityWebRequest-redirectLimit.html)

展示实体id须为非负数，-1保留给未选中/无事件焦点。durationTicks须为正数，tick须在0到durationTicks范围内。Bootstrap导入保留配置中的时长与频率。

5号已接入经过授权的真实快照，接口和账号使用方式见[平台说明](PLATFORM.md)。本轮Unity独立副本使用生产Poll迭代器与真实UnityWebRequest，通过正常双局、文件违规与CPU超时终局四项快照检查，同时拒绝无效令牌和重复帧。

## 旧素材

本地选用了13个模型，重新配置中性材质，正交渲染并裁剪到256×256透明贴图。原始来源与摘要在Art/LegacyManifest.json。New/Police、New/Bailing和New/Student用于人物贴图，替换了根目录中辨识度不足的方块组合。

用户已于 2026-11-07 确认旧代码和素材均为其所有。选定的 13 张派生精灵及导入配置纳入源码，原始模型与修复材质保留在本地，功能检查记录可用贴图数量。

重新生成需要本地LegacyModels目录，执行Editor.LegacyArt.Prepare，图形渲染应使用启用图形设备的Editor批处理。该方法同时生成ObserverRoot.prefab、Observer场景和Bootstrap接线。验证版本使用6000.6.8f1，副本及操作见ENVIRONMENT.md。

## 平台页面

server/Platform采用.NET10 Razor Pages，包含登录、提交AI、提交历史、裁判台、比赛结果和退出。页面已接入Identity、数据库与正式Worker，按队伍及裁判权限显示真实记录。布局具有文件输入标签、表格标题、窄屏排版与键盘焦点样式。具体运行命令与流程见[平台说明](PLATFORM.md)。

```powershell
dotnet build server/Platform/Platform.csproj
dotnet run --no-build --project server/Platform/Platform.csproj --urls http://127.0.0.1:5184
```

本机SDK安装在D:\kanfe\dotnet10，调用其dotnet.exe可运行以上命令。演示页面时期曾在1280/375宽度检查布局，本轮真实后台流程与Unity联调证据见[平台验收记录](PLATFORM_VALIDATION.json)。

## 验证记录

ObserverChecks由SmokeChecks调用，覆盖301帧读取、暂停、单步、倍速与跳转一致性、未知版本拒绝、可见性过滤、在线旧帧/重复帧拒绝、快照信封与9001帧Bootstrap回放。

窗口工具重置后，原生窗口已检查全图、红方视角隐藏敌方、单步0到1、连续播放至242、时间轴跳转至75、恢复播放并暂停于189，后续检查中保持189。滚轮缩放和地图裁剪正常，侧栏滚动速度已修正。原生UI的14个按钮事件、时间轴、地图初始化通过自动检查。地图与曲线采用RectMask2D裁剪。

Ubuntu实际检查已覆盖Linux完整9000 tick、重复运行哈希一致、参数/产物保护、双进程调度、异常退出、缺产物和超时子进程回收。服务器测试目录为用户目录下的turing-signal-relay/validation-20261104。运行时安装在该目录中。平台HTTP检查期间使用回环临时端口，并在检查后结束测试进程。

原生Canvas在ObserverController.Start中生成，ObserverRoot.prefab负责场景接线。界面布局以C#维护，方便Git合并。在线帧可带durationTicks，默认9000。离线倒计时按回放的实际帧数和时钟频率计算。

## 本地素材复现

```powershell
./tools/import-legacy-models.ps1 -SourceDirectory '<原始Model/Model目录>'
```

脚本核对13项源文件，保留已有.meta，并在目标模型内容不同的时候报错。随后在独立副本中用启用图形设备的Editor调用TuringSignalRelay.Editor.LegacyArt.Prepare。贴图使用Single模式，256×256，256 pixels/unit。

原生UI实际窗口已成功启动并显示中文界面。此前Windows防火墙弹窗消失后恢复了游戏内检查，系统安全权限操作由用户处理。构建脚本输出普通Player构建并设置产品名Turing Signal Relay。

## 真实HTTP回归

先按ENVIRONMENT.md创建6000.6.8f1独立副本并完成Check，再运行以下Windows命令，将路径替换为本次副本。日志路径须为新文件。

```powershell
python tools/check-observer-network.py --editor 'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe' --project '<独立副本>\game' --log '<独立副本>\artifacts\observer-network.log'
```

脚本在127.0.0.1临时端口提供只读测试快照，在Editor内驱动生产Poll迭代器和真实UnityWebRequest。覆盖重复/旧帧、畸形JSON、HTTP503后恢复、暂停/恢复取消、地址/令牌切换、302拒绝、401、5秒超时、离线回放切换及迭代器释放。测试结束后关闭端口，日志与请求统计保存在指定输出位置。

真实平台另使用TuringSignalRelay.Editor.ObserverNetworkChecks.RunPlatform，参数-observerPlatformConfig指向本机私有JSON。文件含cases数组，每项提供endpoint、token、tick和status。通过SSH隧道请求正式接口，令牌保留在私有文件中。本轮四项终局快照验证通过，原Unity工程基线保持不变。
