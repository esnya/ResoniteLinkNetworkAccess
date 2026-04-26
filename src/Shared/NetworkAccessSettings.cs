using System.Net;

namespace ResoniteLinkNetworkAccess;

internal static class NetworkAccessSettings
{
    public const string DefaultListenerHost = "localhost";
    public const int DefaultResoniteLinkAnnouncePort = 12512;

    public static string ResolveListenerHost(bool enabled, string? configuredHost)
    {
        string host = configuredHost ?? string.Empty;
        return !ShouldApplyListenerHostPatch(enabled, host)
            ? DefaultListenerHost
            : host.Trim();
    }

    public static bool ShouldApplyPatches(bool enabled)
    {
        return enabled;
    }

    public static bool ShouldApplyListenerHostPatch(bool enabled, string? configuredHost)
    {
        return enabled && !string.IsNullOrWhiteSpace(configuredHost);
    }

    public static bool ShouldApplyResoniteLinkAnnouncePatch(bool enabled, string? configuredHost, int configuredPort)
    {
        return enabled
            && !string.IsNullOrWhiteSpace(configuredHost)
            && configuredPort is > 0 and <= IPEndPoint.MaxPort;
    }

    public static IPEndPoint ResolveResoniteLinkAnnounceEndpoint(bool enabled, string? configuredHost, int configuredPort)
    {
        string host = configuredHost ?? string.Empty;
        return !ShouldApplyResoniteLinkAnnouncePatch(enabled, host, configuredPort)
            ? new IPEndPoint(IPAddress.Broadcast, DefaultResoniteLinkAnnouncePort)
            : new IPEndPoint(ResolveHostAddress(host.Trim()), configuredPort);
    }

    private static IPAddress ResolveHostAddress(string host)
    {
        if (IPAddress.TryParse(host, out IPAddress? address))
        {
            return address;
        }

        IPAddress[] addresses = Dns.GetHostAddresses(host);
        return addresses.FirstOrDefault(static candidate => candidate.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault()
            ?? throw new InvalidOperationException($"Host '{host}' did not resolve to an IP address.");
    }
}
