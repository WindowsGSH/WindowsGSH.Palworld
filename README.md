# Palworld Dedicated Server

[![WindowsGSH](.github/assets/windowsgsh-badge.svg)](https://windowsgsh.com)
[![Status](https://img.shields.io/badge/status-needs_work-f59e0b)](#status)
[![Module version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FWindowsGSH%2FWindowsGSH.Palworld%2Fmain%2FPalworld.mod%2Fmodule.json&query=%24.version&prefix=v&label=module&color=1E8449)](Palworld.mod/module.json)
[![Requires WindowsGSH](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FWindowsGSH%2FWindowsGSH.Palworld%2Fmain%2FPalworld.mod%2Fmodule.json%3Fbadge%3Dminimum&query=%24.minimumWindowsGshVersion&prefix=v&label=requires%20WindowsGSH&color=2563EB)](Palworld.mod/module.json)
[![Licence](https://img.shields.io/badge/licence-MIT-64748B)](LICENSE.md)

This WindowsGSH module installs, configures, launches, updates, imports and backs up Palworld dedicated and community servers.

## Status

**NEEDS WORK.** The native implementation has preservation-aware configuration, import, readiness checks and Palworld REST actions, but its query claim, console input, shutdown behavior, public-lobby networking and backup layout still require live validation.

## Installation

WindowsGSH installs SteamCMD app `2394010` using anonymous login. The current entry point is `PalServer.exe`; an older `Pal/Binaries/Win64/PalServer-Win64-Shipping-Cmd.exe` layout is recognized for migration and should be refreshed with a Steam validation.

1. Import `Palworld.mod` through Module Management.
2. Create the server and run Install/Update.
3. Configure server identity, gameplay and networking.
4. Keep administration services private, then start the server.

### Import an existing server

Import accepts a direct Palworld install or a WindowsGSM parent containing `serverfiles`, including the older shipping-executable layout. WindowsGSH previews supported `PalWorldSettings.ini` values and offers Copy or Adopt. Preview does not modify the source.

## Configuration

The module manages `Pal/Saved/Config/WindowsServer/PalWorldSettings.ini` and preserves unknown option keys. Managed values include server identity/passwords, player limit, public-lobby endpoint, game rates, difficulty, PvP, logging and REST settings. Mod settings are written separately under `Mods`.

RCON is deprecated and is not a WindowsGSH capability for this module. Existing `RCONEnabled` and `RCONPort` values are preserved rather than silently disabled, allowing operators to retain independently managed legacy clients.

## Networking

| Purpose | Default | Protocol | Exposure |
|---|---:|---|---|
| Player/game connection | 8211 | UDP | Public for remote players; eligible for manual forwarding or UPnP |
| Experimental query override | 27015 | UDP | Unverified; do not rely on it until live-tested |
| Palworld REST API | 8212 | TCP | Private administration endpoint; never automatically forwarded |

`PublicPort` advertises a community server's external endpoint and does not change the listening game port. The REST declaration is optional, private and excluded from listener warnings because the host cannot yet conditionally hide a port when its feature switch is off.

## Query, console, and administration

The server card does not claim a supported query or RCON provider. `network.queryPort` is retained as an experimental launch/config override pending a captured response from a current server.

WindowsGSH implements authenticated Palworld REST actions for server information, players, settings, metrics, announcements, moderation, save and shutdown operations when REST is enabled. Palworld uses the admin password for authentication. Keep the endpoint on localhost/LAN or behind a protected management path.

Output redirection does not prove that Palworld accepts useful stdin commands; treat the embedded console as log output until live testing demonstrates input. Normal close-window/forced fallback also requires save-safety testing.

## Files and backups

Runtime data is primarily under `Pal/Saved`, including configuration, logs and worlds. The module also backs up `Mods` and Pal content PAK mod locations. Some current targets overlap because `Pal/Saved/Config` is already inside `Pal/Saved`; restore behavior and whether narrower targets are preferable remain live-test items.

Stop the server before restoring. Treat archives as sensitive because settings can contain server and administration passwords.

## Known limitations

- A2S/query behavior and player counts are unverified.
- Console input and graceful close require live proof.
- Public-lobby and direct-connect port behavior must be captured from current listening sockets.
- Mod installation remains manual.
- Deprecated RCON settings are preserved but are not exposed or operated by WindowsGSH.
- Backup targets overlap and require restore testing.

## Beta verification checklist

- [ ] Fresh-install/update Steam app `2394010` and confirm the current executable and launch arguments.
- [ ] Round-trip `PalWorldSettings.ini`, including unknown values and independently managed RCON settings.
- [ ] Start, attach/reattach the process, inspect logs and prove a save-safe normal/session-ending stop.
- [ ] Test private and public-lobby joining, capture listening sockets, and prove or remove the query override.
- [ ] Enable REST on a protected endpoint and test information, player, save and shutdown actions without leaking the admin password.
- [ ] Test direct/WindowsGSM imports, crash handling, update, backup and full restore.

## Support

Report module problems through the [WindowsGSH.Palworld issue tracker](https://github.com/WindowsGSH/WindowsGSH.Palworld/issues). Include WindowsGSH/module/server versions, the operation performed, public/private lobby mode and sanitized diagnostics. Never post passwords, player identifiers, public addresses or unredacted archives.

## Support development

If you like the work I do and would like to support continued WindowsGSH and module development, you can contribute here:

- [Ko-fi](https://ko-fi.com/shenniko)
- [PayPal](https://paypal.me/shenniko)

## Trust and source

Modules execute with the same Windows permissions as WindowsGSH. Download from a source you trust and review `Palworld.mod/module.json` and `PalworldModule.cs` before loading modified packages. See [SECURITY.md](SECURITY.md) for safe reporting and credential guidance. Palworld server files remain governed by their publisher's terms.
