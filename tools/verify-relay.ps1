param(
    [string]$Dotnet = 'dotnet',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $taskRoot ('artifacts\relay-' + [guid]::NewGuid().ToString('N')) }
& $Dotnet run --project (Join-Path $PSScriptRoot 'RelayRunner\RelayRunner.csproj') -- check $taskRoot $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Relay stages 1-3 checks failed.' }
Write-Output "Replay and result: $OutputDirectory"
