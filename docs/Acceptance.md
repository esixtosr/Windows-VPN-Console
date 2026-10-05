# Release acceptance and live VM checks

This document separates verified software behavior from operations that require a configured VPN server. See PROJECT_STATUS.md for the final CI run and test totals.

## Automated release gates

- Restore and build the complete six-project solution with .NET 10.
- Run unit tests for strict IPv4 parsing, group substitution, reserved subnet warning, profile serialization/import/export, secret exclusion, redaction, longest-prefix/metric routing, failure classification, provider capability checks, mock stages and configuration generation.
- On Windows, provision an app-owned L2TP profile for a documentation-only address, verify its protocol/split route/disconnected status, and remove it. This does not dial a remote server.
- On Windows, round-trip generated test secrets through Credential Manager and delete the test record.
- Launch WPF, instantiate all nine pages, render screenshots, exercise mock connection/failures/disconnection, copy a redacted report through the real Windows clipboard, and inspect exported evidence.
- Publish self-contained win-x64, ZIP it, extract it and run the same smoke test with DOTNET_ROOT pointing at an absent installation.
- Generate and verify SHA256SUMS.txt and publish the same package to the private GitHub release.

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
