# Project status

## Completed phases
- .NET 10 solution, core models, secret-free profile persistence/import/export and editable group-derived Lab 2 topology.
- WPF MVVM shell with nine pages, adaptive profiles, dependency detection, certificate metadata, settings and redacted live logs.
- Native Windows RAS/L2TP management and IKEv2/SSTP paths, WireGuard adapter, OpenVPN management adapter, Shrew/NCP interactive adapters and configurable mock provider.
- Windows diagnostics, IPv4 route validation, failure analysis, Copy for ChatGPT and sanitized eight-file evidence ZIPs.
- Reviewed VyOS/pfSense helpers, seven checkoff catalogs, PSK/WireGuard configuration generation, docs and preset.
- Private GitHub repository: https://github.com/esixtosr/CNIT455-VPN-Console.
- Complete solution cross-build passes with zero warnings/errors on macOS .NET 10.0.201.
- 64 tests pass locally; two Windows API tests correctly skip on macOS.

## Current phase
Windows CI runtime validation and release acceptance. Final generator review/tests are being added in parallel without restarting completed work.

## What currently works
All projects compile. Core security, profiles, routes, deterministic troubleshooting and mock lifecycle pass tests. Windows adapters are implemented and source-reviewed, but actual OS integration and WPF runtime still need the first CI run. Manual/external engine limitations are explicit in the UI/docs.

## Known issues and limitations
- Development host is macOS; Windows CI will run actual L2TP provisioning/cleanup, Credential Manager and WPF clipboard/navigation/mock/export smoke tests.
- No lab endpoints/credentials or installed external VPN products were supplied for live server acceptance; no real VPN success is claimed.
- Shrew legacy Windows 11 compatibility is unverified; Shrew/NCP safely hand off to their vendor UI and report unobserved status as UNKNOWN.
- No reviewed modern VyOS PSK/IKEv1/XAUTH mobile recipe is generated. Exact course image/instructor configuration remains necessary.
- Native certificate/EAP provisioning is limited; OpenVPN rejects unsafe imports and unsupported encrypted-key/challenge flows.
- IPv4 route results are separate from IPv6/DNS/reachability/encryption evidence. See docs/Acceptance.md.

## Exact next task
1. Inspect the first Windows GitHub Actions run and fix any failures.
2. Finish generator review/tests; run final unit/WPF/portable smoke suite.
3. Review generated screenshots, verify extracted package and checksum.
4. Commit/push final acceptance checkpoint, tag v0.1.0, verify GitHub Release.
5. Download ZIP/checksum into the parent outputs directory and record exact commit/release evidence.

## Build/test status
`dotnet build CNIT455-VPN-Console.sln -c Release`: PASS, zero warnings/errors.
`dotnet test tests/CNIT455.VPN.Tests -c Release --no-build`: 64 PASS, 2 Windows-only SKIP.
Windows CI and portable release: pending.

## External dependencies
Build: .NET 10 SDK. Run: self-contained Windows x64 package once published. External engines are separately installed OpenVPN Community/WireGuard; optional Shrew/NCP. No external engines, course PDF or real secrets are bundled.
