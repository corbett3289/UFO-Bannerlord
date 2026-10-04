using System.Reflection;

static class AuditHelpers
{
    public const BindingFlags AllMembers = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    public static bool IsHarmonyPatch(CustomAttributeData attribute) => attribute.AttributeType.FullName == "HarmonyLib.HarmonyPatch";
    public static bool IsPatchMethod(MethodInfo method) => method.Name is "Prefix" or "Postfix" or "Transpiler" or "Finalizer" ||
        method.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName is "HarmonyLib.HarmonyPrefix" or "HarmonyLib.HarmonyPostfix" or "HarmonyLib.HarmonyTranspiler" or "HarmonyLib.HarmonyFinalizer");
    public static Type ValueType(Type type) => type.IsByRef ? type.GetElementType()! : type;
    public static bool BindingsMatch(MethodInfo callback, MethodBase original)
    {
        var findings = new List<string>();
        ValidateParameters(callback, original, findings);
        return findings.Count == 0;
    }
    public static void ValidateParameters(MethodInfo patch, MethodBase original, List<string> findings)
    {
        var parameters = original.GetParameters();
        string label = $"{patch.DeclaringType!.FullName}.{patch.Name} -> {original.DeclaringType!.FullName}.{original.Name}";
        foreach (var parameter in patch.GetParameters())
        {
            string name = parameter.Name!;
            if (name is "__state" or "__args" or "__originalMethod" or "__runOriginal" or "__exception" or "instructions" or "generator") continue;
            if (name == "__instance")
            {
                if (original.IsStatic) findings.Add($"INSTANCE: {label} requests __instance for a static target.");
                else CheckType(original.DeclaringType!, parameter, label, findings);
                continue;
            }
            if (name == "__result")
            {
                if (original is not MethodInfo method || method.ReturnType.FullName == "System.Void")
                    findings.Add($"RESULT: {label} requests a result from a void/constructor target.");
                else CheckType(method.ReturnType, parameter, label, findings);
                continue;
            }
            if (name.StartsWith("___", StringComparison.Ordinal))
            {
                var field = FindField(original.DeclaringType!, name[3..]);
                if (field is null) findings.Add($"FIELD: {label} references missing field {name[3..]}.");
                else CheckType(field.FieldType, parameter, label, findings);
                continue;
            }
            ParameterInfo? bound = null;
            if (name.StartsWith("__", StringComparison.Ordinal) && int.TryParse(name[2..], out var index))
                bound = index >= 0 && index < parameters.Length ? parameters[index] : null;
            else bound = parameters.FirstOrDefault(candidate => candidate.Name == name);
            if (bound is null) findings.Add($"BINDING: {label} parameter '{name}' is absent.");
            else CheckType(bound.ParameterType, parameter, label, findings);
        }
    }
    private static void CheckType(Type targetType, ParameterInfo patchParameter, string label, List<string> findings)
    {
        var actual = ValueType(targetType);
        var requested = ValueType(patchParameter.ParameterType);
        bool compatible = requested == actual || (!patchParameter.ParameterType.IsByRef && requested.IsAssignableFrom(actual));
        if (!compatible) findings.Add($"TYPE: {label} parameter '{patchParameter.Name}' requests {requested.FullName}; actual {actual.FullName}.");
    }
    public static FieldInfo? FindField(Type type, string name)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
            if (current.GetField(name, AllMembers | BindingFlags.DeclaredOnly) is { } field) return field;
        return null;
    }
    public static string Signature(MethodBase method) => $"{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => FormatType(parameter.ParameterType)))})";
    public static string FormatType(Type type)
    {
        if (type.IsByRef) return FormatType(type.GetElementType()!) + "&";
        if (type.IsPointer) return FormatType(type.GetElementType()!) + "*";
        if (type.IsArray) return FormatType(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsGenericParameter) return (type.DeclaringMethod is null ? "!" : "!!") + type.GenericParameterPosition;
        if (type.IsGenericType) return type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(FormatType)) + ">";
        return type.FullName ?? type.Name;
    }
}

