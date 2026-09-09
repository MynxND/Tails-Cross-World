[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ManagedDirectory,

    [Parameter(Mandatory)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$managed = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ManagedDirectory).Path)
$output = [IO.Path]::GetFullPath($OutputPath)
$managedPrefix = $managed.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if ($output.Equals($managed, [StringComparison]::OrdinalIgnoreCase) -or
    $output.StartsWith($managedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputPath must be outside the managed reference directory.'
}

function Read-AssemblyMetadata([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
        try {
            if (-not $pe.HasMetadata) { throw 'PE file has no CLI metadata.' }
            $reader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
            $assemblyName = $null
            if ($reader.IsAssembly) {
                $assemblyName = $reader.GetString($reader.GetAssemblyDefinition().Name)
            }
            $types = foreach ($handle in $reader.TypeDefinitions) {
                $definition = $reader.GetTypeDefinition($handle)
                $name = $reader.GetString($definition.Name)
                if ($name -eq '<Module>') { continue }
                $methods = @($definition.GetMethods() | ForEach-Object {
                    $methodName = $reader.GetString($reader.GetMethodDefinition($_).Name)
                    if (-not ($methodName.StartsWith('get_') -or $methodName.StartsWith('set_'))) { $methodName }
                } | Sort-Object -Unique)
                $fields = @($definition.GetFields() | ForEach-Object {
                    $reader.GetString($reader.GetFieldDefinition($_).Name)
                } | Sort-Object -Unique)
                [ordered]@{
                    namespace = $reader.GetString($definition.Namespace)
                    name = $name
                    methods = $methods
                    fields = $fields
                }
            }
            [ordered]@{
                file = [IO.Path]::GetFileName($Path)
                assembly_name = $assemblyName
                types = @($types | Sort-Object namespace, name)
                error = $null
            }
        }
        finally { $pe.Dispose() }
    }
    catch {
        [ordered]@{
            file = [IO.Path]::GetFileName($Path)
            assembly_name = $null
            types = @()
            error = $_.Exception.Message
        }
    }
    finally { $stream.Dispose() }
}

$assemblies = @(Get-ChildItem -LiteralPath $managed -Filter '*.dll' -File |
    Sort-Object Name | ForEach-Object { Read-AssemblyMetadata $_.FullName })
$report = [ordered]@{
    schema_version = 1
    generated_at_utc = [DateTimeOffset]::UtcNow.ToString('o')
    assemblies = $assemblies
}
$parent = Split-Path -Parent $output
if ($parent) { [IO.Directory]::CreateDirectory($parent) | Out-Null }
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $output -Encoding UTF8
Write-Host "Wrote metadata for $($assemblies.Count) assemblies to $output"
