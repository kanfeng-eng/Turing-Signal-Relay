# Ubuntu部署与维护

目标为Ubuntu 24.04 x86_64、cgroup v2和.NET 10运行时。安装需要本项目的管理员授权，应用使用tsr-platform和tsr-worker两个专用账号。平台监听127.0.0.1的5188端口。

## IP公网HTTP发布

用户于2026-11-07选择IP的HTTP访问。deploy/nginx/tsr-http.conf将111.231.101.147的80端口转发至本机5188，/downloads/映射至/srv/tsr/downloads。该目录仅放已校验的公开交付文件。Nginx配置保留300 KiB请求上限，平台继续检查身份、CSRF、队伍归属与提交配额。

在确认80端口空闲及默认站点为Ubuntu自带配置后，将本项目配置安装到/etc/nginx/sites-available/tsr-http，启用对应链接，移除sites-enabled/default链接，再运行nginx -t及systemctl enable --now nginx。云安全组需允许TCP80。验证公网Login、health、匿名API401、真实上传至双局及下载SHA256。

浏览器入口为http://111.231.101.147/Login。HTTP登录与观战令牌以明文传输，使用项目专用密码。后续有域名和证书时，在本项目Nginx站点启用HTTPS，并同步配置ASP.NET Core的受信转发头及安全Cookie，重新检查登录和观战。

## 构建与首次安装

在开发机发布平台和Worker，将发布目录、fixtures、sdk/cpp和deploy复制到服务器的新目录。依赖使用packages.lock.json锁定版本。

```text
dotnet restore server/Platform/Platform.csproj --locked-mode
dotnet restore server/Worker/Worker.csproj --locked-mode
dotnet publish server/Platform/Platform.csproj --no-restore -c Release -r linux-x64 --self-contained false -o NEW_RELEASE/platform
dotnet publish server/Worker/Worker.csproj --no-restore -c Release -r linux-x64 --self-contained false -o NEW_RELEASE/worker
```

在Ubuntu准备构建依赖与nsjail，源码固定提交和下载摘要由构建脚本核验。保留构建目录中的原始许可证与host-packages.txt。

```bash
sudo apt-get update
sudo apt-get install -y --no-install-recommends g++ git make pkg-config bison flex libprotobuf-dev protobuf-compiler libnl-route-3-dev libcap-dev autoconf libtool curl
bash deploy/sandbox/build-nsjail.sh NEW_BUILD_DIRECTORY
sudo bash deploy/sandbox/install.sh ABSOLUTE_NSJAIL_BINARY
sudo bash deploy/install-services.sh ABSOLUTE_RELEASE_DIRECTORY ABSOLUTE_DOTNET10_DIRECTORY
curl --fail http://127.0.0.1:5188/health
sudo -u tsr-platform env TSR_DATA=/var/lib/tsr/data /opt/tsr/dotnet/dotnet /opt/tsr/current/platform/Platform.dll create-judge judge
sudo systemctl enable --now tsr-worker
```

首次沙箱安装脚本从当前源码根目录读取sdk/cpp。发布目录须有platform、worker和fixtures，且deploy安装脚本可访问。create-judge从标准输入读取一行密码。建议通过安全终端输入，记录到自己的密码管理位置。服务配置中保存数据与源码根目录。

访问方法与页面流程见[平台说明](../docs/PLATFORM.md)。完整源码提交到结果的复验脚本见tools/check-platform.py，执行前确认两个服务active及health返回200。

## 目录与权限

| 目录 | 用途与权限 |
| --- | --- |
| /opt/tsr/releases | Root拥有的分版本程序 |
| /opt/tsr/current | 当前版本软链接 |
| /opt/tsr/previous-release | 上次发布路径 |
| /opt/tsr/dotnet | .NET10运行时 |
| /opt/tsr/sandbox | Root拥有的nsjail、SDK和编译/运行策略 |
| /usr/local/libexec/tsr-sandbox | Root拥有的固定协议helper |
| /etc/sudoers.d/tsr-worker | 仅授予worker调用该helper的权限 |
| /var/lib/tsr/data | 数据库、源码、编译诊断、比赛结果，两个服务通过tsr-data组共享 |
| /var/lib/tsr/data/keys | 平台账号独占的Data Protection密钥 |
| /var/lib/tsr-sandbox | Root管理的只读编译产物、临时编译及运行目录 |
| /sys/fs/cgroup/tsr | 本项目的隔离资源组 |