sealed class PatchTarget
{
    public Type? DeclaringType { get; private set; }
    public string? MethodName { get; private set; }
    private int MethodKind { get; set; }
    private Type[]? ArgumentTypes { get; set; }
    private int[]? ArgumentVariations { get; set; }
    public void Apply(CustomAttributeData attribute)
    {
        var parameters = attribute.Constructor.GetParameters();
        for (var index = 0; index < parameters.Length; index++)
        {
            var kind = parameters[index].ParameterType.FullName;
            var value = attribute.ConstructorArguments[index].Value;
            switch (kind)
            {
                case "System.Type": DeclaringType = (Type?)value; break;
                case "System.String": MethodName = (string?)value; break;
                case "HarmonyLib.MethodType": MethodKind = Convert.ToInt32(value); break;
                case "System.Type[]": ArgumentTypes = ((IEnumerable<CustomAttributeTypedArgument>)value!).Select(argument => (Type)argument.Value!).ToArray(); break;
                case "HarmonyLib.ArgumentType[]": ArgumentVariations = ((IEnumerable<CustomAttributeTypedArgument>)value!).Select(argument => Convert.ToInt32(argument.Value)).ToArray(); break;
            }
        }
    }
    public MethodBase? Resolve(out string? issue)
    {
        issue = null;
        if (DeclaringType is null) { issue = "no declaring type"; return null; }
        var types = ArgumentTypes?.ToArray();
        if (types is not null && ArgumentVariations is not null)
        {
            if (types.Length != ArgumentVariations.Length) { issue = "argument variation count differs"; return null; }
            for (var index = 0; index < types.Length; index++)
                types[index] = ArgumentVariations[index] switch { 0 => types[index], 1 or 2 => types[index].MakeByRefType(), 3 => types[index].MakePointerType(), _ => throw new ArgumentException("Unknown Harmony ArgumentType.") };
        }
        if (MethodKind == 3) return DeclaringType.GetConstructor(AuditHelpers.AllMembers, null, types ?? Type.EmptyTypes, null);
        if (MethodKind == 4) return DeclaringType.TypeInitializer;
        if (MethodKind is 1 or 2)
        {
            var property = DeclaringType.GetProperty(MethodName!, AuditHelpers.AllMembers);
            return MethodKind == 1 ? property?.GetMethod : property?.SetMethod;
        }
        if (MethodKind != 0) { issue = $"unsupported MethodType {MethodKind}"; return null; }
        var candidates = DeclaringType.GetMethods(AuditHelpers.AllMembers).Where(method => method.Name == MethodName)
            .Where(method => types is null || method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(types)).ToArray();
        if (candidates.Length != 1) { issue = $"expected one target, found {candidates.Length}: {string.Join(" | ", candidates.Select(AuditHelpers.Signature))}"; return null; }
        return candidates[0];
    }
    public string Describe() => $"{DeclaringType?.FullName}.{MethodName}";
}

