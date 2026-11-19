# C++17 AI SDK

这是受控 JSON Lines 协议2的客户端，依赖已经固定并随库提供，无需下载额外头文件。只向 stdout 写协议，调试信息写 stderr。两个样例分别使用通信员和运输员取平报送回指挥所，策略只是验证闭环。

## Windows

需要 Visual Studio 的 C++ x64 工具链，支持 C++17。从仓库根目录执行，输出目录必须不存在。

```powershell
./tools/build-sample-ai.ps1 -OutputDirectory "$PWD/artifacts/my-ai"
```

旧 Dev-C++ 的 GCC 4.9 不支持本 SDK。开发自己的 ai.cpp 可在 x64 Native Tools 命令行使用 `cl /std:c++17 /O2 /EHsc /MT /utf-8 ai.cpp`，按文件位置调整 include 路径。

## Ubuntu

需要支持C++17的g++，正式Ubuntu24.04环境使用GCC13.3.0；早期可信程序验证曾使用GCC15.2.0。输出目录必须不存在。

```bash
bash sdk/cpp/build-linux.sh artifacts/my-ai
# CXX=/absolute/path/to/g++ bash sdk/cpp/build-linux.sh artifacts/my-ai
```

`--faults` 可作为第二个参数编译故障测试程序。它们专门用于验证判罚，不作为正常 AI 使用。

## 最小程序

```cpp
#include "turing_sdk.hpp"
int main() {
    tsr::Client ai;
    while (ai.getframeinfo()) {
        // ai.initialization() 包含 side、config、map。
        // ai.frame() 只包含本方允许观察的状态。
        // 在这里计算并调用 ai.moveTo(unitId, x, y) 等指令。
        ai.submit();
    }
}
```

初始化输入 relay-init，SDK 自动回复 relay-ready。随后每帧 relay-frame 对应一次 relay-commands。帧0命令在tick1应用，帧8999命令在tick9000应用，终局不再请求命令。每帧必须 submit 一次，包括空动作帧；stdin 关闭表示退出。每个单位只取首条命令，再校验合法性。合法重复目标保持已有进度。

| 方法 | 含义 |
| --- | --- |
| moveTo(uid,x,y)、move(uid,direction) | 移动，direction 为0左、1下、2右、3上 |
| wait(uid,ticks) | 等待，可由有效任务替换 |
| setupStation(uid)、withdrawStation(uid) | 通信员架台、撤台 |
| suppress(uid,x,y) | 压制目标格敌方台站 |
| buildBridge(uid,x,y)、buildRelay(uid,x,y) | 工兵施工，x/y是架桥点中心或中继格 |
| pickup(uid,"materials") | 取己方仓库、兵站或中央公共器材 |
| unloadMaterials(uid) | 卸入己方指挥所或非满兵站 |
| deliverMaterials(uid) | 9分钟后在枢纽交器材，累计不可取回 |
| transferMaterials(uid,targetUid) | 同方同格转交至接收容量 |
| loadMsg(uid,messageId)、deliverMsg(uid) | 装报、向己方指挥所或有效台站提交携带报文 |
| transferMsg(uid,targetUid,messageId) | 同方同格转交指定报文 |

单位 ID 为阵营×100加序号，序号0—2通信员、3—4工兵、5—6运输员。enum role 为0通信员、1工兵、2运输员。地图 rows[y][x] 从 y=0 向上。半分整数除以2才是分数。完整字段见仓库 Core/RelayModels.cs，规则见 docs/STAGES_4_5.md 和批准规则。

完整模式 featureSet 为 relay-full-v1。旧 movement-plain-report-v1 保留回归用途，不能据此判断完整玩法效果。资源和敌人受当前视野限制，自己的路径数组不暴露裁判基于隐藏桥状态计算的内部路径。静态地图、预设时间表公开，不能把未观察到的库存当成零。

每方每帧10ms CPU、100ms墙钟兜底，初始化2秒。累计3次超时判整场负，其他协议/崩溃故障立即判负。内存256 MiB、stdout单行64 KiB、128动作、输入2 MiB，stderr全局最多保存1 MiB。正式沙箱读取cgroup v2的CPU统计，可信Linux原生入口使用/proc/PID/schedstat，Windows开发模式记录CPU并执行墙钟兜底。

本地运行入口用于自有可信程序。正式提交已接入Ubuntu沙箱Worker，执行源码检查、隔离编译、文件/网络/线程/子进程限制及内存约束。上传与评测操作见[平台说明](../../docs/PLATFORM.md)，逐步体验见[使用报告](../../docs/如何玩这个游戏的使用报告.md)。

JSON依赖为 nlohmann/json v3.12.0，MIT许可和固定提交见 third_party/nlohmann/SOURCE.json、LICENSE.MIT。
