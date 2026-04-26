using System.Net;
using System.Reflection;

namespace ResoniteLinkNetworkAccess.Tests;

public sealed class ResoniteLinkNetworkAccessModTests
{
    [Fact]
    public void PublicMetadataMatchesAssemblyProperties()
    {
        ResoniteLinkNetworkAccessMod mod = new();
        Assembly assembly = typeof(ResoniteLinkNetworkAccessMod).Assembly;

        Assert.Equal("ResoniteLink Network Access", mod.Name);
        Assert.Equal("esnya", mod.Author);
        Assert.False(string.IsNullOrWhiteSpace(mod.Version));
        Assert.Equal("https://github.com/esnya/ResoniteLinkNetworkAccess", mod.Link);
        Assert.NotNull(assembly.GetCustomAttribute<AssemblyTitleAttribute>());
        Assert.NotNull(assembly.GetCustomAttribute<AssemblyCompanyAttribute>());
        Assert.NotNull(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>());
        Assert.Contains(
            assembly.GetCustomAttributes<AssemblyMetadataAttribute>(),
            static metadata => metadata.Key == "RepositoryUrl" && metadata.Value == "https://github.com/esnya/ResoniteLinkNetworkAccess");
    }

    [Fact]
    public void DefaultDisabledListenerHostUsesLocalhost()
    {
        Assert.Equal("localhost", ResoniteLinkNetworkAccessMod.ResolveListenerHost(enabled: false, configuredHost: "+"));
    }

    [Fact]
    public void MissingConfigurationUsesDefaults()
    {
        Assert.Equal("localhost", ResoniteLinkNetworkAccessMod.GetListenerHost());
        IPEndPoint endpoint = ResoniteLinkNetworkAccessMod.GetResoniteLinkAnnounceEndpoint();

        Assert.Equal(IPAddress.Broadcast, endpoint.Address);
        Assert.Equal(12512, endpoint.Port);
    }

    [Fact]
    public void MissingConfigurationDoesNotApplyPatches()
    {
        Assert.False(ResoniteLinkNetworkAccessMod.ShouldApplyPatches());
        Assert.False(ResoniteLinkNetworkAccessMod.ShouldApplyListenerHostPatch());
        Assert.False(ResoniteLinkNetworkAccessMod.ShouldApplyResoniteLinkAnnouncePatch());
    }

    [Fact]
    public void DisabledConfigurationDoesNotApplyPatches()
    {
        Assert.False(ResoniteLinkNetworkAccessMod.ShouldApplyPatches(enabled: false));
        Assert.False(ResoniteLinkNetworkAccessMod.ShouldApplyListenerHostPatch(enabled: false, configuredHost: "+"));
        Assert.False(ResoniteLinkNetworkAccessMod.ShouldApplyResoniteLinkAnnouncePatch(
            enabled: false,
            configuredHost: "192.0.2.1",
            configuredPort: 12512));
    }

    [Fact]
    public void EmptyConfigurationValuesDoNotApplySubpatches()
    {
        Assert.False(ResoniteLinkNetworkAccessMod.ShouldApplyListenerHostPatch(enabled: true, configuredHost: " "));
        Assert.False(ResoniteLinkNetworkAccessMod.ShouldApplyResoniteLinkAnnouncePatch(
            enabled: true,
            configuredHost: " ",
            configuredPort: 12512));
        Assert.False(ResoniteLinkNetworkAccessMod.ShouldApplyResoniteLinkAnnouncePatch(
            enabled: true,
            configuredHost: "192.0.2.1",
            configuredPort: 0));
    }

    [Fact]
    public void EnabledConfigurationAppliesSubpatchesIndependently()
    {
        Assert.True(ResoniteLinkNetworkAccessMod.ShouldApplyListenerHostPatch(enabled: true, configuredHost: "+"));
        Assert.False(ResoniteLinkNetworkAccessMod.ShouldApplyResoniteLinkAnnouncePatch(
            enabled: true,
            configuredHost: string.Empty,
            configuredPort: 12512));
        Assert.False(ResoniteLinkNetworkAccessMod.ShouldApplyListenerHostPatch(enabled: true, configuredHost: string.Empty));
        Assert.True(ResoniteLinkNetworkAccessMod.ShouldApplyResoniteLinkAnnouncePatch(
            enabled: true,
            configuredHost: "192.0.2.1",
            configuredPort: 12512));
    }

    [Fact]
    public void EnabledWildcardListenerHostUsesConfiguredHost()
    {
        Assert.Equal("+", ResoniteLinkNetworkAccessMod.ResolveListenerHost(enabled: true, configuredHost: "+"));
    }

    [Fact]
    public void EnabledBlankListenerHostFallsBackToLocalhost()
    {
        Assert.Equal("localhost", ResoniteLinkNetworkAccessMod.ResolveListenerHost(enabled: true, configuredHost: " "));
    }

    [Fact]
    public void EnabledResoniteLinkAnnounceEndpointUsesConfiguredEndpoint()
    {
        IPEndPoint endpoint = ResoniteLinkNetworkAccessMod.ResolveResoniteLinkAnnounceEndpoint(
            enabled: true,
            configuredHost: "192.0.2.1",
            configuredPort: 54321);

        Assert.Equal(IPAddress.Parse("192.0.2.1"), endpoint.Address);
        Assert.Equal(54321, endpoint.Port);
    }

    [Fact]
    public void BlankResoniteLinkAnnounceEndpointFallsBackToDefault()
    {
        IPEndPoint endpoint = ResoniteLinkNetworkAccessMod.ResolveResoniteLinkAnnounceEndpoint(
            enabled: true,
            configuredHost: string.Empty,
            configuredPort: 54321);

        Assert.Equal(IPAddress.Broadcast, endpoint.Address);
        Assert.Equal(12512, endpoint.Port);
    }

    [Fact]
    public void PatchTypeDeclaresHarmonyPatchMetadata()
    {
        Assert.Contains(
            typeof(ResoniteLinkHostStartPatch).GetCustomAttributes(),
            static attribute => attribute.GetType().FullName is "HarmonyLib.HarmonyPatch");
        Assert.DoesNotContain(
            typeof(ResoniteLinkHostStartPatch).GetCustomAttributes(),
            static attribute => attribute.ToString()?.Contains("FrooxEngine", StringComparison.Ordinal) is true);
        Assert.Contains(
            typeof(ResoniteLinkAnnounceEndpointPatch).GetCustomAttributes(),
            static attribute => attribute.GetType().FullName is "HarmonyLib.HarmonyPatch");
        Assert.DoesNotContain(
            typeof(ResoniteLinkAnnounceEndpointPatch).GetCustomAttributes(),
            static attribute => attribute.ToString()?.Contains("FrooxEngine", StringComparison.Ordinal) is true);
    }
}
