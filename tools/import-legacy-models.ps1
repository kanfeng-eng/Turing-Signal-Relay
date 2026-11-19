param([Parameter(Mandatory=$true)][string]$SourceDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskSource = (Resolve-Path -LiteralPath $SourceDirectory).Path
$taskDestination = Join-Path $taskRoot 'game\Assets\Game\Art\LegacyModels'
$taskModels = [ordered]@{
    'angle road 01.fbx' = 'angle road 01.fbx'
    'angle road 02.fbx' = 'angle road 02.fbx'
    'corner.fbx' = 'corner.fbx'
    'cross road plus 1.fbx' = 'cross road plus 1.fbx'
    'straight road plus.fbx' = 'straight road plus.fbx'
    'straight.fbx' = 'straight.fbx'
    'T road.fbx' = 'T road.fbx'
    'no road.fbx' = 'no road.fbx'
    'wall1.fbx' = 'wall1.fbx'
    'book.fbx' = 'book.fbx'
    'police.fbx' = 'New\Police.fbx'
    'crime.fbx' = 'New\Bailing.fbx'
    'student.fbx' = 'New\Student.fbx'
}
foreach ($taskRelative in $taskModels.Values) {
    if (!(Test-Path -LiteralPath (Join-Path $taskSource $taskRelative) -PathType Leaf)) {
        throw "Missing source model: $taskRelative"
    }
}
New-Item -ItemType Directory -Force -Path $taskDestination | Out-Null
foreach ($taskEntry in $taskModels.GetEnumerator()) {
    $taskInput = Join-Path $taskSource $taskEntry.Value
    $taskOutput = Join-Path $taskDestination $taskEntry.Key
    if (Test-Path -LiteralPath $taskOutput) {
        if ((Get-FileHash -LiteralPath $taskInput).Hash -ne (Get-FileHash -LiteralPath $taskOutput).Hash) {
            throw "Destination changed: $taskOutput. Review the existing file before replacing it."
        }
    } else { Copy-Item -LiteralPath $taskInput -Destination $taskOutput }
}
Write-Output 'Imported 13 local model sources. Bake them with TuringSignalRelay.Editor.LegacyArt.Prepare in an isolated Editor copy.'
