using System.Net;
using System.Reflection;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CecilOpCodes = Mono.Cecil.Cil.OpCodes;
using ReflectionOpCodes = System.Reflection.Emit.OpCodes;

namespace ResoniteLinkNetworkAccess.Tests;

public sealed class ReflectionContractTests
{
    [Fact]
    public void ResoniteLinkHostShouldExposeExpectedStartMethod()
    {
        using AssemblyDefinition frooxEngineAssembly = AssemblyDefinition.ReadAssembly(GetAssemblyPath("FrooxEngine.dll"));
        TypeDefinition host = GetRequiredType(frooxEngineAssembly, "FrooxEngine.ResoniteLinkHost");
        MethodDefinition? method = GetStartMethod(host);

        Assert.NotNull(method);
    }

    [Fact]
    public void ResoniteLinkHostStartShouldCreateWatsonWebsocketServerFromLocalhost()
    {
        using AssemblyDefinition frooxEngineAssembly = AssemblyDefinition.ReadAssembly(GetAssemblyPath("FrooxEngine.dll"));
        TypeDefinition host = GetRequiredType(frooxEngineAssembly, "FrooxEngine.ResoniteLinkHost");
        MethodDefinition method = GetStartMethod(host)
            ?? throw new InvalidOperationException(BuildMissingMemberMessage(frooxEngineAssembly, "FrooxEngine.ResoniteLinkHost.Start(int?)"));

        Assert.Single(
            Enumerable.Range(0, method.Body.Instructions.Count - 4),
            index => IsWatsonWebsocketLocalhostConstructorPattern(method.Body.Instructions, index));
    }

    [Fact]
    public void WatsonWebsocketServerShouldExposeExpectedConstructor()
    {
        using AssemblyDefinition watsonAssembly = AssemblyDefinition.ReadAssembly(GetAssemblyPath("WatsonWebsocket.dll"));
        TypeDefinition server = GetRequiredType(watsonAssembly, "WatsonWebsocket.WatsonWsServer");

        Assert.Contains(server.Methods, static method =>
            method.IsConstructor
            && method.Parameters.Count == 3
            && method.Parameters[0].ParameterType.FullName == "System.String"
            && method.Parameters[1].ParameterType.FullName == "System.Int32"
            && method.Parameters[2].ParameterType.FullName == "System.Boolean");
    }

    [Fact]
    public void LanSessionAnnouncerShouldExposeExpectedConstructor()
    {
        using AssemblyDefinition frooxEngineAssembly = AssemblyDefinition.ReadAssembly(GetAssemblyPath("FrooxEngine.dll"));
        TypeDefinition announcer = GetRequiredType(frooxEngineAssembly, "FrooxEngine.LAN_SessionAnnouncer");
        MethodDefinition? method = GetLanSessionAnnouncerConstructor(announcer);

        Assert.NotNull(method);
    }

    [Fact]
    public void LanSessionAnnouncerShouldInitializeResoniteLinkAnnounceEndpointFromBroadcastDefault()
    {
        using AssemblyDefinition frooxEngineAssembly = AssemblyDefinition.ReadAssembly(GetAssemblyPath("FrooxEngine.dll"));
        TypeDefinition announcer = GetRequiredType(frooxEngineAssembly, "FrooxEngine.LAN_SessionAnnouncer");
        MethodDefinition method = GetLanSessionAnnouncerConstructor(announcer)
            ?? throw new InvalidOperationException(BuildMissingMemberMessage(
                frooxEngineAssembly,
                "FrooxEngine.LAN_SessionAnnouncer..ctor(SessionAnnouncer)"));

        Assert.Single(
            Enumerable.Range(0, method.Body.Instructions.Count - 4),
            index => IsResoniteLinkAnnounceEndpointDefaultPattern(method.Body.Instructions, index));
    }

    [Fact]
    public void HarmonyPatchTypesShouldDeclarePatchMetadata()
    {
        using AssemblyDefinition modAssembly = AssemblyDefinition.ReadAssembly(GetAssemblyPath("ResoniteLinkNetworkAccess.dll"));
        TypeDefinition patchType = GetRequiredType(modAssembly, "ResoniteLinkNetworkAccess.ResoniteLinkHostStartPatch");
        TypeDefinition announcePatchType = GetRequiredType(modAssembly, "ResoniteLinkNetworkAccess.ResoniteLinkAnnounceEndpointPatch");

        Assert.Contains(
            patchType.CustomAttributes,
            static attribute => attribute.AttributeType.FullName is "HarmonyLib.HarmonyPatch");
        Assert.Contains(
            announcePatchType.CustomAttributes,
            static attribute => attribute.AttributeType.FullName is "HarmonyLib.HarmonyPatch");
    }