SQLite主文件及WAL/SHM为0660，数据目录2770，密钥目录0700。服务安装和应用启动会校验共享数据库权限。Web与沙箱的文件根目录分别配置，评测程序通过固定helper参数启动。

## 更新与回滚

等当前评测完成后停止worker，保留数据备份。使用新的发布目录运行install-services.sh，脚本创建版本目录、保存上次路径并重启平台。等待health成功后重新启动worker。

```bash
sudo systemctl stop tsr-worker
sudo python3 deploy/backup.py create NEW_BACKUP_DIRECTORY
sudo bash deploy/install-services.sh NEW_RELEASE_DIRECTORY ABSOLUTE_DOTNET10_DIRECTORY
curl --fail http://127.0.0.1:5188/health
sudo systemctl start tsr-worker
```

回滚时停止两个服务，将current软链接指向previous-release记录的完整目录，确认数据库结构兼容后启动平台与worker。跨数据库结构版本的回滚应配合对应的数据备份恢复。

服务重启会恢复数据库中的queued任务。中断的compiling/running任务最多重试一次，达到上限后保留失败原因供裁判处理。每次比赛尝试使用新目录，已完成结果的下载索引保持对应到该次尝试。

## 数据迁移、备份与恢复

初次启动执行EF Core迁移。已有本项目初始EnsureCreated库会先进行完整结构对比，匹配后登记初始迁移历史。手动迁移命令为以下入口。

```bash
sudo -u tsr-platform env TSR_DATA=/var/lib/tsr/data /opt/tsr/dotnet/dotnet /opt/tsr/current/platform/Platform.dll migrate
```

完整备份包含SQLite快照、源码、比赛产物、密钥和已就绪提交的只读编译缓存，并生成逐文件SHA256清单。目录含账号数据和密钥，由Root权限保护。

```bash
sudo python3 deploy/backup.py create NEW_BACKUP_DIRECTORY
sudo python3 deploy/backup.py verify BACKUP_DIRECTORY
sudo python3 deploy/backup.py restore BACKUP_DIRECTORY NEW_DATA_DIRECTORY --restore-cache
```

restore要求新目录。校验成功后，可停止服务，将/etc/tsr/contest.env的TSR_DATA指向恢复目录，并同步调整平台unit的ReadWritePaths，再启动平台和worker。原数据目录保留用于回退。单独的数据库快照可用Platform.dll backup NEW_SQLITE_FILE生成。

## 日常检查

```bash
sudo systemctl is-active tsr-platform tsr-worker
sudo journalctl -u tsr-worker -n 50 --no-pager
sudo journalctl -u tsr-platform -n 50 --no-pager
sudo find /sys/fs/cgroup/tsr -mindepth 1 -maxdepth 1 -type d
sudo du -sh /var/lib/tsr/data /var/lib/tsr-sandbox /opt/tsr/releases
```

空闲时运行资源组应已回收。源码、结果、日志和Root编译缓存的归档应与数据库记录关联。旧程序发布目录可在确认当前版本和回滚版本后归档，数据保留按比赛安排管理。

实测版本为Ubuntu24.04.4、Linux6.8、GCC13.3.0、nsjail提交d8415bc8eea43cdd413daba25af63127019a1189、Kafel提交76d0f41bf3eb5c4008713d64b9767b461a9129a3，以及.NET10。比赛采用10ms CPU和100ms墙钟预算，复验时同时保存机器负载与超时记录。

2026-11-07已在现有目标服务器完成正常双局、故障与重启恢复、旧初始库备份恢复、同一构建的新版本目录发布与回滚演练。回滚后两个正式服务active，health返回200，数据库记录计数一致。干净主机从零重装尚未复验，当前证据见[平台验收记录](../docs/PLATFORM_VALIDATION.json)。
