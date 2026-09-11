[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$blender = Get-ChildItem -LiteralPath 'C:\Program Files\Blender Foundation' -Filter blender.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $blender) { throw 'Blender was not found under C:\Program Files\Blender Foundation.' }
Push-Location -LiteralPath $root
try {
    & $blender.FullName --background --python '.\tools\blender\generate_characters.py'
    if ($LASTEXITCODE -ne 0) { throw 'Blender character generation failed.' }
} finally { Pop-Location }
