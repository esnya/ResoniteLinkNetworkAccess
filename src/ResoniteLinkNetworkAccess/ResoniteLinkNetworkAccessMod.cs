using System.Net;
using System.Reflection;
using HarmonyLib;
using ResoniteModLoader;
#if USE_RESONITE_HOT_RELOAD_LIB
using ResoniteHotReloadLib;
#endif

namespace ResoniteLinkNetworkAccess;

/// <summary>
/// ResoniteModLoader entry point for ResoniteLink Network Access.
/// </summary>
public sealed class ResoniteLinkNetworkAccessMod : ResoniteMod
{
    internal const string DefaultListenerHost = "localhost";
    internal const int DefaultResoniteLinkAnnouncePort = 12512;

    private const string ModNamespace = "com.nekometer.esnya";
    private static readonly Assembly Assembly = typeof(ResoniteLinkNetworkAccessMod).Assembly;
    private static readonly string HarmonyId = $"{ModNamespace}.{Assembly.GetName().Name}";
    private static readonly Harmony Harmony = new(HarmonyId);
    private static readonly Lock PatchStateLock = new();

    private static ModConfiguration? config;
    private static bool listenerHostPatchApplied;
    private static bool resoniteLinkAnnouncePatchApplied;

    [AutoRegisterConfigKey]
    private static readonly ModConfigurationKey<bool> EnabledKey = new(
        "Enabled",
        "Enable ResoniteLink network access patches. Each sub-patch also requires its own non-empty setting.",
        computeDefault: () => false);

    [AutoRegisterConfigKey]
    private static readonly ModConfigurationKey<string> ListenerHostKey = new(
        "ListenerHost",
        "Host passed to WatsonWebsocket for ResoniteLink. Use '+' to accept any Host header through HttpListener strong wildcard prefix matching.",
        computeDefault: () => string.Empty,
        valueValidator: static value => value is not null);

    [AutoRegisterConfigKey]
    private static readonly ModConfigurationKey<string> ResoniteLinkAnnounceHostKey = new(
        "ResoniteLinkAnnounceHost",
        "Destination host used when announcing ResoniteLink over LAN. Leave empty to keep Resonite defaults.",
        computeDefault: () => string.Empty,
        valueValidator: static value => value is not null);

    [AutoRegisterConfigKey]
    private static readonly ModConfigurationKey<int> ResoniteLinkAnnouncePortKey = new(
        "ResoniteLinkAnnouncePort",
        "Destination port used when announcing ResoniteLink over LAN. Leave 0 to keep Resonite defaults.",
        computeDefault: () => 0,
        valueValidator: static value => value is >= 0 and <= IPEndPoint.MaxPort);

    /// <inheritdoc />
    public override string Name =>
        Assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? Assembly.GetName().Name ?? string.Empty;

    /// <inheritdoc />
    public override string Author =>
        Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;

    /// <inheritdoc />
    public override string Version =>
        Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(static metadata => metadata.Key == "ModVersion")
            ?.Value
        ?? (Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty)
            .Split('+')[0];

    /// <inheritdoc />
    public override string Link =>
        Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(static metadata => metadata.Key == "RepositoryUrl")
            ?.Value ?? string.Empty;

    /// <inheritdoc />
    public override void OnEngineInit()
    {
        Initialize(this);
    }

    internal static string ResolveListenerHost(bool enabled, string? configuredHost)
    {
        string host = configuredHost ?? string.Empty;
        return !ShouldApplyListenerHostPatch(enabled, host)
            ? DefaultListenerHost
            : host.Trim();
    }

    internal static string GetListenerHost()
    {
        return ResolveListenerHost(
            GetConfigValue(EnabledKey, fallback: false),
            GetConfigValue(ListenerHostKey, fallback: string.Empty));
    }

    internal static bool ShouldApplyPatches(bool enabled)
    {
        return enabled;
    }

    internal static bool ShouldApplyPatches()
    {
        return ShouldApplyListenerHostPatch() || ShouldApplyResoniteLinkAnnouncePatch();
    }

    internal static bool ShouldApplyListenerHostPatch(bool enabled, string? configuredHost)
    {
        return enabled && !string.IsNullOrWhiteSpace(configuredHost);
    }

    internal static bool ShouldApplyListenerHostPatch()
    {
        return ShouldApplyListenerHostPatch(
            GetConfigValue(EnabledKey, fallback: false),
            GetConfigValue(ListenerHostKey, fallback: string.Empty));
    }

    internal static bool ShouldApplyResoniteLinkAnnouncePatch(bool enabled, string? configuredHost, int configuredPort)
    {
        return enabled
            && !string.IsNullOrWhiteSpace(configuredHost)
            && configuredPort is > 0 and <= IPEndPoint.MaxPort;
    }

