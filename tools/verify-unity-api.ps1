param(
    [Parameter(Mandatory=$true)][string]$EditorDirectory,
    [string]$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
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
$taskSources = Get-ChildItem (Join-Path $taskRoot 'game\Assets\Game') -Recurse -Filter '*.cs' | ForEach-Object FullName
$taskOutput = Join-Path $taskRoot 'artifacts\checks'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
& $Compiler /nologo /noconfig /nostdlib+ /target:library "/out:$taskOutput\UnityApiCheck.dll" $taskReferences $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Unity API reference compilation failed.' }
Write-Output 'PASS: source compiles against installed Unity API references; Editor import and runtime are not tested.'
