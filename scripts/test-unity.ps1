[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'client'
$editorRoot = 'C:\Program Files\Unity\Hub\Editor'
$editor = Get-ChildItem -LiteralPath $editorRoot -Directory |
    Sort-Object { [version]($_.Name -replace '[^0-9.].*$', '') } -Descending |
    Select-Object -First 1
if (-not $editor) { throw 'Unity Editor was not found under Unity Hub.' }
$unity = Join-Path $editor.FullName 'Editor\Unity.exe'
$artifacts = Join-Path $root 'artifacts'
[IO.Directory]::CreateDirectory($artifacts) | Out-Null
$results = Join-Path $artifacts 'unity-editmode-results.xml'
$log = Join-Path $artifacts 'unity-editmode.log'
$arguments = @(
    '-batchmode',
    '-projectPath', "`"$project`"",
    '-runTests',
    '-testPlatform', 'EditMode',
    '-testResults', "`"$results`"",
    '-logFile', "`"$log`""
)
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Unity tests failed with exit code $($process.ExitCode). See $log" }
Write-Host 'Unity EditMode tests passed.' -ForegroundColor Green
