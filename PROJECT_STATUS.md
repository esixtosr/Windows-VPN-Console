# Windows VPN Console — project status

## Completed phases
- .NET 10 solution and WPF MVVM application; nine pages, adaptive profiles, lab/generic modes and editable topology.
- Native Windows adapter, OpenVPN/WireGuard adapters, Shrew/NCP interactive handoffs and configurable Mock provider.
- Secret-free JSON profiles, optional Windows Credential Manager storage, certificate metadata and redacted logs/clipboard/evidence.
- IPv4 route validation, deterministic troubleshooting, all seven checkoff catalogs, source-reviewed VyOS/pfSense helpers, client templates and packet-capture guidance.
- 122 automated tests; Windows WPF and extracted self-contained release smoke checks; documentation, screenshot, licensing, presets, CI and portable packaging.
- Repository: https://github.com/esixtosr/Windows-VPN-Console.

## Current phase
v0.2.0 software acceptance completed as Windows VPN Console, with a beginner README/user guide, general-purpose first-run defaults, and optional lab mode. Existing storage and provider identifiers remain compatible. The tag workflow repeats acceptance and publishes the public portable download. Live lab/server acceptance remains an explicit external verification task.

v0.2.1-preview.1 focuses on interface cleanup and truthful network evidence. Independent IKEv2 engine work is deferred at the user's request. Earlier v1.0.0 capability/compatibility groundwork remains; this is not a v1.0.0 release.

## Interface preview — 2026-10-09
- Original multi-size shield icon embedded in the EXE and WPF window; vector source and repeatable asset generator included.
- Smaller shell, navigation icons, dark dropdowns, friendly enum labels, responsive forms, collapsed technical details and activity log.
- External clients retain ownership of authentication and connection control. The editor no longer collects unused secrets or shows Windows-only IKEv2 guidance for NCP.
- Dashboard separates observed private-route address/interface evidence from provider status and shows when diagnostics were collected. Equal-cost routes, conflicting subroutes, missing targets and down adapters do not select a misleading address.
- Local Release cross-build: zero warnings/errors. Managed tests: 136 passed; two Windows-only integration checks skipped on macOS. Windows UI/portable smoke checks pending for this preview.
- Public branch upload was blocked by the safety review pending explicit user approval. Nothing was pushed and the stable release was not changed. The local preview ZIP is cross-built; it must not be described as Windows-runtime or visually verified until those checks run.
- User-supplied screenshot reports NCP Connection established. Their local diagnostic report contains a private adapter address and a specific private-network route with a separate Internet interface. This supports a successful external-client connection report, not independently verified private-host access or encryption. No live reports, credentials or screenshots are packaged.
- No router, NCP profile, authentication mode or saved-user-data migration is performed by this UI update.

## v0.2.0 validation — 2026-10-08
- Windows CI https://github.com/esixtosr/Windows-VPN-Console/actions/runs/37878883551 at checkpoint `10d11d4`: **122/122 tests PASS**, no skipped tests.
- Normal-build and extracted `Windows-VPN.exe` smoke runs: **34 assertions PASS** each, including a fresh generic workspace with no prefilled lab endpoint, optional lab workflows, and all existing provider/mock/redaction checks.
- Inspected Windows screenshots of the new general-purpose dashboard, simulated dashboard and renamed About/version display. Updated the documentation screenshot.
- Local cross-build: zero warnings/errors. All 120 cross-platform tests passed; the two Windows-only tests subsequently passed in CI.
- Reviewed README/user-guide local links, public download steps, cross-VM profile export/import limitations and backward-compatible storage behavior.

## Public release review — 2026-10-08
- Reviewed tracked file names and scanned all 125 historical Git blobs for common credential-token, private-key-block and credential-URL patterns; no matches and no secret-bearing VPN files, key containers, captures or PDFs were found.
- Scanned all nine existing workflow runs (22 available log files) using the same credential patterns; no matches were found.
- Stored test data is synthetic; package screenshots are captured from the Mock provider. This is a targeted publication review, not a guarantee that arbitrary future files or logs are safe to publish.

