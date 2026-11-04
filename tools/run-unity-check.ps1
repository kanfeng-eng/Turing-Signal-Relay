param(
    [Parameter(Mandatory=$true)][string]$EditorDirectory,
    [ValidateSet('Check','Windows','LinuxServer')][string]$Action = 'Check',
    [switch]$DevelopmentCopy
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskProject = Join-Path $taskRoot 'game'
$taskEditor = Join-Path $EditorDirectory 'Unity.exe'
$taskVersion = (Get-Content (Join-Path $taskProject 'ProjectSettings\ProjectVersion.txt') |
    Select-String '^m_EditorVersion: (.+)$').Matches.Groups[1].Value
$taskActualVersion = (Get-Item -LiteralPath $taskEditor).VersionInfo.ProductVersion.Split('_')[0]
$taskArtifacts = Join-Path $taskRoot 'artifacts'
if ($DevelopmentCopy) {
    if ($taskActualVersion -ne '6000.6.8f1') { throw 'Development copy requires Unity 6000.6.8f1.' }
    # Each run gets a fresh copy, avoiding stale source and never upgrading the team project.
    $taskCopy = Join-Path $taskArtifacts ('development-6000.6.8f1-' + [guid]::NewGuid().ToString('N'))
    $taskProjectCopy = Join-Path $taskCopy 'game'
    New-Item -ItemType Directory -Force -Path $taskProjectCopy | Out-Null
    Copy-Item -LiteralPath (Join-Path $taskProject 'Assets') -Destination $taskProjectCopy -Recurse
    foreach ($taskFolder in @('Packages','ProjectSettings')) {
        $taskDestination = Join-Path $taskProjectCopy $taskFolder
        New-Item -ItemType Directory -Force -Path $taskDestination | Out-Null
        $taskFiles = & git -C $taskRoot ls-files -- "game/$taskFolder"
        if ($LASTEXITCODE -ne 0) { throw 'Cannot determine tracked project settings.' }
        foreach ($taskFile in $taskFiles) {
            Copy-Item -LiteralPath (Join-Path $taskRoot $taskFile) -Destination $taskDestination
        }
    }
    Set-Content -LiteralPath (Join-Path $taskProjectCopy 'ProjectSettings\ProjectVersion.txt') `
        -Value 'm_EditorVersion: 6000.6.8f1' -Encoding ASCII
    $taskProject = $taskProjectCopy
    $taskArtifacts = Join-Path $taskCopy 'artifacts'
} elseif ($taskActualVersion -ne $taskVersion) {
    throw "Expected Unity $taskVersion, found $taskActualVersion. Use -DevelopmentCopy for isolated testing."
}
$taskMethods = @{
    Check = 'TuringSignalRelay.Editor.SmokeChecks.Run'
    Windows = 'TuringSignalRelay.Editor.BuildGame.Windows'
    LinuxServer = 'TuringSignalRelay.Editor.BuildGame.LinuxServer'
}
$taskMarkers = @{
    Check = 'BOOTSTRAP_CHECKS_PASSED'
    Windows = 'BOOTSTRAP_BUILD_COMPLETED StandaloneWindows64'
    LinuxServer = 'BOOTSTRAP_BUILD_COMPLETED StandaloneLinux64'
}
New-Item -ItemType Directory -Force -Path $taskArtifacts | Out-Null
$taskLog = Join-Path $taskArtifacts ("unity-" + $Action.ToLowerInvariant() + '.log')
$taskArguments = @('-batchmode','-nographics','-quit','-projectPath',('"'+$taskProject+'"'),
    '-executeMethod',$taskMethods[$Action],'-logFile',('"'+$taskLog+'"'))
if ($DevelopmentCopy) { $taskArguments += '-bootstrapDevelopmentCopy' }
Write-Output "Project: $taskProject"
if ($DevelopmentCopy -and $Action -ne 'Check') {
    $taskCheckLog = Join-Path $taskArtifacts 'unity-check.log'
    $taskCheckArguments = @('-batchmode','-nographics','-quit','-projectPath',('"'+$taskProject+'"'),
        '-executeMethod',$taskMethods.Check,'-logFile',('"'+$taskCheckLog+'"'),'-bootstrapDevelopmentCopy')
    $taskCheckProcess = Start-Process -FilePath $taskEditor -ArgumentList $taskCheckArguments -WindowStyle Hidden -PassThru -Wait
    if ($taskCheckProcess.ExitCode -ne 0 -or
        !(Select-String -LiteralPath $taskCheckLog -SimpleMatch $taskMarkers.Check -Quiet)) {
        throw "Development copy checks failed. Inspect $taskCheckLog"
    }
    Write-Output 'PASS: development copy imported and bootstrap checks passed.'
}
$taskProcess = Start-Process -FilePath $taskEditor -ArgumentList $taskArguments -WindowStyle Hidden -PassThru -Wait
if ($taskProcess.ExitCode -ne 0) {
    throw "Unity $Action failed with exit code $($taskProcess.ExitCode). Inspect $taskLog"
}
if (!(Select-String -LiteralPath $taskLog -SimpleMatch $taskMarkers[$Action] -Quiet)) {
    throw "Unity exited without the required completion marker. Inspect $taskLog"
}
Write-Output "PASS: Unity $Action; exit 0 and completion marker verified. Log: $taskLog"
