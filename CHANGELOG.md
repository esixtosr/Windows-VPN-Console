# Changelog

## 0.2.0 — 2026-10-08

- Renamed the application and release package to Windows VPN Console / Windows-VPN.exe.
- Prepared the repository for public downloads and added a plain-language user manual, VM reuse steps, and troubleshooting table.
- New installations start in generic mode with a blank profile; course topology and checklists remain optional in Lab Mode.
- Preserved existing profile, credential and provider identifiers for upgrades from v0.1.x.
- App, diagnostic and Mock version labels now reflect the compiled version.
- Extended Windows smoke coverage to check the fresh generic workspace before exercising optional lab workflows.
- Passed 122 Windows tests and both 34-check UI smoke runs; visually verified the renamed executable and new starting screen.

## 0.1.1 — 2026-10-06

- Passed 122 Windows tests and both 33-assertion smoke runs; visually reviewed nine dropdowns and refreshed the runtime screenshot.

- Fix unreadable dropdown selections and options in the dark interface, including protocol, authentication, tunnel policy, and configuration-template controls.
- Explain how to download the portable executable from GitHub Releases, distinguish release assets from source archives, and update while retaining saved profiles and external configuration paths.
- Preserve the v0.1.0 Windows acceptance record; this patch does not establish new live VPN or external-client compatibility claims.

## 0.1.0 — 2026-10-05

- Initial .NET 10/WPF Windows VPN console with saved lab and generic profiles.
- Native, OpenVPN, WireGuard and external IPsec provider boundaries; developer mock failure scenarios.
- Editable Lab 2 topology, route policies, seven checkoff workflows and source-backed server helpers.
- Redacted diagnostics, route analysis, deterministic troubleshooting and evidence ZIP export.
- Portable win-x64 packaging, automated tests and Windows CI runtime smoke checks.

- Hardened imported OpenVPN/WireGuard profiles, enforced lab UDP/user-auth/PSK requirements, and added generator/security regression coverage.
- Validated native L2TP provisioning/route cleanup and Credential Manager on Windows CI.

- Passed 122 Windows tests and 33 WPF runtime assertions for both build output and extracted self-contained package.
- Included runtime screenshot, upstream license notices and documented live-VM acceptance limits.

Initial release limitations are tracked in PROJECT_STATUS.md and the provider documentation.
