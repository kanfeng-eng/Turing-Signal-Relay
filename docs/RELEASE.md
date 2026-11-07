# 发布与Ubuntu验收

## ⑧最终验收裁定

2026-11-07已完成现有环境的新一轮复验，候选包可用于当前已验证环境。正式发布暂缓，原Unity6000.3.26f1导入/构建和干净Ubuntu从零安装仍为待通过门槛。逐项结果、历史证据范围与复验命令见[发布验收报告](发布验收报告.md)及[阶段8机器记录](STAGE8_VALIDATION.json)。

README、[使用报告](如何玩这个游戏的使用报告.md)及[技术报告](完成这个游戏的技术报告.md)已先行提交，文档初始提交为b0f6d7b。本轮保留5份程序包及其01b22ce构建来源，最新文档和验收记录通过当前Git分支交付。归档中的文档仍为打包时快照。

阶段7负责将已实现的完整玩法、观战、SDK和平台整理成可运行的交付包。原Unity基线为6000.3.26f1；获准的6000.6.8f1独立副本产物在BUILD-INFO.json中单独标明。干净Ubuntu从零部署需要独立测试主机，用户当前暂无额外服务器，现有Ubuntu承担实际运行复验。

## 本轮交付状态

2026-11-07，可独立推进的构建、打包、现有Ubuntu复验与服务更新已通过，5份包位于本机artifacts/stage7/release-final。构建源码为干净提交01b22ce，版本、SHA256与边界见[阶段7记录](STAGE7_VALIDATION.json)。总体状态继续保留两项发布门槛。

Windows与Linux原生完整9000tick、部署后的.NET共享规则裁判结果摘要一致。现有Ubuntu通过24项Worker回归、10项原生入口检查、双进程兼容入口、真实登录跳转及匿名API拒绝。服务包已更新部署，真实上传至双局结果和回放检查通过。

本轮修正了Worker.Checks在进程树终止后立即断言子进程退出的时序问题，复用已有30秒有界等待，Windows与Ubuntu均重新通过24项。依据为.NET的异步终止及后代进程等待语义。[Microsoft文档](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-10.0)

## 一键构建与本机验收

需要Python、.NET10 SDK、Visual Studio C++17 x64工具链，以及Unity Editor的Windows和Linux Dedicated Server构建模块。在仓库根目录执行，输出目录须为新目录。

```powershell
./tools/verify-and-package.ps1 -EditorDirectory 'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor' -ApiEditorDirectory 'D:\kanfe\Unity\6000.3.26f1\Editor' -Dotnet 'D:\kanfe\dotnet10\dotnet.exe' -DevelopmentCopy -OutputDirectory "$PWD/artifacts/stage7/NEW_DELIVERY"
```

流程覆盖旧时钟与地图、正式子集、76项完整规则、真实C++双局、进程故障、Worker回归、Unity导入与双平台构建、观战回放和网络回归、Windows原生完整9000tick以及归档结构校验。通过时输出DELIVERY_VERIFIED。

## 交付文件

| 文件 | 用途 |
| --- | --- |
| TuringSignalRelay-windows.zip | Windows观战客户端及两局完整样例回放 |
| TuringSignalRelay-linux.tar.gz | Unity Linux原生裁判，用于原生运行与一致性验证 |
| TuringSignalRelay-server-linux.tar.gz | 正式.NET平台与Worker、隔离策略、部署脚本、SDK、规则与运维文档 |
| TuringSignalRelay-sdk.zip | C++17接口、样例、固定JSON依赖和许可、协议及规则fixture |
| TuringSignalRelay-source.zip | 本次审计的公开源码快照和构建元数据 |
| DELIVERY.json | 本机验证结果及所有交付包的字节数与SHA256 |
| BUILD-INFO.json、source-manifest.json | 源码基点、工作区修改状态、构建版本和逐文件摘要 |
| UBUNTU.json | 同一交付包在目标Ubuntu上的独立运行记录，完成目标复验后生成 |

## 启动与安装

Windows解压后运行TuringSignalRelay.exe，从打开回放入口选择examples/game-1.replay.json或game-2.replay.json。在线观战通过SSH隧道访问平台，在结果页取得地址与临时令牌。操作细节见[观战说明](OBSERVER.md)。

正式服务包在Ubuntu新目录解压。安装.NET10运行时和nsjail构建依赖，按包内deploy/README.md执行固定版本构建与安装，再由裁判创建队伍账号。Worker使用.NET10直接引用相同C#规则，选手程序始终经过隔离编译和运行。部署操作见[部署手册](../deploy/README.md)。

## 目标Ubuntu复验

```powershell
python tools/check-ubuntu-package.py --delivery '<本轮交付目录>' --host YOUR_SERVER --user ubuntu --known-hosts artifacts/tsr_known_hosts --remote-root /home/ubuntu/turing-signal-relay --runtime /opt/tsr/dotnet/dotnet
```

脚本核对主机密钥和上传摘要，在用户目录的新位置展开产物，检查Linux原生程序、完整规则C++对局、Worker兼容入口和真实平台的未登录访问边界。测试使用独立数据目录和临时回环端口。

正式nsjail、账号归属、故障判负、重启恢复、队列限制及备份/回滚的已验证入口见[平台说明](PLATFORM.md)和[⑤⑥验收](STAGE56_VALIDATION.json)。后续从零部署验收需在独立干净Ubuntu环境执行同一套流程，并补充目标报告。
