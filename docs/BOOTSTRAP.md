# Bootstrap文件入口与交接

这是1号提供的无AI时钟基础；正式动作、地图、计分和比赛有效性仍待2/3/5号接入。团队Editor仍锁定6000.3.26f1；本机使用6000.6.8f1独立副本验证。副本创建和构建见[环境准备](ENVIRONMENT.md#临时使用新版开发基线保持不变)。

## 直接运行已构建的包

默认跑9000 tick，生成回放和同名`.result.json`。下面以Windows为例，在可执行文件所在目录运行：

```powershell
./TuringSignalRelay.exe -batchmode -nographics -logFile ./bootstrap.log --bootstrap-replay ./bootstrap.jsonl
```

自定义演示时长和种子：复制仓库的[配置样例](../fixtures/protocol/bootstrap-config.json)，再运行：

```powershell
./TuringSignalRelay.exe -batchmode -nographics -logFile ./bootstrap.log --bootstrap-config ./bootstrap-config.json --bootstrap-replay ./sample.jsonl --bootstrap-result ./sample.result.json
```

Ubuntu待实测命令（完整解压Linux包后）：

```bash
chmod +x ./TuringSignalRelay
./TuringSignalRelay -batchmode -nographics -logFile ./bootstrap.log --bootstrap-replay ./bootstrap.jsonl
```

- `--bootstrap-replay`必填；`--bootstrap-config`和`--bootstrap-result`可选。配置和结果都是UTF-8 JSON；文件名以短横线开头时使用`./文件名`形式，避免被识别为选项。
- 无配置时使用MatchConfig默认值；样例配置是3 tick、10Hz、seed=7。
- 回放和结果必须是两个不同的新文件；已存在时拒绝覆盖。再次运行请更换输出名或使用新的输出目录。
- 参数缺值、重复参数、未知bootstrap参数、无效配置或回放验证失败均退出1；不得当成比赛成功。失败时已有部分文件仅供诊断。
- 成功必须退出0、日志含`BOOTSTRAP_COMPLETED`、结果状态为`bootstrap-completed`且文件检查通过。该状态不等于正式比赛有效，也不代表沙箱或AI运行成功。

## 4号读取回放

新写出的Bootstrap回放为`replayVersion=2`：

```json
{"type":"bootstrap-replay","replayVersion":2,"config":{"protocolVersion":1,"ticksPerSecond":10,"durationTicks":3,"seed":7,"rulesVersion":"bootstrap-provisional"}}
{"snapshot":{"protocolVersion":1,"tick":0,"completed":false,"scoreHalfPoints":[0,0],"rulesVersion":"bootstrap-provisional"},"stateHash":"1f539e37dc317a243ea5055e0e9c7b8584f7213f1e20e30669139c43bb20335f"}
```

随后逐行写tick 1..durationTicks；上面的两行只是格式示例，完整文件必须有结束帧。

```csharp
foreach (var snapshot in BootstrapReplayReader.ReadSnapshots(path))
{
    // 交给表现层；不要把原始全局快照当成AI Observation。
}
```

程序集接线：Reader位于`TuringSignalRelay.Integration`。若4号在Presentation脚本中直接调用，先在Presentation的asmdef中添加Integration引用；Integration不得再反向引用Presentation，以免循环。也可以由独立加载适配器读取后只传Snapshot给表现组件。

该接口按行读取，不把整场回放存入内存。每帧先检查顺序、版本、字段和哈希再返回；**必须枚举到文件末尾才算整场完整性通过**。提前停止只验证已读前缀。只需最终帧时调用`ReadFinal(path)`，它会遍历并校验全部帧。

旧版`replayVersion=1`（直接写Snapshot）仍可读取，只有结构/顺序校验，没有帧哈希。未知版本会拒绝；正式回放格式尚未冻结。

## 5号读取运行结果

`BootstrapResult`记录`type=bootstrap-result`、`resultVersion=1`、`status=bootstrap-completed`、Unity版本、配置、最终快照、最终状态哈希和整个回放文件SHA256。不包含正式胜负、异常局判罚、双局聚合或AI资源计量。

独立复核（仓库根目录，Windows用python；Ubuntu可用python3）：

```powershell
python tools/verify-bootstrap-artifacts.py --replay <回放路径> --result <结果路径>
```

该工具只依赖Python标准库，逐行重算每帧哈希，并核对最终结果与回放文件摘要。用普通Python运行，不使用会禁用断言的`-O`选项。

## 哈希字节定义 v1

`BootstrapStateHash.Compute(config, snapshot)`对以下字节依次做SHA256，输出小写64位十六进制：

1. ASCII字节`TSR_BOOTSTRAP_STATE_V1`，无长度或结束符。
2. 32位小端有符号整数：protocolVersion、ticksPerSecond、durationTicks、seed。
3. 32位小端整数：rulesVersion的UTF-8字节长度，然后写这些UTF-8字节，不做Unicode归一化。
4. 32位小端整数tick；1字节completed（0或1）；两个32位小端整数scoreHalfPoints，先side 0后side 1。

它只覆盖当前Bootstrap字段。后续地图、单位、动作、事件和规则状态接入后，需另定正式状态哈希版本。状态哈希用于确定性比对与文件一致性，不是签名。

同配置的Bootstrap运行应得到相同最终状态哈希；Windows/Linux结果应比较状态哈希。回放文件摘要记录实际文件字节，可能因平台换行等不同而不同，不作为跨平台状态一致性的替代。

## 一次执行真实进程回归

在仓库根目录运行（output必须是尚不存在的目录）：

```powershell
python tools/check-bootstrap-headless.py --player <可执行文件绝对路径> --output <新的检查目录>
```

脚本实际启动构建产物，覆盖默认9000 tick、同配置重复运行、指定3 tick配置、错误配置、缺参、将Unity选项误作路径、重复/未知参数、缺少回放参数和拒绝覆盖。每次运行核对退出码与完成标记；成功文件再由独立Python工具重算哈希。通过后写入`CHECKS.json`。同一脚本可在Ubuntu上对Linux产物运行；本机尚未执行Linux版本。
