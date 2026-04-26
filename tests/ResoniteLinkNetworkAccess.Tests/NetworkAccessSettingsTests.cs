using System.Net;

namespace ResoniteLinkNetworkAccess.Tests;

public sealed class NetworkAccessSettingsTests
{
    [Fact]
    public void DefaultDisabledListenerHostUsesLocalhost()
    {
        Assert.Equal("localhost", NetworkAccessSettings.ResolveListenerHost(enabled: false, configuredHost: "+"));
    }

    [Fact]
    public void DisabledConfigurationDoesNotApplyPatches()
    {
        Assert.False(NetworkAccessSettings.ShouldApplyPatches(enabled: false));
        Assert.False(NetworkAccessSettings.ShouldApplyListenerHostPatch(enabled: false, configuredHost: "+"));
        Assert.False(NetworkAccessSettings.ShouldApplyResoniteLinkAnnouncePatch(
            enabled: false,
            configuredHost: "192.0.2.1",
            configuredPort: 12512));
    }

    [Fact]
    public void EmptyConfigurationValuesDoNotApplySubpatches()
    {
        Assert.False(NetworkAccessSettings.ShouldApplyListenerHostPatch(enabled: true, configuredHost: " "));
        Assert.False(NetworkAccessSettings.ShouldApplyResoniteLinkAnnouncePatch(
            enabled: true,
            configuredHost: " ",
            configuredPort: 12512));
        Assert.False(NetworkAccessSettings.ShouldApplyResoniteLinkAnnouncePatch(
            enabled: true,
            configuredHost: "192.0.2.1",
            configuredPort: 0));
    }

    [Fact]
    public void EnabledConfigurationAppliesSubpatchesIndependently()
    {
        Assert.True(NetworkAccessSettings.ShouldApplyListenerHostPatch(enabled: true, configuredHost: "+"));
        Assert.False(NetworkAccessSettings.ShouldApplyResoniteLinkAnnouncePatch(
            enabled: true,
            configuredHost: string.Empty,
            configuredPort: 12512));
        Assert.False(NetworkAccessSettings.ShouldApplyListenerHostPatch(enabled: true, configuredHost: string.Empty));
        Assert.True(NetworkAccessSettings.ShouldApplyResoniteLinkAnnouncePatch(
            enabled: true,
            configuredHost: "192.0.2.1",
            configuredPort: 12512));
    }

    [Fact]
    public void EnabledWildcardListenerHostUsesConfiguredHost()
    {
        Assert.Equal("+", NetworkAccessSettings.ResolveListenerHost(enabled: true, configuredHost: "+"));
    }

    [Fact]
    public void EnabledBlankListenerHostFallsBackToLocalhost()
    {
        Assert.Equal("localhost", NetworkAccessSettings.ResolveListenerHost(enabled: true, configuredHost: " "));
    }

    [Fact]
    public void EnabledResoniteLinkAnnounceEndpointUsesConfiguredEndpoint()
    {
        IPEndPoint endpoint = NetworkAccessSettings.ResolveResoniteLinkAnnounceEndpoint(
            enabled: true,
            configuredHost: "192.0.2.1",
            configuredPort: 54321);

        Assert.Equal(IPAddress.Parse("192.0.2.1"), endpoint.Address);
        Assert.Equal(54321, endpoint.Port);
    }

    [Fact]
    public void BlankResoniteLinkAnnounceEndpointFallsBackToDefault()
    {
        IPEndPoint endpoint = NetworkAccessSettings.ResolveResoniteLinkAnnounceEndpoint(
            enabled: true,
            configuredHost: string.Empty,
            configuredPort: 54321);

        Assert.Equal(IPAddress.Broadcast, endpoint.Address);
        Assert.Equal(12512, endpoint.Port);
    }
}