    [Fact]
    public void ListenerHostTranspilerReplacesOnlyLocalhostOperand()
    {
        Assembly.LoadFrom(GetAssemblyPath("FrooxEngine.dll"));
        Assembly.LoadFrom(GetAssemblyPath("WatsonWebsocket.dll"));

        Type hostType = AccessTools.TypeByName("FrooxEngine.ResoniteLinkHost")
            ?? throw new InvalidOperationException("FrooxEngine.ResoniteLinkHost was not loaded.");
        Type serverType = AccessTools.TypeByName("WatsonWebsocket.WatsonWsServer")
            ?? throw new InvalidOperationException("WatsonWebsocket.WatsonWsServer was not loaded.");
        MethodInfo portGetter = AccessTools.PropertyGetter(hostType, "Port")
            ?? throw new InvalidOperationException("ResoniteLinkHost.Port getter was not found.");
        ConstructorInfo serverConstructor = AccessTools.Constructor(serverType, [typeof(string), typeof(int), typeof(bool)])
            ?? throw new InvalidOperationException("WatsonWsServer(string,int,bool) constructor was not found.");

        List<CodeInstruction> instructions =
        [
            new(ReflectionOpCodes.Ldstr, "localhost"),
            new(ReflectionOpCodes.Ldarg_0),
            new(ReflectionOpCodes.Call, portGetter),
            new(ReflectionOpCodes.Ldc_I4_0),
            new(ReflectionOpCodes.Newobj, serverConstructor),
        ];

        List<CodeInstruction> patched = InvokeTranspiler(
            "ResoniteLinkNetworkAccess.ResoniteLinkHostStartPatch",
            "ReplaceListenerHost",
            instructions);

        Assert.Equal(5, patched.Count);
        Assert.Equal(ReflectionOpCodes.Call, patched[0].opcode);
        Assert.Equal(nameof(ResoniteLinkNetworkAccessMod.GetListenerHost), ((MethodInfo)patched[0].operand).Name);
        Assert.Equal(ReflectionOpCodes.Ldarg_0, patched[1].opcode);
        Assert.Same(serverConstructor, patched[4].operand);
    }

    [Fact]
    public void ResoniteLinkAnnounceTranspilerReplacesEndpointConstruction()
    {
        Assembly.LoadFrom(GetAssemblyPath("FrooxEngine.dll"));

        Type announcerType = AccessTools.TypeByName("FrooxEngine.LAN_SessionAnnouncer")
            ?? throw new InvalidOperationException("FrooxEngine.LAN_SessionAnnouncer was not loaded.");
        FieldInfo broadcastField = AccessTools.Field(typeof(IPAddress), nameof(IPAddress.Broadcast))
            ?? throw new InvalidOperationException("IPAddress.Broadcast field was not found.");
        ConstructorInfo endpointConstructor = AccessTools.Constructor(typeof(IPEndPoint), [typeof(IPAddress), typeof(int)])
            ?? throw new InvalidOperationException("IPEndPoint(IPAddress,int) constructor was not found.");
        FieldInfo endpointField = AccessTools.Field(announcerType, "resoniteLinkAnnounceEndpoint")
            ?? throw new InvalidOperationException("LAN_SessionAnnouncer.resoniteLinkAnnounceEndpoint was not found.");

        List<CodeInstruction> instructions =
        [
            new(ReflectionOpCodes.Ldarg_0),
            new(ReflectionOpCodes.Ldsfld, broadcastField),
            new(ReflectionOpCodes.Ldc_I4, 12512),
            new(ReflectionOpCodes.Newobj, endpointConstructor),
            new(ReflectionOpCodes.Stfld, endpointField),
        ];

        List<CodeInstruction> patched = InvokeTranspiler(
            "ResoniteLinkNetworkAccess.ResoniteLinkAnnounceEndpointPatch",
            "ReplaceResoniteLinkAnnounceEndpoint",
            instructions);

        Assert.Equal(3, patched.Count);
        Assert.Equal(ReflectionOpCodes.Ldarg_0, patched[0].opcode);
        Assert.Equal(ReflectionOpCodes.Call, patched[1].opcode);
        Assert.Equal(nameof(ResoniteLinkNetworkAccessMod.GetResoniteLinkAnnounceEndpoint), ((MethodInfo)patched[1].operand).Name);
        Assert.Equal(ReflectionOpCodes.Stfld, patched[2].opcode);
        Assert.Same(endpointField, patched[2].operand);
    }

    [Fact]
    public void ListenerHostTranspilerFailsClosedWhenPatternIsMissing()
    {
        AssertTranspilerThrows<MissingMethodException>(
            "ResoniteLinkNetworkAccess.ResoniteLinkHostStartPatch",
            "ReplaceListenerHost",
            [new CodeInstruction(ReflectionOpCodes.Nop)]);
    }

