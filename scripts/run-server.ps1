[CmdletBinding()]
param([string]$HostAddress = '0.0.0.0', [int]$Port = 12712)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $root
try {
    $bundled = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    $python = if (Test-Path $bundled) { $bundled } else { (Get-Command python -ErrorAction Stop).Source }
    & $python -m src.server.lan_server --host $HostAddress --port $Port
} finally { Pop-Location }
