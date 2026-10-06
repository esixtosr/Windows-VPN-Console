# Changelog

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
