using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ResoniteLinkNetworkAccess;

[HarmonyPatch]
internal static class ResoniteLinkAnnounceEndpointPatch
{
    private const string LanSessionAnnouncerTypeName = "FrooxEngine.LAN_SessionAnnouncer";
    private const string SessionAnnouncerTypeName = "FrooxEngine.SessionAnnouncer";

    private static readonly Type? LanSessionAnnouncerType = AccessTools.TypeByName(LanSessionAnnouncerTypeName);
    private static readonly Type? SessionAnnouncerType = AccessTools.TypeByName(SessionAnnouncerTypeName);
    private static readonly FieldInfo? IPAddressBroadcastField = AccessTools.Field(typeof(System.Net.IPAddress), nameof(System.Net.IPAddress.Broadcast));
    private static readonly ConstructorInfo? IPEndPointConstructor = AccessTools.Constructor(
        typeof(System.Net.IPEndPoint),
        [typeof(System.Net.IPAddress), typeof(int)]);
    private static readonly FieldInfo? ResoniteLinkAnnounceEndpointField =
        AccessTools.Field(LanSessionAnnouncerType, "resoniteLinkAnnounceEndpoint");

    internal static MethodBase GetTargetMethod()
    {
        return AccessTools.Constructor(LanSessionAnnouncerType, [SessionAnnouncerType])
            ?? throw new MissingMethodException(LanSessionAnnouncerTypeName, ".ctor(SessionAnnouncer)");
    }

    internal static HarmonyMethod GetTranspilerMethod()
    {
        return new HarmonyMethod(AccessTools.Method(
            typeof(ResoniteLinkAnnounceEndpointPatch),
            nameof(ReplaceResoniteLinkAnnounceEndpoint)));
    }

    private static MethodBase TargetMethod()
    {
        return GetTargetMethod();
    }

    [HarmonyTranspiler]
    private static List<CodeInstruction> ReplaceResoniteLinkAnnounceEndpoint(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> patched = [.. instructions];
        int targetIndex = -1;

        for (int index = 0; index < patched.Count - 4; index++)
        {
            if (IsTargetEndpointInitialization(patched, index))
            {
                if (targetIndex >= 0)
                {
                    throw new AmbiguousMatchException(
                        "Found multiple resoniteLinkAnnounceEndpoint Broadcast:12512 initialization sites in LAN_SessionAnnouncer..ctor(SessionAnnouncer).");
                }

                targetIndex = index;
            }
        }

        if (targetIndex < 0)
        {
            throw new MissingMethodException(
                LanSessionAnnouncerTypeName,
                ".ctor(SessionAnnouncer) containing resoniteLinkAnnounceEndpoint Broadcast:12512 initialization");
        }

        patched[targetIndex + 1] = new CodeInstruction(
            OpCodes.Call,
            AccessTools.Method(
                typeof(ResoniteLinkNetworkAccessMod),
                nameof(ResoniteLinkNetworkAccessMod.GetResoniteLinkAnnounceEndpoint)));
        patched.RemoveRange(targetIndex + 2, 2);
        return patched;
    }

    private static bool IsTargetEndpointInitialization(List<CodeInstruction> instructions, int index)
    {
        return instructions[index].opcode == OpCodes.Ldarg_0
            && IPAddressBroadcastField is not null
            && instructions[index + 1].LoadsField(IPAddressBroadcastField)
            && IsConfiguredDefaultPort(instructions[index + 2])
            && IPEndPointConstructor is not null
            && instructions[index + 3].operand is ConstructorInfo constructor
            && constructor == IPEndPointConstructor
            && ResoniteLinkAnnounceEndpointField is not null
            && instructions[index + 4].StoresField(ResoniteLinkAnnounceEndpointField);
    }

    private static bool IsConfiguredDefaultPort(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Ldc_I4
            && instruction.operand is int port
            && port == ResoniteLinkNetworkAccessMod.DefaultResoniteLinkAnnouncePort;
    }
}
