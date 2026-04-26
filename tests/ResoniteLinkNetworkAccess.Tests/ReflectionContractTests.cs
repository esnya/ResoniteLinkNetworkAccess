using Mono.Cecil;

namespace ResoniteLinkNetworkAccess.Tests;

public sealed class ReflectionContractTests
{
    [Fact]
    public void ResoniteLinkHostShouldExposeExpectedStartMethod()
    {
        using AssemblyDefinition frooxEngineAssembly = AssemblyDefinition.ReadAssembly(GetAssemblyPath("FrooxEngine.dll"));
        TypeDefinition host = GetRequiredType(frooxEngineAssembly, "FrooxEngine.ResoniteLinkHost");
        MethodDefinition? method = host.Methods.FirstOrDefault(static candidate =>
            candidate.Name == "Start"
            && candidate.Parameters.Count == 1
            && candidate.Parameters[0].ParameterType.FullName == "System.Nullable`1<System.Int32>");

        Assert.NotNull(method);
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
        MethodDefinition? method = announcer.Methods.FirstOrDefault(static candidate =>
            candidate.IsConstructor
            && !candidate.IsStatic
            && candidate.Parameters.Count == 1
            && candidate.Parameters[0].ParameterType.FullName == "FrooxEngine.SessionAnnouncer");

        Assert.NotNull(method);
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

    private static string GetAssemblyPath(string assemblyFileName)
    {
        string outputPath = Path.Combine(AppContext.BaseDirectory, assemblyFileName);
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        string repositoryRoot = GetRepositoryRoot();
        return assemblyFileName == "ResoniteLinkNetworkAccess.dll"
            ? Path.Combine(repositoryRoot, "src", "ResoniteLinkNetworkAccess", "bin", "Release", assemblyFileName)
            : outputPath;
    }

    private static string GetRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ResoniteLinkNetworkAccess.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static TypeDefinition GetRequiredType(AssemblyDefinition assembly, string fullName)
    {
        return assembly.MainModule.GetType(fullName)
            ?? throw new InvalidOperationException($"Required type '{fullName}' was not found in '{assembly.MainModule.FileName}'.");
    }
}