    [Fact]
    public void ListenerHostTranspilerFailsClosedWhenPatternIsAmbiguous()
    {
        List<CodeInstruction> targetPattern = CreateListenerHostPattern();

        AssertTranspilerThrows<AmbiguousMatchException>(
            "ResoniteLinkNetworkAccess.ResoniteLinkHostStartPatch",
            "ReplaceListenerHost",
            [.. targetPattern, .. targetPattern]);
    }

    [Fact]
    public void ResoniteLinkAnnounceTranspilerFailsClosedWhenPatternIsMissing()
    {
        AssertTranspilerThrows<MissingMethodException>(
            "ResoniteLinkNetworkAccess.ResoniteLinkAnnounceEndpointPatch",
            "ReplaceResoniteLinkAnnounceEndpoint",
            [new CodeInstruction(ReflectionOpCodes.Nop)]);
    }

    [Fact]
    public void ResoniteLinkAnnounceTranspilerFailsClosedWhenPatternIsAmbiguous()
    {
        List<CodeInstruction> targetPattern = CreateResoniteLinkAnnounceEndpointPattern();

        AssertTranspilerThrows<AmbiguousMatchException>(
            "ResoniteLinkNetworkAccess.ResoniteLinkAnnounceEndpointPatch",
            "ReplaceResoniteLinkAnnounceEndpoint",
            [.. targetPattern, .. targetPattern]);
    }

    private static MethodDefinition? GetStartMethod(TypeDefinition host)
    {
        return host.Methods.FirstOrDefault(static method =>
            method.Name == "Start"
            && method.Parameters.Count == 1
            && method.Parameters[0].ParameterType.FullName == "System.Nullable`1<System.Int32>");
    }

    private static MethodDefinition? GetLanSessionAnnouncerConstructor(TypeDefinition announcer)
    {
        return announcer.Methods.FirstOrDefault(static method =>
            method.IsConstructor
            && !method.IsStatic
            && method.Parameters.Count == 1
            && method.Parameters[0].ParameterType.FullName == "FrooxEngine.SessionAnnouncer");
    }

    private static bool IsWatsonWebsocketLocalhostConstructorPattern(
        Mono.Collections.Generic.Collection<Instruction> instructions,
        int index)
    {
        return instructions[index].OpCode == CecilOpCodes.Ldstr
            && instructions[index].Operand is "localhost"
            && instructions[index + 1].OpCode == CecilOpCodes.Ldarg_0
            && instructions[index + 2].OpCode == CecilOpCodes.Call
            && instructions[index + 2].Operand is MethodReference { Name: "get_Port" }
            && instructions[index + 3].OpCode == CecilOpCodes.Ldc_I4_0
            && IsExpectedWatsonWebsocketConstructor(instructions[index + 4]);
    }

    private static bool IsExpectedWatsonWebsocketConstructor(Instruction instruction)
    {
        return instruction.OpCode == CecilOpCodes.Newobj
            && instruction.Operand is MethodReference
            {
                DeclaringType.FullName: "WatsonWebsocket.WatsonWsServer",
                Name: ".ctor",
                Parameters.Count: 3,
            } constructor
            && constructor.Parameters[0].ParameterType.FullName == "System.String"
            && constructor.Parameters[1].ParameterType.FullName == "System.Int32"
            && constructor.Parameters[2].ParameterType.FullName == "System.Boolean";
    }

    private static bool IsResoniteLinkAnnounceEndpointDefaultPattern(
        Mono.Collections.Generic.Collection<Instruction> instructions,
        int index)
    {
        return instructions[index].OpCode == CecilOpCodes.Ldarg_0
            && instructions[index + 1].OpCode == CecilOpCodes.Ldsfld
            && instructions[index + 1].Operand is FieldReference { FullName: "System.Net.IPAddress System.Net.IPAddress::Broadcast" }
            && instructions[index + 2].OpCode == CecilOpCodes.Ldc_I4
            && instructions[index + 2].Operand is 12512
            && IsExpectedIPEndPointConstructor(instructions[index + 3])
            && instructions[index + 4].OpCode == CecilOpCodes.Stfld
            && instructions[index + 4].Operand is FieldReference
            {
                FullName: "System.Net.IPEndPoint FrooxEngine.LAN_SessionAnnouncer::resoniteLinkAnnounceEndpoint",
            };
    }

