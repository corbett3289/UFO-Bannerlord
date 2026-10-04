using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: MaintainedRegressionTests <UFO.dll> <runtime-assembly-directory> [...]");
    return 2;
}

var dependencyPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (string root in args.Skip(1))
{
    foreach (string path in Directory.EnumerateFiles(root, "*.dll", SearchOption.AllDirectories))
        dependencyPaths.TryAdd(Path.GetFileNameWithoutExtension(path), path);
}
AssemblyLoadContext.Default.Resolving += (_, name) =>
    name.Name != null && dependencyPaths.TryGetValue(name.Name, out string? path)
        ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path))
        : null;

Assembly ufo = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[0]));
const BindingFlags allStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
int checks = 0;
void Check(bool condition, string scenario)
{
    checks++;
    if (!condition) throw new InvalidOperationException("Regression failed: " + scenario);
}
Type RequiredType(string name) => ufo.GetType(name, true)!;
MethodInfo RequiredMethod(Type type, string name, params Type[] parameters) =>
    type.GetMethod(name, allStatic, null, parameters, null)
        ?? throw new MissingMethodException(type.FullName, name);

try
{
    Type submodule = RequiredType("UFO.SubModule");
    MethodInfo versionGate = RequiredMethod(submodule, "IsSupportedGameVersion", typeof(int), typeof(int), typeof(int));
    foreach (var test in new[]
    {
        (1, 5, 0, false), (1, 5, 1, false), (1, 5, 2, false), (1, 5, 3, true),
        (1, 5, 4, false), (1, 4, 8, false), (1, 6, 0, false), (2, 5, 1, false)
    })
        Check((bool)versionGate.Invoke(null, new object[] { test.Item1, test.Item2, test.Item3 })! == test.Item4,
            $"supported version {test.Item1}.{test.Item2}.{test.Item3}");

    Type settings = RequiredType("UFO.Setting.SettingsManager");
    bool navalEdition = ufo.GetType("UFO.Patching.NavalDlcCompatibility") != null;
    foreach (string settingsType in new[]
    {
        "UFO.Setting.SettingsManager", "UFO.Setting.BannerlordCheatsGlobalSettings",
        "UFO.Setting.BannerlordCheatsPerCampaignSettings"
    })
        Check((RequiredType(settingsType).GetProperty("NavalCampaignSpeedMultiplier",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static) != null) == navalEdition,
            $"naval settings edition isolation: {settingsType}");
    Check(settings.GetProperty("TestMode", BindingFlags.Public | BindingFlags.Static) != null,
        "non-naval TestMode remains in both editions");
    MethodInfo boolResolver = RequiredMethod(settings, "ResolveAuthoritativeCampaignBool", typeof(bool), typeof(bool), typeof(bool));
    foreach (bool loaded in new[] { false, true })
    foreach (bool local in new[] { false, true })
    foreach (bool global in new[] { false, true })
    {
        object result = boolResolver.Invoke(null, new object[] { loaded, local, global })!;
        bool expected = loaded ? local : global;
        Check((bool)result.GetType().GetProperty("Value")!.GetValue(result)! == expected,
            $"campaign bool authority: loaded={loaded}, local={local}, global={global}");
        Check((bool)result.GetType().GetProperty("IsChanged")!.GetValue(result)! == expected,
            $"campaign bool activation: loaded={loaded}, local={local}, global={global}");
    }

    Type scopeType = RequiredType("UFO.Setting.AutoChoosePerk_Type");
    object none = Enum.Parse(scopeType, "No");
    MethodInfo dropdownResolver = settings.GetMethods(allStatic)
        .Single(method => method.Name == "ResolveAuthoritativeCampaignDropdown").MakeGenericMethod(scopeType);
    foreach (bool loaded in new[] { false, true })
    foreach (object local in Enum.GetValues(scopeType))
    foreach (object global in Enum.GetValues(scopeType))
    {
        object result = dropdownResolver.Invoke(null, new[] { (object)loaded, local, global, none })!;
        object expected = loaded ? local : global;
        Check(result.GetType().GetProperty("Value")!.GetValue(result)!.Equals(expected),
            $"campaign perk authority: loaded={loaded}, local={local}, global={global}");
        Check((bool)result.GetType().GetProperty("IsChanged")!.GetValue(result)! == !expected.Equals(none),
            $"campaign perk activation: loaded={loaded}, local={local}, global={global}");
    }

    MethodInfo perkScope = RequiredMethod(RequiredType("UFO.Extension.HeroEnhanceExtensions"),
        "ShouldAddBothBranchPerks", scopeType, typeof(int));
    foreach (object scope in Enum.GetValues(scopeType))
    foreach (int heroType in new[] { -1, 0, 1, 2 })
    {
        bool expected = scope.ToString() switch
        {
            "Clan" => heroType == 0 || heroType == 1,
            "Player" => heroType == 1,
            "All" => heroType >= 0,
            _ => false
        };
        Check((bool)perkScope.Invoke(null, new[] { scope, (object)heroType })! == expected,
            $"perk scope {scope}, hero type {heroType}");
    }

    MethodInfo restoredScope = RequiredMethod(RequiredType("UFO.Behavior.AutoChoosePerks"),
        "ResolveScope", typeof(bool), scopeType, typeof(bool), scopeType);
    foreach (bool available in new[] { false, true })
    foreach (bool hasSaved in new[] { false, true })
    foreach (object configured in Enum.GetValues(scopeType))
    foreach (object saved in Enum.GetValues(scopeType))
    {
        object expected = available ? configured : hasSaved ? saved : none;
        Check(restoredScope.Invoke(null, new[] { (object)available, configured, hasSaved, saved })!.Equals(expected),
            $"load scope authority: available={available}, configured={configured}, hasSaved={hasSaved}, saved={saved}");
    }

    MethodInfo survivorGuard = RequiredMethod(RequiredType("UFO.Patch.Combat.EnemyTroopsKnockoutOrKilled"),
        "ShouldForceKnockoutForLordlessParty", typeof(bool), typeof(bool), typeof(bool), typeof(int));
    foreach (bool forcedKill in new[] { false, true })
    foreach (bool hero in new[] { false, true })
    foreach (bool leader in new[] { false, true })
    foreach (int otherTroops in new[] { 0, 1, 2 })
    {
        bool expected = forcedKill && !hero && !leader && otherTroops == 0;
        Check((bool)survivorGuard.Invoke(null, new object[] { forcedKill, hero, leader, otherTroops })! == expected,
            $"last troop guard: forcedKill={forcedKill}, hero={hero}, leader={leader}, otherTroops={otherTroops}");
    }

    Type reflection = RequiredType("UFO.Extension.Reflection");
    MethodInfo viewModel = reflection.GetMethods(allStatic).Single(method => method.Name == "TryGetViewModel")
        .MakeGenericMethod(typeof(VmMarker));
    Type screenType = viewModel.GetParameters()[0].ParameterType;
    var dynamicModule = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("UFO.ViewModelFixtures"),
        AssemblyBuilderAccess.Run).DefineDynamicModule("Fixtures");
    TypeBuilder parentBuilder = dynamicModule.DefineType("InheritedScreen", TypeAttributes.Public, screenType);
    parentBuilder.DefineField("_dataSource", typeof(object), FieldAttributes.Private);
    Type parentScreen = parentBuilder.CreateType()!;
    TypeBuilder childBuilder = dynamicModule.DefineType("AlternateScreen", TypeAttributes.Public, parentScreen);
    childBuilder.DefineField("_alternateViewModel", typeof(VmMarker), FieldAttributes.Private);
    Type childScreen = childBuilder.CreateType()!;
    object screen = RuntimeHelpers.GetUninitializedObject(childScreen);
    var marker = new VmMarker();
    FieldInfo named = parentScreen.GetField("_dataSource", BindingFlags.Instance | BindingFlags.NonPublic)!;
    FieldInfo alternate = childScreen.GetField("_alternateViewModel", BindingFlags.Instance | BindingFlags.NonPublic)!;
    named.SetValue(screen, marker);
    object?[] vmArgs = { screen, null };
    Check((bool)viewModel.Invoke(null, vmArgs)! && ReferenceEquals(vmArgs[1], marker), "inherited private view model field");
    named.SetValue(screen, "wrong type");
    alternate.SetValue(screen, marker);
    vmArgs = new object?[] { screen, null };
    Check((bool)viewModel.Invoke(null, vmArgs)! && ReferenceEquals(vmArgs[1], marker), "alternate typed view model field");
    alternate.SetValue(screen, null);
    vmArgs = new object?[] { screen, null };
    Check(!(bool)viewModel.Invoke(null, vmArgs)! && vmArgs[1] == null, "missing view model returns false");
    vmArgs = new object?[] { null, null };
    Check(!(bool)viewModel.Invoke(null, vmArgs)! && vmArgs[1] == null, "null screen returns false");

    Console.WriteLine($"PASS {checks} regression checks for {Path.GetFullPath(args[0])}");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error is TargetInvocationException { InnerException: not null } invocation
        ? invocation.InnerException!.ToString() : error.ToString());
    return 1;
}

public sealed class VmMarker;
