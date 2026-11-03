param(
    [Parameter(Mandatory=$true)][string]$Compiler,
    [string]$Mono
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskOutput = Join-Path $taskRoot 'artifacts\checks'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskExe = Join-Path $taskOutput 'BootstrapChecks.exe'
$taskSources = @(
    'game/Assets/Game/Core/Models.cs',
    'game/Assets/Game/Core/TickClock.cs',
    'game/Assets/Game/Integration/MatchEngine.cs',
    'game/Assets/Game/Tests/Core/BootstrapChecks.cs',
    'tools/BootstrapCheckMain.cs'
) | ForEach-Object { Join-Path $taskRoot $_ }
& $Compiler /nologo /target:exe "/out:$taskExe" $taskSources
if ($LASTEXITCODE -ne 0) { throw 'C# compilation failed.' }
if ($Mono) { & $Mono $taskExe } else { & $taskExe }
if ($LASTEXITCODE -ne 0) { throw 'Bootstrap checks failed.' }
