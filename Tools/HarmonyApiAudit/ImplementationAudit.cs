using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

static class ImplementationAudit
{
    public static int Run(AuditEnvironment environment, List<string> findings, bool naval)
    {
        var paths = AuditEnvironment.OfficialFiles(environment.Options.ImplementationRoot!).ToArray();
        var campaign = FindAssembly(paths, "TaleWorlds.CampaignSystem");
        if (campaign is null) { findings.Add("IL ASSEMBLY: TaleWorlds.CampaignSystem implementation missing."); return 0; }
        int count = 1;
        using (var image = new IlImage(campaign))
        {
            var calls = image.Calls("TaleWorlds.CampaignSystem.CampaignBehaviors.PartyHealCampaignBehavior", "HealMemberHeroes", findings);
            var heals = calls.Where(call => call.Owner == "TaleWorlds.CampaignSystem.Hero" && call.Name == "Heal" && call.Parameters.SequenceEqual(["System.Int32", "System.Boolean"])).ToArray();
            if (heals.Length != 1) findings.Add($"IL HEALING: expected one Hero.Heal(int,bool) call in HealMemberHeroes; found {heals.Length}.");
            foreach (var name in new[] { "get_SelfMedication", "get_ValorInjuryRecoveryEffect", "RoundRandomized" })
                if (!calls.Any(call => call.Name == name)) findings.Add($"IL HEALING: Native member recovery no longer calls {name}; review adaptation.");
            if (calls.Any(call => call.Name == "get_PrisonRoster")) findings.Add("IL HEALING: member recovery now includes prisoners; review caps before patching.");
        }
        if (naval)
        {
            var navalPath = FindAssembly(paths, "NavalDLC");
            if (navalPath is null) { findings.Add("IL ASSEMBLY: NavalDLC implementation missing."); return count; }
            using var image = new IlImage(navalPath);
            foreach (var (type, member) in new[] {
                ("NavalDLCPartySpeedCalculationModel", "CalculateFinalSpeed"),
                ("NavalDLCMapVisibilityModel", "GetPartySpottingRange"),
                ("NavalDLCMobilePartyFoodConsumptionModel", "CalculateDailyFoodConsumptionf"),
                ("NavalDLCPartyWageModel", "GetTotalWage"),
                ("NavalDLCPartyHealingModel", "GetDailyHealingHpForHeroes"),
                ("NavalDLCPartyHealingModel", "GetDailyHealingForRegulars"),
                ("NavalDLCBattleRewardModel", "CalculateRenownGain"),
                ("NavalDLCBattleRewardModel", "CalculateInfluenceGain") })
            {
                count++;
                var calls = image.Calls("NavalDLC.GameComponents." + type, member, findings);
                int getter = Array.FindIndex(calls, call => call.Name == "get_BaseModel");
                int forwarded = Array.FindIndex(calls, call => call.Name == member);
                if (getter < 0 || forwarded <= getter) findings.Add($"IL FORWARDING: {type}.{member} does not call BaseModel.{member}; generic options require review.");
            }
        }
        return count;
    }
    private static string? FindAssembly(string[] paths, string name) => paths.FirstOrDefault(path => Path.GetFileNameWithoutExtension(path) == name);
}

readonly record struct IlCall(string Owner, string Name, string[] Parameters);
readonly record struct IlInstruction(int Offset, OpCode Opcode, int? Token);

sealed class IlImage : IDisposable
{
    private readonly FileStream _stream;
    private readonly PEReader _pe;
    private readonly MetadataReader _reader;
    private readonly SignatureNames _names = new();
    public IlImage(string path) { _stream = File.OpenRead(path); _pe = new(_stream); _reader = _pe.GetMetadataReader(); }
    public IlCall[] Calls(string typeName, string methodName, List<string> findings)
    {
        var types = _reader.TypeDefinitions.Where(handle => _names.GetTypeFromDefinition(_reader, handle, 0) == typeName).ToArray();
        if (types.Length != 1) { findings.Add($"IL TYPE: {typeName} expected once, found {types.Length}."); return []; }
        var methods = _reader.GetTypeDefinition(types[0]).GetMethods().Where(handle => _reader.GetString(_reader.GetMethodDefinition(handle).Name) == methodName).ToArray();
        if (methods.Length != 1) { findings.Add($"IL METHOD: {typeName}.{methodName} expected once, found {methods.Length}."); return []; }
        var definition = _reader.GetMethodDefinition(methods[0]);
        if (definition.RelativeVirtualAddress == 0) { findings.Add($"IL BODY: {typeName}.{methodName} has no implementation."); return []; }
        var bytes = _pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes()!;
        var calls = new List<IlCall>();
        foreach (var instruction in IlDecoder.Read(bytes).Where(instruction => instruction.Opcode == OpCodes.Call || instruction.Opcode == OpCodes.Callvirt))
        {
            var handle = MetadataTokens.EntityHandle(instruction.Token!.Value);
            if (handle.Kind == HandleKind.MethodSpecification) handle = _reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method;
            if (handle.Kind == HandleKind.MemberReference)
            {
                var reference = _reader.GetMemberReference((MemberReferenceHandle)handle);
                calls.Add(new(_names.Owner(_reader, reference.Parent), _reader.GetString(reference.Name), reference.DecodeMethodSignature(_names, null).ParameterTypes.ToArray()));
            }
            else if (handle.Kind == HandleKind.MethodDefinition)
            {
                var method = _reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                calls.Add(new(_names.GetTypeFromDefinition(_reader, method.GetDeclaringType(), 0), _reader.GetString(method.Name), method.DecodeSignature(_names, null).ParameterTypes.ToArray()));
            }
        }
        return calls.ToArray();
    }
    public void Dispose() { _pe.Dispose(); _stream.Dispose(); }
}

static class IlDecoder
{
    private static readonly Dictionary<ushort, OpCode> Opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)!).ToDictionary(opcode => unchecked((ushort)opcode.Value));
    public static IEnumerable<IlInstruction> Read(byte[] bytes)
    {
        for (int index = 0; index < bytes.Length;)
        {
            int offset = index;
            ushort code = bytes[index++];
            if (code == 0xfe) { Require(bytes, index, 1); code = (ushort)(0xfe00 | bytes[index++]); }
            if (!Opcodes.TryGetValue(code, out var opcode)) throw new BadImageFormatException($"Unknown IL opcode {code:X4} at {offset:X4}.");
            int size = opcode.OperandType switch {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => SwitchSize(bytes, index),
                _ => 4
            };
            Require(bytes, index, size);
            int? token = opcode.OperandType is OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineType or OperandType.InlineTok ? BitConverter.ToInt32(bytes, index) : null;
            yield return new(offset, opcode, token);
            index += size;
        }
    }
    private static int SwitchSize(byte[] bytes, int index)
    {
        Require(bytes, index, 4);
        int count = BitConverter.ToInt32(bytes, index);
        if (count < 0 || count > (bytes.Length - index - 4) / 4) throw new BadImageFormatException("Invalid IL switch table.");
        return 4 + count * 4;
    }
    private static void Require(byte[] bytes, int index, int length)
    {
        if (index < 0 || length < 0 || index > bytes.Length - length) throw new BadImageFormatException("Truncated IL operand.");
    }
}
