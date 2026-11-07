param(
    [Parameter(Mandatory=$true)][string]$EditorDirectory,
    [string]$ApiEditorDirectory = $EditorDirectory,
    [string]$Dotnet = 'dotnet',
    [string]$Python = 'python',
    [string]$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
    [string]$OutputDirectory,
    [switch]$DevelopmentCopy,
    [switch]$IncludeLocalArt
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $taskRoot ('artifacts\delivery\independent-' + [guid]::NewGuid().ToString('N')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new delivery directory.' }
$taskDotnet = (Get-Command $Dotnet -ErrorAction Stop).Source
Get-Command $Python -ErrorAction Stop | Out-Null
if (!(Test-Path -LiteralPath (Join-Path $EditorDirectory 'Unity.exe'))) { throw 'Unity Editor executable is missing.' }
if (!(Test-Path -LiteralPath $Compiler)) { throw 'Pass -Compiler with the C# compiler used by verify-bootstrap.ps1.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$taskOldDotnetRoot = $env:DOTNET_ROOT
$env:DOTNET_ROOT = Split-Path -Parent $taskDotnet
function Invoke-Checked([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable exited with code $LASTEXITCODE" }
}
function Invoke-EditorCheck([string]$Method, [string]$Marker, [string]$Log) {
    $taskArguments = @('-batchmode','-nographics','-quit','-projectPath',('"'+$taskBuild.project+'"'),'-executeMethod',$Method,'-logFile',('"'+$Log+'"'))
    if ($DevelopmentCopy) { $taskArguments += '-bootstrapDevelopmentCopy' }
    $taskProcess = Start-Process -FilePath (Join-Path $EditorDirectory 'Unity.exe') -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
    $taskWatch = [Diagnostics.Stopwatch]::StartNew()
    $taskPeak = 0L
    while (!$taskProcess.WaitForExit(250)) {
        if ($taskWatch.Elapsed.TotalSeconds -gt 300) { Stop-Process -Id $taskProcess.Id; throw "Editor check timed out. Inspect $Log" }
        try {
            $taskProcess.Refresh()
            $taskPeak = [Math]::Max($taskPeak, $taskProcess.PeakWorkingSet64)
        } catch [InvalidOperationException] { }
    }
    [ordered]@{ sampledPeakWorkingSetBytes=$taskPeak; sampleIntervalMilliseconds=250; elapsedSeconds=$taskWatch.Elapsed.TotalSeconds } |
        ConvertTo-Json | Set-Content -LiteralPath ($Log+'.process.json') -Encoding utf8
    if ($taskProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $Log -SimpleMatch $Marker -Quiet)) { throw "Editor check failed. Inspect $Log" }
}
Push-Location $taskRoot
try {
    $taskSourceArguments = @('tools/verify-source.py','--output',(Join-Path $OutputDirectory 'source-manifest.json'))
    if ($IncludeLocalArt) { $taskSourceArguments += '--include-local-art' }
    Invoke-Checked $Python $taskSourceArguments
    & "$PSScriptRoot\verify-bootstrap.ps1" -Compiler $Compiler
    & "$PSScriptRoot\verify-world.ps1" -Compiler $Compiler
    & "$PSScriptRoot\verify-relay.ps1" -Dotnet $taskDotnet -OutputDirectory (Join-Path $OutputDirectory 'relay-stages123')
    & "$PSScriptRoot\check-local-match.ps1" -Dotnet $taskDotnet -OutputDirectory (Join-Path $OutputDirectory 'full-game')
    foreach ($taskService in @('Platform','Worker','Worker.Checks')) {
        Invoke-Checked $taskDotnet @('publish',"server/$taskService/$taskService.csproj",'-c','Release','-o',(Join-Path $OutputDirectory "server\$taskService"),'--nologo')
    }
    Invoke-Checked $taskDotnet @((Join-Path $OutputDirectory 'server\Worker.Checks\WorkerChecks.dll'),(Join-Path $OutputDirectory 'worker-checks'))
    # Linux apphost is required by the worker's process-tree fixture.
    Invoke-Checked $taskDotnet @('publish','server/Worker.Checks/Worker.Checks.csproj','-c','Release','-r','linux-x64','--self-contained','false','-o',(Join-Path $OutputDirectory 'server-linux\Worker.Checks'),'--nologo')
    foreach ($taskService in @('Platform','Worker')) {
        Invoke-Checked $taskDotnet @('restore',"server/$taskService/$taskService.csproj",'--locked-mode')
        Invoke-Checked $taskDotnet @('publish',"server/$taskService/$taskService.csproj",'--no-restore','-c','Release','-r','linux-x64','--self-contained','false','-o',(Join-Path $OutputDirectory "server-linux\$taskService"),'--nologo')
    }
    $taskResult = Join-Path $OutputDirectory 'unity-result.json'
    & "$PSScriptRoot\run-unity-check.ps1" -EditorDirectory $EditorDirectory -Action Delivery -DevelopmentCopy:$DevelopmentCopy -PublicSource:(!$IncludeLocalArt) -ResultFile $taskResult
    $taskBuild = Get-Content -LiteralPath $taskResult -Raw | ConvertFrom-Json
    & "$PSScriptRoot\verify-unity-api.ps1" -EditorDirectory $ApiEditorDirectory -Compiler $Compiler -UiAssembly (Join-Path $taskBuild.project 'Library\ScriptAssemblies\UnityEngine.UI.dll')
    Invoke-EditorCheck 'TuringSignalRelay.Editor.ObserverAcceptanceChecks.Run' 'OBSERVER_ACCEPTANCE_CHECKS_PASSED' (Join-Path $OutputDirectory 'observer-acceptance.log')
    Copy-Item -LiteralPath (Join-Path $taskBuild.artifacts 'observer-acceptance.json') -Destination $OutputDirectory
    Invoke-Checked $Python @('tools/check-observer-network.py','--editor',(Join-Path $EditorDirectory 'Unity.exe'),'--project',$taskBuild.project,'--log',(Join-Path $OutputDirectory 'observer-network.log'))
    Invoke-Checked $Python @('tools/check-bootstrap-headless.py','--player',(Join-Path $taskBuild.artifacts 'windows\TuringSignalRelay.exe'),'--output',(Join-Path $OutputDirectory 'windows-headless'))
    $taskSeries = Get-Content -LiteralPath (Join-Path $OutputDirectory 'full-game\series\series.result.json') -Raw | ConvertFrom-Json
    Invoke-Checked $Python @('tools/check-relay-headless.py','--player',(Join-Path $taskBuild.artifacts 'windows\TuringSignalRelay.exe'),'--repository',$taskRoot,'--red',(Join-Path $OutputDirectory 'full-game\cpp\courier.exe'),'--blue',(Join-Path $OutputDirectory 'full-game\cpp\transporter.exe'),'--output',(Join-Path $OutputDirectory 'windows-full-game'),'--expected-hash',$taskSeries.games[0].result.stateHash)
    $taskExamples = Join-Path $taskBuild.artifacts 'windows\examples'
    New-Item -ItemType Directory -Path $taskExamples | Out-Null
    Copy-Item -LiteralPath (Join-Path $OutputDirectory 'full-game\series\game-1\relay.replay.json') -Destination (Join-Path $taskExamples 'game-1.replay.json')
    Copy-Item -LiteralPath (Join-Path $OutputDirectory 'full-game\series\game-2\relay.replay.json') -Destination (Join-Path $taskExamples 'game-2.replay.json')
    $taskGitCommit = & git rev-parse HEAD
    $taskDirty = [bool](& git status --porcelain --untracked-files=all -- game/Assets game/Packages game/ProjectSettings server tools docs fixtures README.md .gitignore .gitattributes)
    if ($IncludeLocalArt) { $taskDirty = $true }
    $taskVersion = (Get-Content game/ProjectSettings/ProjectVersion.txt | Select-String '^m_EditorVersion: (.+)$').Matches.Groups[1].Value
    $taskInfo = [ordered]@{
        sourceCommit = $taskGitCommit
        sourceModified = $taskDirty
        teamEditor = $taskVersion
        buildEditor = $taskBuild.editorVersion
        legacyArtIncluded = [bool]$IncludeLocalArt
        sourceManifest = 'source-manifest.json'
        status = 'independent-module-validation'
    }
    $taskInfo | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'BUILD-INFO.json') -Encoding utf8
    Copy-Item -LiteralPath (Join-Path $OutputDirectory 'BUILD-INFO.json') -Destination (Join-Path $taskBuild.artifacts 'windows\BUILD-INFO.json')
    Copy-Item -LiteralPath (Join-Path $OutputDirectory 'BUILD-INFO.json') -Destination (Join-Path $taskBuild.artifacts 'linux\BUILD-INFO.json')
    Copy-Item -LiteralPath (Join-Path $OutputDirectory 'source-manifest.json') -Destination (Join-Path $taskBuild.artifacts 'windows\source-manifest.json')
    Copy-Item -LiteralPath (Join-Path $OutputDirectory 'source-manifest.json') -Destination (Join-Path $taskBuild.artifacts 'linux\source-manifest.json')
    Compress-Archive -Path (Join-Path $taskBuild.artifacts 'windows\*') -DestinationPath (Join-Path $OutputDirectory 'TuringSignalRelay-windows.zip')
    Invoke-Checked 'tar' @('-czf',(Join-Path $OutputDirectory 'TuringSignalRelay-linux.tar.gz'),'-C',(Join-Path $taskBuild.artifacts 'linux'),'.')
    Invoke-Checked $Python @('tools/package-release.py','--directory',$OutputDirectory,'--repository',$taskRoot)
    Invoke-Checked $Python @('tools/verify-delivery.py','--directory',$OutputDirectory,'--project-copy',$taskBuild.project)
    $taskPackages = @()
    foreach ($taskName in @('TuringSignalRelay-windows.zip','TuringSignalRelay-linux.tar.gz')) {
        $taskFile = Get-Item -LiteralPath (Join-Path $OutputDirectory $taskName)
        $taskPackages += [ordered]@{ name=$taskName; bytes=$taskFile.Length; sha256=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    $taskFormalPackages = Get-Content -LiteralPath (Join-Path $OutputDirectory 'release-packages.json') -Raw | ConvertFrom-Json
    $taskPackages += $taskFormalPackages.packages
    $taskReport = [ordered]@{
        status = 'passed'
        sourceCommit = $taskGitCommit
        sourceModified = $taskDirty
        teamEditor = $taskVersion
        buildEditor = $taskBuild.editorVersion
        publicSource = (!$IncludeLocalArt)
        workerChecks = 'worker-checks/CHECKS.json'
        observerAcceptance = 'observer-acceptance.json'
        observerNetwork = 'observer-network.json'
        windowsHeadless = 'windows-headless/CHECKS.json'
        fullGame = 'full-game/CHECKS.json'
        windowsFullGame = 'windows-full-game/CHECKS.json'
        ubuntuValidation = 'separate-target-report'
        ubuntuReport = 'UBUNTU.json'
        packages = $taskPackages
    }
    $taskPending = Join-Path $OutputDirectory 'DELIVERY.pending.json'
    $taskReport | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $taskPending -Encoding utf8
    @"
# Turing Signal Relay交付

源码提交为$taskGitCommit。团队Editor为$taskVersion，本次构建Editor为$($taskBuild.editorVersion)。

Windows压缩包解压到新目录后运行TuringSignalRelay.exe。初始显示观战fixture，从打开回放入口加载examples/game-1.replay.json或game-2.replay.json即可观看完整9000tick真实AI双局。支持播放、单步、跳转和视角切换。

TuringSignalRelay-linux.tar.gz为Unity Linux原生裁判，TuringSignalRelay-server-linux.tar.gz包含正式.NET平台/Worker、SDK、规则与部署脚本，TuringSignalRelay-sdk.zip提供选手接口与样例，TuringSignalRelay-source.zip保存本次审计的公开源码快照。正式Worker在Ubuntu使用nsjail隔离运行AI，安装按服务包deploy/README.md进行，需要.NET10及对应构建依赖。

DELIVERY.json记录包摘要，source-manifest.json记录源码摘要，BUILD-INFO.json记录版本与素材模式。UBUNTU.json在目标服务器实测通过后由check-ubuntu-package.py单独生成。

完整命令见仓库docs/RELEASE.md。本次已包含完整玩法、C++样例、正式平台与Worker，已有Ubuntu沙箱和数据库验收记录随文档提供。旧素材所有权已由用户于2026-11-07确认，默认包含选定精灵，IncludeLocalArt另包含本地模型和材质。

团队Unity基线保持$taskVersion，独立副本使用$($taskBuild.editorVersion)。原版本导入/构建与干净Ubuntu从零安装的验收状态以仓库最新发布记录为准。
"@ | Set-Content -LiteralPath (Join-Path $OutputDirectory 'README.md') -Encoding utf8
    Move-Item -LiteralPath $taskPending -Destination (Join-Path $OutputDirectory 'DELIVERY.json')
    Write-Output "DELIVERY_VERIFIED $OutputDirectory"
} catch {
    [ordered]@{ status='failed'; error=$_.Exception.Message } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'FAILURE.json') -Encoding utf8
    throw
} finally {
    $env:DOTNET_ROOT = $taskOldDotnetRoot
    Pop-Location
}
