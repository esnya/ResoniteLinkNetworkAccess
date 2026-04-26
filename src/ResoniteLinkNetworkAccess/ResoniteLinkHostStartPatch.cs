using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ResoniteLinkNetworkAccess;

[HarmonyPatch]
internal static class ResoniteLinkHostStartPatch
{
    private const string ResoniteLinkHostTypeName = "FrooxEngine.ResoniteLinkHost";
    private const string WatsonWsServerTypeName = "WatsonWebsocket.WatsonWsServer";

    private static readonly Type? ResoniteLinkHostType = AccessTools.TypeByName(ResoniteLinkHostTypeName);
    private static readonly MethodInfo? ResoniteLinkHostPortGetter =
        AccessTools.PropertyGetter(ResoniteLinkHostType, "Port");

    internal static MethodBase GetTargetMethod()
    {
        return AccessTools.Method(ResoniteLinkHostType, "Start", [typeof(int?)])
            ?? throw new MissingMethodException(ResoniteLinkHostTypeName, "Start(int?)");
    }

    internal static HarmonyMethod GetTranspilerMethod()
    {
        return new HarmonyMethod(AccessTools.Method(
            typeof(ResoniteLinkHostStartPatch),
            nameof(ReplaceListenerHost)));
    }

    private static MethodInfo TargetMethod()
    {
        return (MethodInfo)GetTargetMethod();
    }

    [HarmonyTranspiler]
    private static List<CodeInstruction> ReplaceListenerHost(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> patched = [.. instructions];
        int targetIndex = -1;

        for (int index = 0; index < patched.Count - 4; index++)
        {
            if (IsTargetWatsonWsServerHostArgument(patched, index))
            {
                if (targetIndex >= 0)
                {
                    throw new AmbiguousMatchException(
                        "Found multiple WatsonWsServer(\"localhost\", Port, false) constructor sites in ResoniteLinkHost.Start(int?).");
                }

                targetIndex = index;
            }
        }

        if (targetIndex >= 0)
        {
            patched[targetIndex].opcode = OpCodes.Call;
            patched[targetIndex].operand = AccessTools.Method(
                typeof(ResoniteLinkNetworkAccessMod),
                nameof(ResoniteLinkNetworkAccessMod.GetListenerHost));

            return patched;
        }

        throw new MissingMethodException(
            ResoniteLinkHostTypeName,
            "Start(int?) containing WatsonWsServer(\"localhost\", Port, false)");
    }

    private static bool IsTargetWatsonWsServerHostArgument(List<CodeInstruction> instructions, int index)
    {
        return instructions[index].opcode == OpCodes.Ldstr
            && instructions[index].operand is "localhost"
            && instructions[index + 1].opcode == OpCodes.Ldarg_0
            && ResoniteLinkHostPortGetter is not null
            && instructions[index + 2].Calls(ResoniteLinkHostPortGetter)
            && instructions[index + 3].opcode == OpCodes.Ldc_I4_0
            && IsWatsonWsServerConstructor(instructions[index + 4]);
    }

    private static bool IsWatsonWsServerConstructor(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Newobj
            && instruction.operand is ConstructorInfo
            {
                DeclaringType: not null,
            } constructor
            && constructor.DeclaringType.FullName == WatsonWsServerTypeName
            && HasExpectedConstructorParameters(constructor);
    }

    private static bool HasExpectedConstructorParameters(ConstructorInfo constructor)
    {
        ParameterInfo[] parameters = constructor.GetParameters();
        return parameters.Length == 3
            && parameters[0].ParameterType == typeof(string)
            && parameters[1].ParameterType == typeof(int)
            && parameters[2].ParameterType == typeof(bool);
    }
}
