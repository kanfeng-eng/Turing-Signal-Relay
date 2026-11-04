param(
    [Parameter(Mandatory=$true)][string]$EditorDirectory,
    [string]$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
    [string]$UiAssembly
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskManaged = Join-Path $EditorDirectory 'Data\Managed'
$taskStandard = Join-Path $EditorDirectory 'Data\NetStandard\ref\2.1.0\netstandard.dll'
if (!(Test-Path -LiteralPath $taskStandard)) { throw 'Expected Unity .NET Standard 2.1 reference assembly.' }
$taskReferences = Get-ChildItem -LiteralPath $taskManaged -Recurse -Filter '*.dll' |
    Where-Object Name -Match '^Unity(Engine|Editor)\.' |
    Group-Object Name | ForEach-Object {
        # Prefer modular references in Managed/UnityEngine over root compatibility DLLs.
        $taskReference = $_.Group | Sort-Object { $_.FullName.Length } -Descending | Select-Object -First 1
        '/reference:' + $taskReference.FullName
    }
$taskReferences += '/reference:' + $taskStandard
if (!$UiAssembly) { $UiAssembly = Join-Path $taskRoot 'game\Library\ScriptAssemblies\UnityEngine.UI.dll' }
if (!(Test-Path -LiteralPath $UiAssembly)) { throw 'Pass -UiAssembly pointing to UnityEngine.UI.dll imported from the pinned com.unity.ugui 2.0.0 package.' }
$taskReferences += '/reference:' + $UiAssembly
$taskSources = Get-ChildItem (Join-Path $taskRoot 'game\Assets\Game') -Recurse -Filter '*.cs' | ForEach-Object FullName
$taskOutput = Join-Path $taskRoot 'artifacts\checks'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
& $Compiler /nologo /noconfig /nostdlib+ /target:library "/out:$taskOutput\UnityApiCheck.dll" $taskReferences $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Unity API reference compilation failed.' }
Write-Output 'PASS: source compiles against installed Unity API references; Editor import and runtime are not tested.'
