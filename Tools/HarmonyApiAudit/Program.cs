using System.Reflection;
using System.Security.Cryptography;

if (args.Length == 1 && args[0] == "--self-test") return AuditSelfTests.Run();
if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: HarmonyApiAudit <UFO.dll> <isolated reference root> [--support-dir DIR] [--implementation-dir DIR] [--inspect TYPE [MEMBER]]");
    return 64;
}
try
{
    var options = AuditOptions.Parse(args);
    using var environment = new AuditEnvironment(options);
    if (options.InspectType is not null) return environment.Inspect(options.InspectType, options.InspectMember);
    var findings = new List<string>();
    if (options.WithoutDlc && (environment.ModAssembly.GetReferencedAssemblies().Any(assembly => assembly.Name?.StartsWith("NavalDLC", StringComparison.OrdinalIgnoreCase) == true) ||
        environment.ModAssembly.GetType("UFO.Patching.NavalDlcCompatibility", false) is not null))
        findings.Add("EDITION: no-War-Sails binary contains a NavalDLC dependency or integration class.");
    int checkedPatches = 0, checkedNaval = 0;
    foreach (var type in environment.ModAssembly.GetTypes())
    {
        var classAttributes = type.GetCustomAttributesData().Where(AuditHelpers.IsHarmonyPatch).ToArray();
        var methods = type.GetMethods(AuditHelpers.AllMembers);
        var patchMethods = methods.Where(AuditHelpers.IsPatchMethod).ToArray();
        if (classAttributes.Length > 0 || patchMethods.Any(method => method.GetCustomAttributesData().Any(AuditHelpers.IsHarmonyPatch)))
        {
            if (methods.Any(method => method.Name is "TargetMethod" or "TargetMethods"))
                findings.Add($"DYNAMIC TARGET: {type.FullName} requires an explicit metadata contract; runtime discovery is not executed by the audit.");
            foreach (var patch in patchMethods)
            {
                var target = new PatchTarget();
                foreach (var attribute in classAttributes.Concat(patch.GetCustomAttributesData().Where(AuditHelpers.IsHarmonyPatch))) target.Apply(attribute);
                checkedPatches++;
                var original = target.Resolve(out var issue);
                if (original is null || issue is not null) { findings.Add($"TARGET: {type.FullName}.{patch.Name} -> {target.Describe()}: {issue ?? "member not found"}"); continue; }
                AuditHelpers.ValidateParameters(patch, original, findings);
            }
        }
        foreach (var callback in methods)
            foreach (var attribute in callback.GetCustomAttributesData().Where(attribute => attribute.AttributeType.FullName == "UFO.Patching.NavalPatchTargetAttribute"))
            {
                checkedNaval++;
                var typeName = (string)attribute.ConstructorArguments[0].Value!;
                var methodName = (string)attribute.ConstructorArguments[1].Value!;
                var originalType = environment.FindType(typeName);
                if (originalType is null) { findings.Add($"NAVAL TYPE: {typeName} not found."); continue; }
                var originals = originalType.GetMethods(AuditHelpers.AllMembers).Where(method => method.Name == methodName && AuditHelpers.BindingsMatch(callback, method)).ToArray();
                if (originals.Length != 1) { findings.Add($"NAVAL TARGET: {typeName}.{methodName} has {originals.Length} compatible overloads for {callback.Name}."); continue; }
                AuditHelpers.ValidateParameters(callback, originals[0], findings);
            }
    }
    var reflected = ReflectedMemberAudit.Run(environment, findings, checkedNaval > 0);
    var memberRefs = MemberReferenceAudit.Run(environment, findings);
    var ilChecks = options.ImplementationRoot is null ? 0 : ImplementationAudit.Run(environment, findings, checkedNaval > 0);
    Console.WriteLine($"Assembly: {environment.ModAssembly.GetName().FullName}");
    Console.WriteLine($"Assembly SHA256: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(options.ModPath)))}");
    Console.WriteLine($"Reference root: {options.ReferenceRoot}");
    if (options.WithoutDlc) Console.WriteLine("NavalDLC assemblies excluded from metadata resolution.");
    Console.WriteLine($"Harmony callbacks checked: {checkedPatches}; Naval targets: {checkedNaval}; reflected contracts: {reflected}; direct game MemberRefs: {memberRefs}; IL contracts: {ilChecks}");
    if (options.ImplementationRoot is null) Console.WriteLine("IL contracts not verified: metadata references do not establish transpiler or forwarding behavior.");
    foreach (var finding in findings) Console.WriteLine(finding);
    Console.WriteLine($"Incompatibilities: {findings.Count}");
    return findings.Count == 0 ? 0 : 1;
}
catch (Exception exception) { Console.Error.WriteLine($"AUDIT ERROR: {exception}"); return 2; }

