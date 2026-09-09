using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: AssemblyInspector <managed-directory> [output.json]");
    return 2;
}

var root = Path.GetFullPath(args[0]);
if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"Managed directory does not exist: {root}");
    return 2;
}

var assemblies = new List<AssemblyInventory>();
foreach (var path in Directory.EnumerateFiles(root, "*.dll").Order(StringComparer.OrdinalIgnoreCase))
{
    try
    {
        assemblies.Add(Inspect(path));
    }
    catch (BadImageFormatException exception)
    {
        assemblies.Add(new AssemblyInventory(Path.GetFileName(path), null, [], exception.Message));
    }
}

var report = new Report(1, DateTimeOffset.UtcNow, assemblies);
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
if (args.Length == 2)
{
    var output = Path.GetFullPath(args[1]);
    if (IsWithin(output, root))
    {
        Console.Error.WriteLine("Output must be outside the managed reference directory.");
        return 2;
    }
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output, json + Environment.NewLine);
}
else
{
    Console.WriteLine(json);
}
return 0;

static bool IsWithin(string candidate, string directory)
{
    var relative = Path.GetRelativePath(directory, candidate);
    return relative == "." || (!relative.StartsWith(".." + Path.DirectorySeparatorChar) && relative != "..");
}

static AssemblyInventory Inspect(string path)
{
    using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
    if (!pe.HasMetadata)
        throw new BadImageFormatException("PE file has no CLI metadata.");

    var reader = pe.GetMetadataReader();
    string? assemblyName = reader.IsAssembly ? reader.GetString(reader.GetAssemblyDefinition().Name) : null;
    var types = new List<TypeInventory>();
    foreach (var handle in reader.TypeDefinitions)
    {
        var definition = reader.GetTypeDefinition(handle);
        var name = reader.GetString(definition.Name);
        if (name == "<Module>") continue;
        var ns = reader.GetString(definition.Namespace);
        var methods = definition.GetMethods()
            .Select(h => reader.GetString(reader.GetMethodDefinition(h).Name))
            .Where(n => !n.StartsWith("get_", StringComparison.Ordinal) && !n.StartsWith("set_", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var fields = definition.GetFields()
            .Select(h => reader.GetString(reader.GetFieldDefinition(h).Name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        types.Add(new TypeInventory(ns, name, methods, fields));
    }
    return new AssemblyInventory(Path.GetFileName(path), assemblyName, types.OrderBy(t => t.Namespace).ThenBy(t => t.Name).ToArray(), null);
}

record Report(int SchemaVersion, DateTimeOffset GeneratedAtUtc, IReadOnlyList<AssemblyInventory> Assemblies);
record AssemblyInventory(string File, string? AssemblyName, IReadOnlyList<TypeInventory> Types, string? Error);
record TypeInventory(string Namespace, string Name, IReadOnlyList<string> Methods, IReadOnlyList<string> Fields);
