# Security model

No telemetry or analytics. Normal app operation requires no cloud account. GitHub is a distribution/development service only.

## Secrets

JSON profiles contain no password, PSK, private-key or RADIUS-secret fields. The default session does not remember secrets. The optional Remember control uses Windows Credential Manager scoped to the current user; there is no homemade encryption. Clearing secrets drops managed references but cannot guarantee immediate erasure of immutable strings from a garbage-collected process.

WireGuard service integration needs protected key material available to LocalSystem. The adapter uses an ACL-restricted directory and machine-scope Windows DPAPI `.conf.dpapi` file, never plaintext runtime configuration. Disconnect removes the app-owned tunnel and its generated file. A tunnel service can survive the UI or a crash; explicitly disconnect it when finished. Machine administrators remain trusted.

Windows L2TP must provision a PSK in the protected Windows VPN profile to connect. The app creates its own profile and removes it on disconnect/failure. A process crash can leave that app-owned system profile; reconnect/disconnect to clean up. Windows protected provider storage is distinct from ordinary profile JSON.

Imported `.ovpn`, `.conf` and Shrew profile files may contain original credentials. The app does not rewrite or silently delete those originals. Treat imports as sensitive. Ordinary JSON exports omit the provider file path. Explicit provider-config export can reveal credentials and must be treated as secret-bearing output.

OpenVPN imports are constrained to reviewed self-contained directives. Executable hooks, plugins, management changes and external credential-file directives are rejected. This deliberately excludes some legitimate vendor configurations; review and adapt using official client export rather than allowing arbitrary imported code.

## Logs and exports

A shared redactor removes known runtime secrets, sensitive key/value lines, inline OpenVPN credentials, PEM private material and WireGuard-shaped keys. Conservative over-redaction is intentional. It runs before logs are persisted or copied and before each evidence ZIP member is written. No redactor can infer every unlabeled secret in arbitrary pasted prose; inspect data before sharing. IPs, hostnames, usernames in provider messages and certificate metadata can remain.

No private certificate keys are exported by the certificate helper. Importing a certificate is an explicit user action. Certificate selection does not bypass server validation; unsupported client-certificate modes must be rejected by provider capabilities.

## Privilege and scope

The app is asInvoker. Privileged VPN operations report why elevation is required; the user may relaunch as administrator. Denial is handled as a failed action with diagnostics. No firewall disabling, global weakening of IPsec, silent driver installs, unrelated process termination or automatic router modification is permitted.

Executable discovery is constrained to Windows system folders and known product installation directories. Profiles do not choose arbitrary executable paths. The native adapter operates on application-owned names. Generated router commands are reviewable text; secrets use placeholders by default and no commands are sent to a router.

## Retention and recovery

Data lives under `%LOCALAPPDATA%\CNIT455-VPN-Console`. Default log retention is 14 days. Use Clear Logs and disconnect before removing the portable app. Windows Credential Manager entries are separate from ZIP contents; disable Remember and save/remove the relevant stored entry when retiring a profile.
