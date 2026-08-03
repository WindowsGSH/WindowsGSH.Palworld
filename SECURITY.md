# Security policy

## Security and trust

The Palworld module executes with the same Windows permissions as WindowsGSH. WindowsGSH cannot guarantee arbitrary third-party or modified module packages. Review executable source before loading it.

## Download modules safely

Obtain this module from the official WindowsGSH.Palworld repository or another source you explicitly trust. Verify the repository, release origin and changed files before installing updates.

## Protect credentials and server data

Server/admin passwords, REST credentials, player information, logs and backups are sensitive. Keep REST and legacy RCON endpoints private, restrict filesystem access and never publish unredacted configuration or support archives. Palworld requires some credentials in its plaintext game configuration.

## Report a vulnerability

Use the [private repository advisory page](https://github.com/WindowsGSH/WindowsGSH.Palworld/security/advisories/new) before opening a public issue. Do not publish working exploits, credentials or private server data.

## Include in a report

Include affected WindowsGSH/module/server versions, module source, reproduction steps, expected and observed behavior, and sanitized diagnostics. Remove passwords, tokens, player identifiers and public addresses.

## Supported versions

Security fixes are provided for the current module release unless stated otherwise. Upgrade to the latest supported WindowsGSH and module versions before reporting a resolved issue.