static class ReflectedMemberAudit
{
    public static int Run(AuditEnvironment environment, List<string> findings, bool naval)
    {
        int count = 0;
        void Method(string typeName, string name, string[] parameters, string? result = null, bool? generic = null)
        {
            count++;
            var type = environment.FindType(typeName);
            var methods = type?.GetMethods(AuditHelpers.AllMembers).Where(method => method.Name == name &&
                method.GetParameters().Select(parameter => AuditHelpers.FormatType(parameter.ParameterType)).SequenceEqual(parameters) &&
                (result is null || AuditHelpers.FormatType(method.ReturnType) == result) && (generic is null || method.IsGenericMethodDefinition == generic)).ToArray();
            if (methods?.Length != 1) findings.Add($"REFLECTED METHOD: {typeName}.{name}({string.Join(",", parameters)}) expected once; found {methods?.Length ?? 0}.");
        }
        void Field(string typeName, string name, string? expected = null)
        {
            count++;
            var type = environment.FindType(typeName);
            var field = type is null ? null : AuditHelpers.FindField(type, name);
            if (field is null || expected is not null && AuditHelpers.FormatType(field.FieldType) != expected) findings.Add($"REFLECTED FIELD: {typeName}.{name} missing or changed.");
        }
        void Property(string typeName, string name, string? expected = null, bool setter = false)
        {
            count++;
            var property = environment.FindType(typeName)?.GetProperty(name, AuditHelpers.AllMembers);
            if (property?.GetMethod is null || setter && property.SetMethod is null || expected is not null && AuditHelpers.FormatType(property.PropertyType) != expected) findings.Add($"REFLECTED PROPERTY: {typeName}.{name} missing or changed.");
        }
        const string weaponVm = "TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign.WeaponDesignVM";
        Method(weaponVm, "TrySetSecondaryUsageIndex", ["System.Int32"], "System.Void");
        Method(weaponVm, "RefreshStats", [], "System.Void");
        Field(weaponVm, "_craftingBehavior", "TaleWorlds.CampaignSystem.CampaignBehaviors.ICraftingCampaignBehavior");
        foreach (var name in new[] { "Handling", "SwingDamage", "SwingSpeed", "ThrustDamage", "ThrustSpeed" }) Property("TaleWorlds.Core.WeaponComponentData", name, "System.Int32", setter: true);
        Field("TaleWorlds.CampaignSystem.Hero", "_defaultAge", "System.Single");
        Field("TaleWorlds.CampaignSystem.Hero", "_birthDay", "TaleWorlds.CampaignSystem.CampaignTime");
        Field("TaleWorlds.CampaignSystem.Settlements.Locations.Location", "_characterList");
        Field("TaleWorlds.CampaignSystem.Settlements.Locations.Location", "_aiCanExit", "System.String");
        Field("TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages.EncyclopediaHeroPageVM", "_hero", "TaleWorlds.CampaignSystem.Hero");
        Field("TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM", "_selectedItem");
        Method("TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyVM", "InitializeTroopLists", [], "System.Void");
        foreach (var (screenName, modelName) in new[] {
            ("SandBox.GauntletUI.GauntletCharacterDeveloperScreen", "TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper.CharacterDeveloperVM"),
            ("SandBox.GauntletUI.GauntletInventoryScreen", "TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM"),
            ("SandBox.GauntletUI.GauntletPartyScreen", "TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyVM") })
        {
            count++;
            var screen = environment.FindType(screenName);
            var model = environment.FindType(modelName);
            bool found = false;
            for (var current = screen; current is not null; current = current.BaseType)
                found |= model is not null && current.GetFields(AuditHelpers.AllMembers | BindingFlags.DeclaredOnly).Any(field => model.IsAssignableFrom(field.FieldType));
            if (!found) findings.Add($"REFLECTED VM: {screenName} has no compatible {modelName} field.");
        }
        if (naval)
        {
            Method("NavalDLC.NavalDLCCheats", "AddShipToPlayer", ["System.Collections.Generic.List`1<System.String>"], "System.String");
            Method("NavalDLC.NavalDLCCheats", "UnlockFigurehead", ["System.Collections.Generic.List`1<System.String>"], "System.String");
            Method("NavalDLC.NavalDLCHelpers", "AddUpgradePiecesToPartyShips", ["TaleWorlds.CampaignSystem.Party.MobileParty", "System.Collections.Generic.Dictionary`2<System.String,System.String>", "TaleWorlds.CampaignSystem.Naval.Figurehead"], "System.Void");
            Property("TaleWorlds.CampaignSystem.Naval.DefaultFigureheads", "Instance");
            Property("TaleWorlds.ObjectSystem.MBObjectManager", "Instance");
            Method("TaleWorlds.ObjectSystem.MBObjectManager", "GetObjectTypeList", [], generic: true);
            Property("TaleWorlds.Core.ShipUpgradePiece", "TargetSlots");
            foreach (var name in new[] { "LightValue", "MediumValue", "HeavyValue" }) Property("TaleWorlds.Core.ShipUpgradePiece", name, "System.Int32");
        }
        return count;
    }
}
