# 4号：2D画面、观战与回放

先读[总README](../../../README.md)、[契约](../../CONTRACTS.md)、[架构](../../ARCHITECTURE.md)。你负责只读表现，游戏单位只由AI控制。

## 你负责什么

- 2D俯视地图、单位/建筑、资源/报文、链路连线与迷雾表现。
- 主视角、小地图、数据条；计分曲线、事件倒计时、单位详情、枢纽进度。
- 镜头缩放/拖动/跟随事件、全图观战与阵营视角切换。
- 离线回放打开、播放/暂停、单步、倍速、跳转和错误提示。
- 协助5号完成简单提交/裁判页面布局，页面只做必要表单和状态，不做预热竞猜。

主目录：`game/Assets/Game/Presentation/`、`Prefabs/`、`Art/`、`Scenes/Observer.unity`、`Tests/Presentation/`。服务器页面布局在5号约定的位置修改。Bootstrap仅交Prefab给1号集成。

## 第一批工作

1. 无工程时先画主视角+小地图+数据条草图，与1、3号核对需要哪些字段。
2. 工程合入后建`feat/4-observer-view`；使用简单方块/圆点与明确图例，先不等正式美术资产。
3. 使用CONTRACTS的模拟快照画地图和红蓝实体，接入单位位置/分数；缺字段向1号提契约变更，不直读Core私有状态。
4. 读取3号生成、1号输出的第一份真实回放，实现暂停和单步，再做倍速、跳转和镜头跟随。
5. M3接入5号提供的授权只读快照通道；优先复用离线播放的渲染入口。

## 交付边界

你不实现正式计分、不向裁判发送单位动作、不把拖动图标当手动移动。镜头控制和离线回放暂停允许使用鼠标键盘。

离线全图回放和选手受限视角区分清楚。线上暂停只暂停客户端显示，不暂停正式裁判；返回实时状态时给出清楚提示。

回放跳转从权威检查点恢复，不根据画面插值猜测状态。插值仅用于显示，不反向修改规则坐标。

## 验证与验收

- 独立fixture无需真实AI就能显示地图、单位、分数和事件。
- 红蓝除颜色还有标签/形状区别；链路开断、压制、天气和枢纽进度能从画面识别。
- 暂停后单步前进恰好1tick；倍速不改变回放最终分数；跳转后单位/分数匹配检查点。
- 损坏或未知版本回放清楚报错，不显示假比赛结果。
- 不同窗口大小下地图和关键数据不遮挡；目标观战分辨率在首个UI PR记录，附截图。
- M1用真实回放演示最小闭环；M4验证完整15分钟回放和远程断流/恢复提示。

先交完整可读的基本画面，再补音效/动画。素材须记录来源和许可，不以无授权资源作为正式版本资产。

## 技术栈：Unity画面加少量网页布局

| 技术 | 用在哪里 | 具体交付 |
| --- | --- | --- |
| Unity Grid/Tilemap | 绘制道路、草地、山地、河流和静态点 | MapView、地图图例 |
| SpriteRenderer/Prefab | 表现通信员/工兵/运输员/指挥所 | UnitView、按类型区分的图标Prefab |
| LineRenderer | 显示节点连接、断链和覆盖提示 | LinkView；只读3号的边列表 |
| uGUI Canvas + TextMeshPro | 分数、倒计时、单位详情、按钮和时间轴 | ObserverHud、ReplayControls、枢纽进度面板 |
| Camera正交投影+C#输入 | 拖动、缩放、小地图定位、事件跟随 | CameraController；不发送单位动作 |
| C#只读数据绑定 | 从Snapshot/Event更新画面 | SnapshotPresenter；不自行判分 |
| UnityWebRequest/协程 | 非阻塞获取授权在线快照 | LiveSnapshotClient，断流/恢复UI |
| Razor .cshtml、HTML/CSS、少量原生JS | 正赛提交/裁判/结果页面布局 | server/Platform/Pages与wwwroot中的布局资源 |

