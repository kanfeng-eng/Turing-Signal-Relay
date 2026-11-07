param([Parameter(Mandatory=$true)][string]$OutputDirectory, [string]$VcVars, [switch]$IncludeFaultFixtures)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new compiler output directory.' }
if (!$VcVars) {
    $taskVsWhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
    $taskVs = & $taskVsWhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if (!$taskVs) { throw 'A Visual Studio C++ toolchain with C++17 support is required.' }
    $VcVars = Join-Path $taskVs 'VC\Auxiliary\Build\vcvars64.bat'
}
if (!(Test-Path -LiteralPath $VcVars)) { throw 'Missing compiler environment script.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
foreach ($taskPath in @($taskRoot, $OutputDirectory, $VcVars)) {
    if ($taskPath -match '[%"\r\n]') { throw 'Compiler paths cannot contain cmd expansion characters.' }
}
$taskCommands = @('@echo off', 'chcp 65001 >nul', 'setlocal DisableDelayedExpansion', ('call "' + $VcVars + '" >nul'), 'if errorlevel 1 exit /b 1')
foreach ($taskName in @('courier','transporter')) {
    $taskSource = Join-Path $taskRoot "sdk\cpp\examples\$taskName.cpp"
    $taskExe = Join-Path $OutputDirectory "$taskName.exe"
    $taskObj = Join-Path $OutputDirectory "$taskName.obj"
    $taskCommands += 'cl /nologo /std:c++17 /O2 /EHsc /MT /utf-8 /DNOMINMAX /Fo"' + $taskObj + '" /Fe"' + $taskExe + '" "' + $taskSource + '"'
    $taskCommands += 'if errorlevel 1 exit /b 1'
}
$taskScript = Join-Path $OutputDirectory 'build.cmd'
if ($IncludeFaultFixtures) {
    $taskFaults = @('malformed','oversize','crash','timeout','late-once','memory','stderr','future-tick','cpu')
    for ($taskMode = 0; $taskMode -lt $taskFaults.Count; $taskMode++) {
        $taskName = $taskFaults[$taskMode]
        $taskSource = Join-Path $taskRoot 'sdk\cpp\tests\faults.cpp'
        $taskExe = Join-Path $OutputDirectory "$taskName.exe"
        $taskObj = Join-Path $OutputDirectory "$taskName.obj"
        $taskCommands += 'cl /nologo /std:c++17 /O2 /EHsc /MT /utf-8 /DNOMINMAX /DFAULT_MODE=' + $taskMode + ' /Fo"' + $taskObj + '" /Fe"' + $taskExe + '" "' + $taskSource + '"'
        $taskCommands += 'if errorlevel 1 exit /b 1'
    }
}
[IO.File]::WriteAllLines($taskScript, $taskCommands, [Text.Encoding]::Default)
& $env:ComSpec /d /c $taskScript
if ($LASTEXITCODE -ne 0) { throw 'C++17 sample compilation failed.' }
Write-Output 'SAMPLE_AI_BUILD_PASSED'
