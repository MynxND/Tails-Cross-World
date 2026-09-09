[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $root
try {
    $bundledPython = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    $pythonPath = if (Test-Path -LiteralPath $bundledPython) {
        $bundledPython
    } else {
        $command = Get-Command py -ErrorAction SilentlyContinue
        if (-not $command) { $command = Get-Command python -ErrorAction SilentlyContinue }
        if ($command) { $command.Source }
    }
    if (-not $pythonPath) {
        throw 'Python 3.10 or newer is required. Install it from https://www.python.org/downloads/windows/'
    }
    & $pythonPath -m unittest discover -s tests -v
    if ($LASTEXITCODE -ne 0) { throw 'Unit tests failed.' }
    & $pythonPath tools/check_public_tree.py
    if ($LASTEXITCODE -ne 0) { throw 'Public-tree policy failed.' }
    Write-Host 'All Phase 0-1 checks passed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