sealed record AuditOptions(string ModPath, string ReferenceRoot, string[] SupportRoots, string? ImplementationRoot, string? InspectType, string? InspectMember, bool WithoutDlc)
{
    public static AuditOptions Parse(string[] args)
    {
        string mod = Path.GetFullPath(args[0]), references = Path.GetFullPath(args[1]);
        if (!File.Exists(mod) || !Directory.Exists(references)) throw new ArgumentException("Mod assembly or selected reference root does not exist.");
        var support = new List<string>();
        string? implementation = null, type = null, member = null;
        bool withoutDlc = false;
        for (var index = 2; index < args.Length; index++)
            switch (args[index])
            {
                case "--support-dir": support.Add(Path.GetFullPath(args[++index])); break;
                case "--implementation-dir": implementation = Path.GetFullPath(args[++index]); break;
                case "--without-dlc": withoutDlc = true; break;
                case "--inspect": type = args[++index]; if (index + 1 < args.Length && !args[index + 1].StartsWith("--")) member = args[++index]; break;
                default:
                    if (type is null && !args[index].StartsWith("--")) { type = args[index]; if (index + 1 < args.Length) member = args[++index]; }
                    else throw new ArgumentException($"Unknown option {args[index]}.");
                    break;
            }
        foreach (var root in support.Append(implementation).Where(root => root is not null))
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        return new(mod, references, support.ToArray(), implementation, type, member, withoutDlc);
    }
}

sealed class AuditEnvironment : IDisposable
{
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);
    public AuditOptions Options { get; }
    public MetadataLoadContext Context { get; }
    public Assembly ModAssembly { get; }
    public AuditEnvironment(AuditOptions options)
    {
        Options = options;
        foreach (var root in options.SupportRoots.Prepend(options.ReferenceRoot))
            foreach (var path in OfficialFiles(root).Where(path => !options.WithoutDlc || !Path.GetFileName(path).StartsWith("NavalDLC", StringComparison.OrdinalIgnoreCase))) AddPath(path);
        // Only explicitly selected snapshots and dependencies supply game assemblies.
        foreach (var path in ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)) AddPath(path, framework: true);
        AddPath(options.ModPath);
        Context = new(new PathAssemblyResolver(_paths.Values), "System.Private.CoreLib");
        ModAssembly = Context.LoadFromAssemblyPath(options.ModPath);
    }
    public static IEnumerable<string> OfficialFiles(string root)
    {
        // Exclude unrelated modules when inspecting a full installed game.
        if (Directory.Exists(Path.Combine(root, "Modules")) && Directory.Exists(Path.Combine(root, "bin")))
        {
            string platform = Directory.Exists(Path.Combine(root, "bin", "Win64_Shipping_Client")) ? "Win64_Shipping_Client" : "Gaming.Desktop.x64_Shipping_Client";
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "bin", platform), "*.dll", SearchOption.TopDirectoryOnly)) yield return path;
            foreach (var module in new[] { "Native", "SandBox", "SandBoxCore", "StoryMode", "CustomBattle", "BirthAndDeath", "NavalDLC" })
            {
                var directory = Path.Combine(root, "Modules", module, "bin", platform);
                if (Directory.Exists(directory)) foreach (var path in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly)) yield return path;
            }
        }
        else foreach (var path in Directory.EnumerateFiles(root, "*.dll", SearchOption.AllDirectories)) yield return path;
    }
    private void AddPath(string path, bool framework = false)
    {
        AssemblyName identity;
        try { identity = AssemblyName.GetAssemblyName(path); } catch (BadImageFormatException) { return; }
        var name = identity.Name!;
        if (framework)
        {
            if (name is "mscorlib" or "netstandard" or "System" || name.StartsWith("System.", StringComparison.Ordinal)) _paths[name] = path;
            else _paths.TryAdd(name, path);
            return;
        }
        if (_paths.TryGetValue(name, out var previous))
        {
            if (!File.ReadAllBytes(previous).AsSpan().SequenceEqual(File.ReadAllBytes(path)))
                throw new InvalidOperationException($"Conflicting assemblies named {name}: {previous} and {path}. Select one snapshot and dependency framework per directory.");
            return;
        }
        _paths.Add(name, path);
    }
    public Type? FindType(string name)
    {
        foreach (var path in _paths.Values)
        {
            try { var type = Context.LoadFromAssemblyPath(path).GetType(name, false); if (type is not null) return type; }
            catch (FileNotFoundException) { } catch (FileLoadException) { }
        }
        return null;
    }
    public int Inspect(string name, string? member)
    {
        var matches = _paths.Values.Select(path => Context.LoadFromAssemblyPath(path)).SelectMany(assembly => assembly.GetTypes()).Where(type => type.FullName == name || type.Name == name).ToArray();
        if (matches.Length == 0) { Console.WriteLine($"TYPE NOT FOUND: {name}"); return 1; }
        foreach (var type in matches)
        {
            Console.WriteLine($"TYPE: {type.FullName} ({type.Assembly.GetName().Name})");
            foreach (var value in type.GetMembers(AuditHelpers.AllMembers).Where(value => member is null || value.Name == member)) Console.WriteLine($"MEMBER: {value}");
        }
        return 0;
    }
    public void Dispose() => Context.Dispose();
}