    internal static bool ShouldApplyResoniteLinkAnnouncePatch()
    {
        return ShouldApplyResoniteLinkAnnouncePatch(
            GetConfigValue(EnabledKey, fallback: false),
            GetConfigValue(ResoniteLinkAnnounceHostKey, fallback: string.Empty),
            GetConfigValue(ResoniteLinkAnnouncePortKey, fallback: 0));
    }

    internal static IPEndPoint ResolveResoniteLinkAnnounceEndpoint(bool enabled, string? configuredHost, int configuredPort)
    {
        string host = configuredHost ?? string.Empty;
        return !ShouldApplyResoniteLinkAnnouncePatch(enabled, host, configuredPort)
            ? new IPEndPoint(IPAddress.Broadcast, DefaultResoniteLinkAnnouncePort)
            : new IPEndPoint(ResolveHostAddress(host.Trim()), configuredPort);
    }

    internal static IPEndPoint GetResoniteLinkAnnounceEndpoint()
    {
        return ResolveResoniteLinkAnnounceEndpoint(
            GetConfigValue(EnabledKey, fallback: false),
            GetConfigValue(ResoniteLinkAnnounceHostKey, fallback: string.Empty),
            GetConfigValue(ResoniteLinkAnnouncePortKey, fallback: 0));
    }

#if USE_RESONITE_HOT_RELOAD_LIB
    /// <summary>
    /// Removes Harmony patches before a hot reload cycle.
    /// </summary>
    public static void BeforeHotReload()
    {
        SetPatchesApplied(shouldPatch: false);
    }

    /// <summary>
    /// Reinitializes the mod after a hot reload cycle.
    /// </summary>
    /// <param name="mod">The reloaded mod instance.</param>
    public static void OnHotReload(ResoniteMod mod)
    {
        Initialize(mod);
    }
#endif

    private static void Initialize(ResoniteMod mod)
    {
        ArgumentNullException.ThrowIfNull(mod);

        config = mod.GetConfiguration();
        config?.OnThisConfigurationChanged += HandleConfigurationChanged;

        RefreshPatchState();

#if USE_RESONITE_HOT_RELOAD_LIB
        HotReloader.RegisterForHotReload(mod);
#endif
    }

    private static void HandleConfigurationChanged(ConfigurationChangedEvent _)
    {
        RefreshPatchState();
    }

    private static void RefreshPatchState()
    {
        SetPatchApplied(
            "ListenerHost",
            ref listenerHostPatchApplied,
            ShouldApplyListenerHostPatch(),
            ResoniteLinkHostStartPatch.GetTargetMethod,
            ResoniteLinkHostStartPatch.GetTranspilerMethod);

        SetPatchApplied(
            "ResoniteLinkAnnounce",
            ref resoniteLinkAnnouncePatchApplied,
            ShouldApplyResoniteLinkAnnouncePatch(),
            ResoniteLinkAnnounceEndpointPatch.GetTargetMethod,
            ResoniteLinkAnnounceEndpointPatch.GetTranspilerMethod);
    }

#if USE_RESONITE_HOT_RELOAD_LIB
    private static void SetPatchesApplied(bool shouldPatch)
    {
        SetPatchApplied(
            "ListenerHost",
            ref listenerHostPatchApplied,
            shouldPatch,
            ResoniteLinkHostStartPatch.GetTargetMethod,
            ResoniteLinkHostStartPatch.GetTranspilerMethod);

        SetPatchApplied(
            "ResoniteLinkAnnounce",
            ref resoniteLinkAnnouncePatchApplied,
            shouldPatch,
            ResoniteLinkAnnounceEndpointPatch.GetTargetMethod,
            ResoniteLinkAnnounceEndpointPatch.GetTranspilerMethod);
    }
#endif

    private static void SetPatchApplied(
        string patchName,
        ref bool patchApplied,
        bool shouldPatch,
        Func<MethodBase> getTargetMethod,
        Func<HarmonyMethod> getTranspilerMethod)
    {
        lock (PatchStateLock)
        {
            if (patchApplied == shouldPatch)
            {
                return;
            }

            MethodBase targetMethod = getTargetMethod();
            if (shouldPatch)
            {
                Harmony.Patch(targetMethod, transpiler: getTranspilerMethod());
            }
            else
            {
                Harmony.Unpatch(targetMethod, HarmonyPatchType.Transpiler, HarmonyId);
            }

            patchApplied = shouldPatch;
        }

        DebugFunc(() => $"[ResoniteLinkNetworkAccess] {patchName} Harmony patch {(shouldPatch ? "applied" : "removed")}.");
    }

    private static T GetConfigValue<T>(ModConfigurationKey<T> key, T fallback)
    {
        ArgumentNullException.ThrowIfNull(key);

        return config is null ? fallback : config.TryGetValue(key, out T? value) ? value! : fallback;
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