## v0.1.1 patch validation — 2026-10-06
- Windows CI https://github.com/esixtosr/Windows-VPN-Console/actions/runs/37511988054 at checkpoint `ae3c6cb`: **122/122 tests PASS**, no skipped tests.
- Both normal-build and extracted self-contained EXE smoke runs: **33 assertions PASS** across nine pages, including native L2TP provisioning/route cleanup and Credential Manager covered by the separate integration tests.
- Captured nine real dropdown fields in closed, focused, disabled and expanded states. Inspected enum, string, integer and object-name selections, including all fields shown in the user's screenshots. Popup content is captured separately from the main window.
- Navigation, primary buttons, DataGrid and logs remain readable. These are visual render checks; keyboard navigation was not separately automated.
- Full local cross-build: zero warnings/errors; 120 managed tests passed on macOS and the two Windows-only tests were skipped there, then passed in Windows CI.
- README and release notes explain GitHub downloads, portable extraction and manual upgrades. App version is read from the assembly; saved profiles remain under the same Windows user-data directory.

## What actually passed
- Full solution restore/build: zero warnings/errors.
- Windows CI run https://github.com/esixtosr/Windows-VPN-Console/actions/runs/37385355364 at source checkpoint dcf4b93: **122/122 tests PASS**.
- Real Windows L2TP profile creation, split route configuration, disconnected-state query and removal: PASS. No remote dial attempted.
- Real Windows Credential Manager save/read/delete with generated disposable test values: PASS.
- WPF navigation for nine pages, all seven Mock failure stages, connect/disconnect, simulated route analysis, profile serialization, clipboard/evidence redaction, group/policy changes and templates: **33 assertions PASS**.
- Extracted self-contained EXE: same **33 assertions PASS**, with DOTNET_ROOT pointing to an absent runtime installation.
- ZIP integrity, SHA-256, Windows PE x64 architecture and UI screenshots: checked.
- External providers absent on the runner: missing-dependency behavior observed without crashing or claiming CONNECTED.

## Known limits / not validated
- No actual L2TP/IPsec, OpenVPN or WireGuard handshake, lab server reachability, AD authentication or encrypted packet flow was tested. Those require the user's Windows 11 VM, installed engines, endpoints and credentials.
- NCP launch and connection were reported by the user on their installed client; no automated profile-scoped NCP status/control API is verified. Shrew launch remains untested on an installed product. Both remain interactive handoffs and do not terminate unrelated processes.
- No reviewed VyOS 1.4/1.5 CLI recipe exists here for legacy PSK + IKEv1/XAUTH mobile access; obtain the exact course image/configuration. The app does not modify generated strongSwan files.
- Native IKEv2 uses Windows EAP UI; automatic client-certificate selection/policy provisioning is not implemented. OpenVPN rejects unsafe imports, encrypted private-key prompts and unsupported MFA/challenges.
- IPv4 route policy does not prove IPv6 protection, DNS leakage behavior, target authorization or encryption. Manual checkoff entries are attestations.
- Imported provider files can contain secrets. Runtime file/provider-state cleanup after an abnormal process/OS crash may need manual attention; see Security.md.

## Exact next task
For a new development session: read README.md and this file, inspect git status/log, restore/build/test, then continue only a specifically requested enhancement or the live Windows 11 checks in docs/Acceptance.md. Do not recreate completed work.

For lab acceptance: install the required official engine, supply real server/profile credentials locally, validate Local Test first, then AD/RADIUS/LDAP, routing/return paths/NAT/firewalls and public-interface captures. Record actual outcomes and export redacted evidence.

## Release identity
Tag: **v0.2.0**. The successful tag workflow supplies the corresponding release build evidence.
Release: https://github.com/esixtosr/Windows-VPN-Console/releases/tag/v0.2.0
Asset: **Windows-VPN-Console-v0.2.0-win-x64.zip** plus **SHA256SUMS.txt**.
The checksum in the release is authoritative; documentation edits change ZIP bytes, so candidate-build checksums are not reused.

## External dependencies
Build: .NET 10 SDK. Runtime: self-contained Windows x64 package. OpenVPN Community/WireGuard are separate optional installations; Shrew/NCP are optional legacy/commercial clients. No external VPN engines, course PDF or real secrets are distributed.
