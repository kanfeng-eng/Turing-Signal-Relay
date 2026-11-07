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
    foreach ($taskService in @('Platform','Worker','Worker.Checks')) {
        Invoke-Checked $taskDotnet @('publish',"server/$taskService/$taskService.csproj",'-c','Release','-o',(Join-Path $OutputDirectory "server\$taskService"),'--nologo')
    }
    Invoke-Checked $taskDotnet @((Join-Path $OutputDirectory 'server\Worker.Checks\WorkerChecks.dll'),(Join-Path $OutputDirectory 'worker-checks'))
    # Linux apphost is required by the worker's process-tree fixture.
    Invoke-Checked $taskDotnet @('publish','server/Worker.Checks/Worker.Checks.csproj','-c','Release','-r','linux-x64','--self-contained','false','-o',(Join-Path $OutputDirectory 'server-linux\Worker.Checks'),'--nologo')
    $taskResult = Join-Path $OutputDirectory 'unity-result.json'
    & "$PSScriptRoot\run-unity-check.ps1" -EditorDirectory $EditorDirectory -Action Delivery -DevelopmentCopy:$DevelopmentCopy -PublicSource:(!$IncludeLocalArt) -ResultFile $taskResult
    $taskBuild = Get-Content -LiteralPath $taskResult -Raw | ConvertFrom-Json
    & "$PSScriptRoot\verify-unity-api.ps1" -EditorDirectory $ApiEditorDirectory -Compiler $Compiler -UiAssembly (Join-Path $taskBuild.project 'Library\ScriptAssemblies\UnityEngine.UI.dll')
    Invoke-EditorCheck 'TuringSignalRelay.Editor.ObserverAcceptanceChecks.Run' 'OBSERVER_ACCEPTANCE_CHECKS_PASSED' (Join-Path $OutputDirectory 'observer-acceptance.log')
    Copy-Item -LiteralPath (Join-Path $taskBuild.artifacts 'observer-acceptance.json') -Destination $OutputDirectory
    Invoke-Checked $Python @('tools/check-observer-network.py','--editor',(Join-Path $EditorDirectory 'Unity.exe'),'--project',$taskBuild.project,'--log',(Join-Path $OutputDirectory 'observer-network.log'))
    Invoke-Checked $Python @('tools/check-bootstrap-headless.py','--player',(Join-Path $taskBuild.artifacts 'windows\TuringSignalRelay.exe'),'--output',(Join-Path $OutputDirectory 'windows-headless'))
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
    Invoke-Checked $Python @('tools/verify-delivery.py','--directory',$OutputDirectory,'--project-copy',$taskBuild.project)
    $taskPackages = @()
    foreach ($taskName in @('TuringSignalRelay-windows.zip','TuringSignalRelay-linux.tar.gz')) {
        $taskFile = Get-Item -LiteralPath (Join-Path $OutputDirectory $taskName)
        $taskPackages += [ordered]@{ name=$taskName; bytes=$taskFile.Length; sha256=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
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
        ubuntuValidation = 'separate-target-report'
        ubuntuReport = 'UBUNTU.json'
        packages = $taskPackages
    }
    $taskPending = Join-Path $OutputDirectory 'DELIVERY.pending.json'
    $taskReport | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $taskPending -Encoding utf8
    @"
# Turing Signal Relay交付

源码提交为$taskGitCommit。团队Editor为$taskVersion，本次构建Editor为$($taskBuild.editorVersion)。

Windows压缩包解压到新目录后运行TuringSignalRelay.exe。初始显示观战fixture，可播放、单步、跳转和切换视角。Linux包解压后给TuringSignalRelay执行权限，使用-batchmode -nographics --bootstrap-replay参数指定新回放文件。

DELIVERY.json记录包摘要，source-manifest.json记录源码摘要，BUILD-INFO.json记录版本与素材模式。UBUNTU.json在目标服务器实测通过后由check-ubuntu-package.py单独生成。

完整命令见仓库docs/INDEPENDENT_ACCEPTANCE.md。正式地图、规则、AI及平台接口仍需对应模块接入。旧素材所有权已由用户于2026-11-07确认，默认包含选定精灵，IncludeLocalArt另包含本地模型和材质。
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
