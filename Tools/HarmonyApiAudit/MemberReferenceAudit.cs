using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

static class MemberReferenceAudit
{
    public static int Run(AuditEnvironment environment, List<string> findings)
    {
        using var stream = File.OpenRead(environment.Options.ModPath);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var decoder = new SignatureNames();
        int checkedMembers = 0;
        foreach (var handle in reader.MemberReferences)
        {
            var reference = reader.GetMemberReference(handle);
            var owner = decoder.Owner(reader, reference.Parent);
            var definitionName = owner.Split('<')[0];
            if (!IsGameType(definitionName)) continue;
            checkedMembers++;
            var type = environment.FindType(definitionName);
            var name = reader.GetString(reference.Name);
            if (type is null) { findings.Add($"MEMBERREF TYPE: {owner} for {name} not found."); continue; }
            string Substitute(string value)
            {
                var arguments = SignatureNames.GenericArguments(owner);
                return Regex.Replace(value, @"(?<!!)!(\d+)", match => {
                    int index = int.Parse(match.Groups[1].Value);
                    return index < arguments.Length ? arguments[index] : match.Value;
                });
            }
            if (reference.GetKind() == MemberReferenceKind.Field)
            {
                string expected = Substitute(reference.DecodeFieldSignature(decoder, null));
                var field = AuditHelpers.FindField(type, name);
                if (field is null || Substitute(AuditHelpers.FormatType(field.FieldType)) != expected)
                    findings.Add($"MEMBERREF FIELD: {owner}.{name}: {expected} missing or changed.");
                continue;
            }
            var signature = reference.DecodeMethodSignature(decoder, null);
            IEnumerable<MethodBase> candidates = name == ".ctor" ? type.GetConstructors(AuditHelpers.AllMembers) :
                name == ".cctor" ? (type.TypeInitializer is { } initializer ? [initializer] : []) :
                type.GetMethods(AuditHelpers.AllMembers).Where(method => method.Name == name);
            bool found = candidates.Any(method =>
                method.IsStatic != signature.Header.IsInstance &&
                (method.IsGenericMethod ? method.GetGenericArguments().Length : 0) == signature.GenericParameterCount &&
                method.GetParameters().Select(parameter => Substitute(AuditHelpers.FormatType(parameter.ParameterType))).SequenceEqual(signature.ParameterTypes.Select(Substitute)) &&
                (method is not MethodInfo info ? "System.Void" : Substitute(AuditHelpers.FormatType(info.ReturnType))) == Substitute(signature.ReturnType));
            if (!found) findings.Add($"MEMBERREF METHOD: {owner}.{name}({string.Join(",", signature.ParameterTypes)}) -> {signature.ReturnType} missing or changed.");
        }
        return checkedMembers;
    }
    private static bool IsGameType(string name) => name.StartsWith("TaleWorlds.", StringComparison.Ordinal) ||
        name.StartsWith("SandBox.", StringComparison.Ordinal) || name.StartsWith("StoryMode.", StringComparison.Ordinal) || name.StartsWith("NavalDLC.", StringComparison.Ordinal);
}

sealed class SignatureNames : ISignatureTypeProvider<string, object?>
{
    public string Owner(MetadataReader reader, EntityHandle handle) => handle.Kind switch {
        HandleKind.TypeReference => GetTypeFromReference(reader, (TypeReferenceHandle)handle, 0),
        HandleKind.TypeDefinition => GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, 0),
        HandleKind.TypeSpecification => GetTypeFromSpecification(reader, null, (TypeSpecificationHandle)handle, 0),
        HandleKind.MethodDefinition => GetTypeFromDefinition(reader, reader.GetMethodDefinition((MethodDefinitionHandle)handle).GetDeclaringType(), 0),
        _ => $"unsupported:{handle.Kind}"
    };
    public static string[] GenericArguments(string owner)
    {
        int start = owner.IndexOf('<');
        if (start < 0) return [];
        var content = owner[(start + 1)..^1];
        var values = new List<string>();
        int depth = 0, previous = 0;
        for (int index = 0; index < content.Length; index++)
        {
            if (content[index] == '<') depth++;
            else if (content[index] == '>') depth--;
            else if (content[index] == ',' && depth == 0) { values.Add(content[previous..index]); previous = index + 1; }
        }
        values.Add(content[previous..]);
        return values.ToArray();
    }
    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";
    public string GetByReferenceType(string elementType) => elementType + "&";
    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr(" + string.Join(",", signature.ParameterTypes) + ")";
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType + "<" + string.Join(",", typeArguments) + ">";
    public string GetGenericMethodParameter(object? context, int index) => "!!" + index;
    public string GetGenericTypeParameter(object? context, int index) => "!" + index;
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType;
    public string GetPointerType(string elementType) => elementType + "*";
    public string GetPrimitiveType(PrimitiveTypeCode code) => "System." + (code switch {
        PrimitiveTypeCode.Void => "Void", PrimitiveTypeCode.Boolean => "Boolean", PrimitiveTypeCode.Char => "Char",
        PrimitiveTypeCode.SByte => "SByte", PrimitiveTypeCode.Byte => "Byte", PrimitiveTypeCode.Int16 => "Int16",
        PrimitiveTypeCode.UInt16 => "UInt16", PrimitiveTypeCode.Int32 => "Int32", PrimitiveTypeCode.UInt32 => "UInt32",
        PrimitiveTypeCode.Int64 => "Int64", PrimitiveTypeCode.UInt64 => "UInt64", PrimitiveTypeCode.Single => "Single",
        PrimitiveTypeCode.Double => "Double", PrimitiveTypeCode.String => "String", PrimitiveTypeCode.IntPtr => "IntPtr",
        PrimitiveTypeCode.UIntPtr => "UIntPtr", PrimitiveTypeCode.Object => "Object", PrimitiveTypeCode.TypedReference => "TypedReference",
        _ => throw new BadImageFormatException($"Unknown primitive {code}.")
    });
    public string GetSZArrayType(string elementType) => elementType + "[]";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var type = reader.GetTypeDefinition(handle);
        string name = reader.GetString(type.Name);
        if (!type.GetDeclaringType().IsNil) return GetTypeFromDefinition(reader, type.GetDeclaringType(), 0) + "+" + name;
        string space = reader.GetString(type.Namespace);
        return space.Length == 0 ? name : space + "." + name;
    }
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var type = reader.GetTypeReference(handle);
        string name = reader.GetString(type.Name);
        if (type.ResolutionScope.Kind == HandleKind.TypeReference) return GetTypeFromReference(reader, (TypeReferenceHandle)type.ResolutionScope, 0) + "+" + name;
        string space = reader.GetString(type.Namespace);
        return space.Length == 0 ? name : space + "." + name;
    }
    public string GetTypeFromSpecification(MetadataReader reader, object? context, TypeSpecificationHandle handle, byte rawTypeKind) => reader.GetTypeSpecification(handle).DecodeSignature(this, context);
}