UI包由1号统一在工程内锁定；TMP通过工程的uGUI包使用，不另装多个互相冲突版本。网页没有React/Vue/Node构建链，你只需掌握服务端模板中的HTML/CSS；登录、上传保存和权限由5号做。

## 文件级任务表

Unity路径相对于`game/Assets/Game/`，文件名为待实现建议。

| 任务卡 | 文件/内容 | 输入 → 输出 | 验收例子 |
| --- | --- | --- | --- |
| 4-A 假数据地图 | Presentation/MapView.cs、UnitView.cs、Prefabs/ObserverRoot.prefab | fixtures/ui快照 → 地图与单位 | 不启动AI也能看到双方实体 |
| 4-B 链路与HUD | Presentation/LinkView.cs、ObserverHud.cs | 3号边列表/得分/事件 → 可读状态 | 断链、压制、天气、分数能区分 |
| 4-C 镜头与迷雾 | Presentation/CameraController.cs、FogView.cs | 镜头输入/阵营遮罩 → 视图 | 缩放/拖动正常，切阵营不露隐藏单位 |
| 4-D 离线播放器 | Presentation/ReplayPlayer.cs、ReplayControls.cs | 1号ReplayReader → 逐tick显示 | 暂停/单步/倍速/跳转一致 |
| 4-E 线上观战 | Presentation/LiveSnapshotClient.cs | 授权snapshot端点 → 最新快照 | 过期响应不回滚画面，断流有提示 |
| 4-F 网页布局 | server/Platform/Pages的.cshtml及wwwroot/css/site.css | 5号PageModel假字段 → 表单/状态/结果页 | 真实/假数据同字段均不溢出，业务处理留5号 |

迷雾首版可用覆盖Tilemap表达，得分曲线先用简单绘制；不为这些画面提前引入Shader框架或图表服务。用原生组件达成验收即可。

## 页面需要明确画出什么

| 页面 | 用户看到/操作 | 5号提供的数据和行为 |
| --- | --- | --- |
| 提交页 | 选择ai.cpp、提交、查看状态/编译错误 | 登录状态、上传限额、表单handler、submissionId、状态和诊断 |
| 提交历史 | 本队提交列表、版本/时间/状态 | 经过队伍权限过滤的记录 |
| 裁判页 | 选择红蓝提交、种子、冻结规则，发起比赛 | 裁判授权、可选提交、创建比赛handler |
| 结果页 | 两局分数、分项账本、异常原因、回放入口 | MatchResult、授权产物链接 |

这些是正赛平台，不包含观众竞猜、手机号活动登录或预热排行榜。4号不把演示表单接到伪成功提示；后台未接通时页面明确显示演示数据。

## 联调时你具体怎么做

1. 让1号给Snapshot/Replay读取模型、3号给链路/得分fixture，不等完整对局。
2. 从fixture绘制一帧，再绘制两帧位置变化；逻辑坐标转Unity世界坐标由你的MapView负责，映射固定且记录。
3. 建ObserverRoot.prefab，在自己的Observer场景调试；交给1号挂入Bootstrap，避免同时改同一个Scene。
4. 回放的读取与检查点索引由1号提供；你实现时间轴选择和画面刷新，跳转不推测漏失事件。
5. 线上阶段让5号给授权测试账号与快照fixture，使用相同SnapshotPresenter替换数据源。

## 首个PR与验证

交4-A：一个模拟快照、ObserverRoot.prefab、可打开的演示场景、地图图例，以及实际分辨率截图。验证Console无错误；单位类型与阵营可区分；调整窗口后分数/时间仍可读。若尚无Unity工程，先交布局草图和所需字段，不交另一个game工程。

## 可交给AI的一张任务卡

```text
我是4号。阅读README、CONTRACTS、IMPLEMENTATION_GUIDE和4号指南。
只做4-A：复用现有Unity工程和Snapshot，用Tilemap/SpriteRenderer/uGUI画假数据地图。
不实现移动、计分或单位操控，不安装额外UI框架。缺字段先提出契约需求。
制作独立ObserverRoot.prefab和演示场景，不覆盖Bootstrap。
给出实际打开方法、截图和1号集成步骤；未渲染不声称视觉验收通过。
```
