using System.Reflection;
using System.Reflection.Emit;

internal static class AuditSelfTests
{
    public static int Run()
    {
        int checks = 0;
        void Verify(bool condition, string label)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("Audit self-test failed: " + label);
        }
        MethodInfo Method(string name) => typeof(BindingFixtures).GetMethod(name, AuditHelpers.AllMembers)!;
        var findings = new List<string>();
        AuditHelpers.ValidateParameters(Method(nameof(BindingFixtures.Valid)), Method(nameof(BindingFixtures.Target)), findings);
        Verify(findings.Count == 0, "valid result and by-reference argument binding");
        findings.Clear();
        AuditHelpers.ValidateParameters(Method(nameof(BindingFixtures.WrongResult)), Method(nameof(BindingFixtures.Target)), findings);
        Verify(findings.Any(finding => finding.StartsWith("TYPE:")), "reject changed result type");
        findings.Clear();
        AuditHelpers.ValidateParameters(Method(nameof(BindingFixtures.MissingArgument)), Method(nameof(BindingFixtures.Target)), findings);
        Verify(findings.Any(finding => finding.StartsWith("BINDING:")), "reject removed named argument");
        findings.Clear();
        AuditHelpers.ValidateParameters(Method(nameof(BindingFixtures.VoidResult)), Method(nameof(BindingFixtures.VoidTarget)), findings);
        Verify(findings.Any(finding => finding.StartsWith("RESULT:")), "reject result on void target");
        var patch = typeof(AttributeFixtures).GetMethod(nameof(AttributeFixtures.Prefix), AuditHelpers.AllMembers)!;
        var target = new PatchTarget();
        foreach (var attribute in typeof(AttributeFixtures).GetCustomAttributesData().Where(AuditHelpers.IsHarmonyPatch).Concat(patch.GetCustomAttributesData().Where(AuditHelpers.IsHarmonyPatch))) target.Apply(attribute);
        Verify(target.Resolve(out var issue)?.GetParameters()[0].ParameterType == typeof(int).MakeByRefType() && issue is null, "merge method attributes and resolve explicit ref overload");
        var il = IlDecoder.Read([0xfe, 0x16, 1, 0, 0, 1, 0x6f, 2, 0, 0, 6, 0x2a]).ToArray();
        Verify(il.Length == 3 && il[0].Opcode == OpCodes.Constrained && il[1].Opcode == OpCodes.Callvirt && il[1].Token == 0x06000002, "two-byte opcodes preserve following call operands");
        bool rejected = false;
        try { _ = IlDecoder.Read([0x28, 0, 0, 0]).ToArray(); } catch (BadImageFormatException) { rejected = true; }
        Verify(rejected, "reject truncated IL");
        Console.WriteLine($"Audit self-tests passed: {checks}.");
        return 0;
    }
    private static class BindingFixtures
    {
        internal static int Target(ref int count) => count;
        internal static void VoidTarget() { }
        internal static void Valid(ref int count, ref int __result) { }
        internal static void WrongResult(ref string __result) { }
        internal static void MissingArgument(int deleted) { }
        internal static void VoidResult(ref int __result) { }
        internal static int Overloaded(int count) => count;
        internal static int Overloaded(ref int count) => count;
    }
    [HarmonyLib.HarmonyPatch(typeof(BindingFixtures), nameof(BindingFixtures.Overloaded))]
    private static class AttributeFixtures
    {
        [HarmonyLib.HarmonyPatch(new[] { typeof(int) }, new[] { HarmonyLib.ArgumentType.Ref })]
        internal static void Prefix(ref int count) { }
    }
}

namespace HarmonyLib
{
    // Tool-only fixture attributes test the real custom-attribute wire shapes.
    internal enum ArgumentType { Normal, Ref, Out, Pointer }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type declaringType, string methodName) { }
        public HarmonyPatch(Type[] argumentTypes, ArgumentType[] argumentVariations) { }
    }
}
