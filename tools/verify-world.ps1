param([string]$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskOutput = Join-Path $taskRoot 'artifacts\checks'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskSources = @(
    'game/Assets/Game/Core/GridPrimitives.cs',
    'game/Assets/Game/World/GridPathfinder.cs',
    'game/Assets/Game/Presentation/ObserverEventLog.cs',
    'tools/WorldCheckMain.cs'
) | ForEach-Object { Join-Path $taskRoot $_ }
$taskExe = Join-Path $taskOutput 'WorldChecks.exe'
& $Compiler /nologo /target:exe "/out:$taskExe" $taskSources
if ($LASTEXITCODE -ne 0) { throw 'World checks compilation failed.' }
& $taskExe
if ($LASTEXITCODE -ne 0) { throw 'World checks failed.' }
