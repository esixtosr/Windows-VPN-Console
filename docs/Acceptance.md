# Release acceptance and live VM checks

This document separates verified software behavior from operations that require a configured VPN server.

## Verified Windows results

### v0.2.1 interface refresh

[CI run 38009185217](https://github.com/esixtosr/Windows-VPN-Console/actions/runs/38009185217), checkpoint `5484fd6`, passed **138/138 tests** and both **44-assertion** smoke runs with the final v0.2.1 application version. The normal build and extracted portable app exercised all nine pages, icon loading, dark dropdown labels/states, compact layouts, external-client evidence display and profile switching. Refreshing the editor preserves the selected engine, authentication and in-memory secrets. UI smoke mode blocks real-provider connect/disconnect calls. Windows-rendered screens were inspected. The v0.2.1 tag workflow repeats these gates for the exact release package before publication.

Observed route/adapter fixtures do not establish real VPN authentication, private-host access or encryption. External-client state remains separate from those observations.

### v0.2.0 public rebrand

[CI run 37878883551](https://github.com/esixtosr/Windows-VPN-Console/actions/runs/37878883551), checkpoint `10d11d4`, passed **122/122 tests** and both **34-assertion** smoke runs. The renamed `Windows-VPN.exe` launched from the extracted self-contained ZIP. A new assertion verifies generic mode, one blank non-lab profile and no prefilled gateway on first start. The smoke then opts into lab mode and repeats the existing nine-page, dropdown, simulated-provider and evidence checks. Screenshots confirm the general-purpose dashboard and current product/version labels. The tag workflow repeats the gates for the exact release.

### v0.1.1 dropdown patch

[CI run 37511988054](https://github.com/esixtosr/Windows-VPN-Console/actions/runs/37511988054), checkpoint `ae3c6cb`, passed **122/122 tests** and both **33-assertion** WPF smoke runs. Nine real dropdowns were captured closed, focused, disabled and expanded. Visual review confirmed readable VPN type, authentication, tunnel policy, server-template and backend values, along with string, integer and object-name choices. Expanded popup windows were rendered separately. Primary buttons, navigation, tables and logs were also reviewed. This is visual and existing functional acceptance; keyboard navigation and live VPN clients were not newly exercised. The v0.1.1 tag workflow repeats the gates for the exact release package.

### v0.1.0 baseline

[CI run 37385355364](https://github.com/esixtosr/Windows-VPN-Console/actions/runs/37385355364), source checkpoint `dcf4b93`, passed restore/build, all **122 tests**, publishing and both normal-build/portable-executable smoke runs. The GitHub-hosted Windows runner exercised real VpnClient cmdlets, Windows Credential Manager, WPF rendering, clipboard and ZIP filesystem operations. Each smoke run passed **33 assertions** across nine pages. The portable EXE ran from an extracted ZIP with DOTNET_ROOT redirected to a nonexistent installation. Local inspection verified the ZIP SHA-256, archive integrity and PE x64 machine type.

The runner detected Windows native VPN/PowerShell; OpenVPN, WireGuard, Shrew and NCP were absent. Missing-client detection was exercised. Vendor launches and real VPN handshakes were not tested. Route-analysis tests use deterministic route fixtures; native provisioning verified configured split-tunnel routes but did not dial a server. The runner is a hosted Windows environment, not a supplied Windows 11 lab VM.

The v0.1.0 tag workflow reruns the same gates for the release commit and publishes its own checksum alongside the package. See PROJECT_STATUS.md and the repository Actions/Release pages.

## Automated release gates

- Restore and build the complete six-project solution with .NET 10.
- Run unit tests for strict IPv4 parsing, group substitution, reserved subnet warning, profile serialization/import/export, secret exclusion, redaction, longest-prefix/metric routing, failure classification, provider capability checks, mock stages and configuration generation.
- On Windows, provision an app-owned L2TP profile for a documentation-only address, verify its protocol/split route/disconnected status, and remove it. This does not dial a remote server.
- On Windows, round-trip generated test secrets through Credential Manager and delete the test record.
- Launch WPF, instantiate all nine pages, render screenshots, exercise mock connection/failures/disconnection, copy a redacted report through the real Windows clipboard, and inspect exported evidence.
- Publish self-contained win-x64, ZIP it, extract it and run the same smoke test with DOTNET_ROOT pointing at an absent installation.
- Generate SHA256SUMS.txt, publish the same package to the public GitHub release, then anonymously download and verify the published ZIP and checksum.

## Fresh Windows 11 VM acceptance

No lab credentials, lab router access, or third-party VPN installation was supplied to the development host. Before relying on this tool for a graded checkoff, perform these real endpoint tests:

1. Extract the release, launch without an SDK, and verify the detected engine paths and privilege.
2. L2TP: use Local Test credentials/PSK, connect, confirm assigned address and target route, disconnect and confirm the app-owned native profile is removed. Then configure server RADIUS and repeat. Distinguish client/server authentication failures from transport failures.
3. WireGuard: install the official client explicitly, generate keys, configure the peer and PSK, install the tunnel, send permitted traffic, verify handshake/byte counts and actual IPv4/IPv6 routes, then disconnect and confirm service/runtime cleanup.
4. OpenVPN: install Community explicitly, import a reviewed self-contained UDP profile, connect and validate full-tunnel routing, DNS and the remote pfSense network through site-to-site IPsec. Disconnect and confirm the owned process/transient files are gone.
5. External IPsec: import a valid Shrew profile in the vendor manager or use a licensed NCP installation. Check the actual VyOS image's IKE/authentication support first. The console's launch handoff is not proof that the tunnel is up.
6. For each of the three site-to-site networks, validate both directions with native addresses, explicit NAT exemptions, firewall/return routes and packet captures. Do not infer encryption from UDP traffic alone.
7. Validate forbidden networks and the instructor-selected Strict/Extended client policy. Add evidence notes, then export and inspect the ZIP before sharing.

## Explicit limitations

- No reviewed modern VyOS CLI recipe was established for legacy PSK + IKEv1/XAUTH mobile access. The generator explains this instead of emitting invented commands or modifying managed strongSwan files.
- Shrew's Windows 11 compatibility and safe per-profile status/disconnect automation are unverified. NCP automation remains manual until a documented version-specific contract is available.
- Native IKEv2 delegates EAP interaction/policy to Windows. Automatic selection of a specific client certificate is not implemented; the certificate browser/import helper never exports private keys.
- OpenVPN imports intentionally reject scripts/plugins, arbitrary file references, encrypted private keys and unsupported challenge/MFA flows. Use the vendor client when those are required.
- IPv4 route policy is measured; IPv6, DNS leak behavior, destination reachability, server authorization and encryption require separate evidence.
- Real target-server connections cannot be certified by mocks or CI. No lab connectivity success is claimed in the release.