    private static bool IsExpectedIPEndPointConstructor(Instruction instruction)
    {
        return instruction.OpCode == CecilOpCodes.Newobj
            && instruction.Operand is MethodReference
            {
                DeclaringType.FullName: "System.Net.IPEndPoint",
                Name: ".ctor",
                Parameters.Count: 2,
            } constructor
            && constructor.Parameters[0].ParameterType.FullName == "System.Net.IPAddress"
            && constructor.Parameters[1].ParameterType.FullName == "System.Int32";
    }

    private static string GetAssemblyPath(string assemblyFileName)
    {
        return Path.Combine(AppContext.BaseDirectory, assemblyFileName);
    }

    private static List<CodeInstruction> CreateListenerHostPattern()
    {
        Assembly.LoadFrom(GetAssemblyPath("FrooxEngine.dll"));
        Assembly.LoadFrom(GetAssemblyPath("WatsonWebsocket.dll"));

        Type hostType = AccessTools.TypeByName("FrooxEngine.ResoniteLinkHost")
            ?? throw new InvalidOperationException("FrooxEngine.ResoniteLinkHost was not loaded.");
        Type serverType = AccessTools.TypeByName("WatsonWebsocket.WatsonWsServer")
            ?? throw new InvalidOperationException("WatsonWebsocket.WatsonWsServer was not loaded.");
        MethodInfo portGetter = AccessTools.PropertyGetter(hostType, "Port")
            ?? throw new InvalidOperationException("ResoniteLinkHost.Port getter was not found.");
        ConstructorInfo serverConstructor = AccessTools.Constructor(serverType, [typeof(string), typeof(int), typeof(bool)])
            ?? throw new InvalidOperationException("WatsonWsServer(string,int,bool) constructor was not found.");

        return
        [
            new(ReflectionOpCodes.Ldstr, "localhost"),
            new(ReflectionOpCodes.Ldarg_0),
            new(ReflectionOpCodes.Call, portGetter),
            new(ReflectionOpCodes.Ldc_I4_0),
            new(ReflectionOpCodes.Newobj, serverConstructor),
        ];
    }

    private static List<CodeInstruction> CreateResoniteLinkAnnounceEndpointPattern()
    {
        Assembly.LoadFrom(GetAssemblyPath("FrooxEngine.dll"));

        Type announcerType = AccessTools.TypeByName("FrooxEngine.LAN_SessionAnnouncer")
            ?? throw new InvalidOperationException("FrooxEngine.LAN_SessionAnnouncer was not loaded.");
        FieldInfo broadcastField = AccessTools.Field(typeof(IPAddress), nameof(IPAddress.Broadcast))
            ?? throw new InvalidOperationException("IPAddress.Broadcast field was not found.");
        ConstructorInfo endpointConstructor = AccessTools.Constructor(typeof(IPEndPoint), [typeof(IPAddress), typeof(int)])
            ?? throw new InvalidOperationException("IPEndPoint(IPAddress,int) constructor was not found.");
        FieldInfo endpointField = AccessTools.Field(announcerType, "resoniteLinkAnnounceEndpoint")
            ?? throw new InvalidOperationException("LAN_SessionAnnouncer.resoniteLinkAnnounceEndpoint was not found.");

        return
        [
            new(ReflectionOpCodes.Ldarg_0),
            new(ReflectionOpCodes.Ldsfld, broadcastField),
            new(ReflectionOpCodes.Ldc_I4, 12512),
            new(ReflectionOpCodes.Newobj, endpointConstructor),
            new(ReflectionOpCodes.Stfld, endpointField),
        ];
    }

    private static List<CodeInstruction> InvokeTranspiler(string patchTypeName, string methodName, List<CodeInstruction> instructions)
    {
        Type patchType = typeof(ResoniteLinkNetworkAccessMod).Assembly.GetType(patchTypeName)
            ?? throw new InvalidOperationException($"{patchTypeName} was not found.");
        MethodInfo method = patchType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{patchTypeName}.{methodName} was not found.");

        return (List<CodeInstruction>)method.Invoke(null, [instructions])!;
    }

    private static void AssertTranspilerThrows<TException>(
        string patchTypeName,
        string methodName,
        List<CodeInstruction> instructions)
        where TException : Exception
    {
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
            InvokeTranspiler(patchTypeName, methodName, instructions));

        Assert.IsType<TException>(exception.InnerException);
    }

    private static TypeDefinition GetRequiredType(AssemblyDefinition assembly, string fullName)
    {
        return assembly.MainModule.GetType(fullName)
            ?? throw new InvalidOperationException(BuildMissingMemberMessage(assembly, fullName));
    }

    private static string BuildMissingMemberMessage(AssemblyDefinition assembly, string member)
    {
        return $"Required member '{member}' was not found in '{assembly.MainModule.FileName}' version '{assembly.Name.Version}'.";
    }
}
