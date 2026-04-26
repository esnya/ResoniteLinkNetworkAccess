# ResoniteLink Network Access

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/).

This mod can change the ResoniteLink listener host and LAN announcement destination.

## Security Warning

This mod does not add authentication, authorization, TLS, client identity checks, rate limiting, or any other security boundary to ResoniteLink.

Do not expose ResoniteLink directly to untrusted networks. If you allow non-local connections, protect the port outside this mod with a firewall, VPN, reverse proxy, network ACL, private network, or equivalent infrastructure.

## Installation

Install it like a normal ResoniteModLoader mod:

1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Download `ResoniteLinkNetworkAccess.dll` from GitHub Releases.
3. Place the DLL into your `rml_mods` directory.
4. Launch Resonite or Resonite Headless.

## Settings

- `Enabled`: enables this mod's patches. Default: `false`.
- `ListenerHost`: host passed to ResoniteLink's WebSocket listener. Empty keeps Resonite's default listener unchanged.
- `ResoniteLinkAnnounceHost`: UDP destination host used for ResoniteLink LAN announcements. Empty keeps Resonite's default announcement destination unchanged.
- `ResoniteLinkAnnouncePort`: UDP destination port used with `ResoniteLinkAnnounceHost`. `0` keeps Resonite's default announcement port unchanged.

`ListenerHost` and announcement settings are patched independently. With `Enabled=true`, a subpatch is applied only when its own required setting is non-empty.

Useful `ListenerHost` values include:

- `localhost`
- `*`
- `+`
- an explicit hostname or IP address

`+` uses HttpListener strong wildcard prefix matching, which is useful when Host header flexibility is needed.

Useful `ResoniteLinkAnnounceHost` values include:

- a subnet broadcast address
- an explicit unicast IP address
- a resolvable host name

`ResoniteLinkAnnounceHost` changes the UDP announcement destination only. It does not change the host value inside the announcement payload.
