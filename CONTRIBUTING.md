# GitHub 协作约定

## 仓库与访问

仓库：[kanfeng-eng/Turing-Signal-Relay](https://github.com/kanfeng-eng/Turing-Signal-Relay)，当前为公开仓库。

仓库已创建，无需再次初始化。拥有者邀请其余四人成为协作者，各成员接受邀请后才能推送任务分支。初次参与时执行：

```powershell
git clone https://github.com/kanfeng-eng/Turing-Signal-Relay.git
cd Turing-Signal-Relay
git remote -v
```

策划案原件当前仅保留在本地，未加入公开仓库；如需发布由拥有者决定。新成员不在已有克隆目录再次执行 `git init` 或添加同名 origin。

## 分支和 PR

- `main` 始终保持可打开/可构建；每个任务使用 `feat/<编号>-<任务>` 或 `fix/<编号>-<问题>`。
- 从最新 main 建分支；一个 PR 完成一个可验证任务。
- 合入前至少另一人审查；如果 GitHub 套餐支持，配置保护分支；否则由团队执行同样约定。
- 提交 PR 必填：解决的问题、改动目录、验证命令/操作及结果、相关 Issue、界面变化截图、未完成项。
- 不直接提交到他人模块；跨模块先在 Issue 中说明接口变化并邀请负责人审查。
- 不提交 token、服务器密码、选手私有源码、真实账号数据、数据库和比赛运行日志。

日常模板：

```powershell
git switch main
git pull --ff-only
git switch -c feat/2-pathfinding
# 修改并验证
git add <明确列出的文件>
git diff --cached
git commit -m "feat(world): add deterministic grid pathfinding"
git push -u origin feat/2-pathfinding
```

## Unity 文件冲突

- `Assets` 下文件必须与 `.meta` 一起提交；不手改 GUID、不通过覆盖目录解决冲突。
- `Visible Meta Files`、`Asset Serialization = Force Text` 由 1 号统一设定。
- 同一 Scene 同时只安排一人编辑。Bootstrap 由 1 号负责接线；观战布局做成 4 号自己的 Prefab，交由 1 号集成。
- 其余人使用独立测试场景；合入后由 4 号统一展示。不共同修改同一个巨型 Scene。
- `ProjectSettings`、`Packages/manifest.json`、`packages-lock.json` 改动必须由 1 号审查。
- 不提交 `Library`、`Temp`、`Obj`、`Logs`、个人 IDE 配置、构建和运行产物。
- 出现 Scene/Prefab 冲突时联系相关负责人，必要时配置 UnityYAMLMerge；不要无依据选择 ours/theirs。
- 首次引入大体积二进制素材时统一决定 Git LFS 策略，然后全员安装；当前没有 LFS 必要，不预设依赖。

## 接口与规则变更

- 接口由 1 号维护；1、3、5 号共同审查，4 号确认展示数据足够。
- 规则由 3 号维护台账，用户/规则负责人定稿；未决项只能出现在标明 provisional 的测试配置中。
- 冻结后每个正式比赛记录引擎版本、协议版本、规则配置哈希、地图种子、AI 源码哈希。
- 任何改动若影响上述内容，必须同步文档和对应 fixture，不默默改变正式参数。

## AI 辅助开发约定

每位成员向 AI 提供总 README、自己的编号指南、CONTRACTS 和相关源码。要求 AI 先复述任务范围，只修改相关模块，运行对应验证；不要让 AI 同时改五个模块或升级整个项目。

新增依赖须写明用途、版本、许可及是否支持 Unity/Linux；不接受未经运行验证的自动生成代码作为完成证据。
