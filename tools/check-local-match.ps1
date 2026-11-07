param([string]$Dotnet = 'dotnet', [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $taskRoot ('artifacts\local-match-' + [guid]::NewGuid().ToString('N')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new output directory.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
& "$PSScriptRoot\build-sample-ai.ps1" -OutputDirectory (Join-Path $OutputDirectory 'cpp') -IncludeFaultFixtures
& $Dotnet publish (Join-Path $PSScriptRoot 'RelayRunner/RelayRunner.csproj') -c Release -o (Join-Path $OutputDirectory 'runner') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Runner build failed.' }
$taskRunner = Join-Path $OutputDirectory 'runner/RelayRunner.dll'
function Invoke-Runner([string[]]$RunnerArguments) {
    & $Dotnet $taskRunner @RunnerArguments
    if ($LASTEXITCODE -ne 0) { throw 'Local match verification failed.' }
}
Invoke-Runner @('check',$taskRoot,(Join-Path $OutputDirectory 'legacy'))
Invoke-Runner @('full-check',$taskRoot,(Join-Path $OutputDirectory 'full'))
Invoke-Runner @('bridge-check',(Join-Path $OutputDirectory 'cpp'))
$taskCourier = Join-Path $OutputDirectory 'cpp/courier.exe'
Invoke-Runner @('series',$taskRoot,$taskCourier,(Join-Path $OutputDirectory 'cpp/transporter.exe'),(Join-Path $OutputDirectory 'series'),'--full-rules')
$taskSeries = Get-Content -Raw (Join-Path $OutputDirectory 'series/series.result.json') | ConvertFrom-Json
if ($taskSeries.status -ne 'completed') { throw 'Sample paired match did not complete.' }
foreach ($taskFault in @('malformed','timeout')) {
    $taskFaultOutput = Join-Path $OutputDirectory $taskFault
    Invoke-Runner @('ai',$taskRoot,(Join-Path $OutputDirectory "cpp/$taskFault.exe"),$taskCourier,$taskFaultOutput,'--full-rules')
    $taskReport = Get-Content -Raw (Join-Path $taskFaultOutput 'ai.report.json') | ConvertFrom-Json
    if (!$taskReport.forfeit -or $taskReport.faultSide -ne 0 -or $taskReport.completed) { throw 'Participant fault adjudication failed.' }
    if ($taskFault -eq 'timeout' -and $taskReport.timeouts[0] -ne 3) { throw 'Three-timeout rule failed.' }
}
[ordered]@{ status='passed'; fullRuleAssertions=76; bridgeCases=9; pairedGames=2; participantFaultCases=2; cpuBudgetEnforced=$false } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'CHECKS.json') -Encoding utf8
Write-Output "LOCAL_MATCH_CHECKS_PASSED $OutputDirectory"
