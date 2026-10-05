# Project status

## Completed phases
- .NET 10 solution, core models, secret-free profile persistence/import/export and editable group-derived Lab 2 topology.
- WPF MVVM shell with nine pages, adaptive profiles, dependency detection, certificate metadata, settings and redacted live logs.
- Native Windows RAS/L2TP management and IKEv2/SSTP paths, WireGuard adapter, OpenVPN management adapter, Shrew/NCP interactive adapters and configurable mock provider.
- Windows diagnostics, IPv4 route validation, failure analysis, Copy for ChatGPT and sanitized eight-file evidence ZIPs.
- Reviewed VyOS/pfSense helpers, seven checkoff catalogs, PSK/WireGuard configuration generation, docs and preset.
- Private GitHub repository: https://github.com/esixtosr/CNIT455-VPN-Console.
- Complete solution cross-build passes with zero warnings/errors on macOS .NET 10.0.201.
- 120 tests pass locally; two Windows API tests correctly skip on macOS.
- Windows CI run 37384382006 passed all 66 tests then present, including actual native L2TP provisioning/route inspection/removal and Credential Manager save/read/delete. WPF screenshots, mock stages, clipboard/evidence ran before a template smoke fixture failed; fixture fixed in next checkpoint.

## Current phase
Final Windows runtime/package validation. Generator/security review completed; expanded 122-test suite and corrected WPF smoke fixture ready for CI.

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
1. Inspect the next Windows GitHub Actions run and fix any failures.
2. Confirm all 122 tests plus full WPF/portable smoke suite pass.
3. Review generated screenshots, verify extracted package and checksum.
4. Commit/push final acceptance checkpoint, tag v0.1.0, verify GitHub Release.
5. Download ZIP/checksum into the parent outputs directory and record exact commit/release evidence.

## Build/test status
`dotnet build CNIT455-VPN-Console.sln -c Release`: PASS, zero warnings/errors.
`dotnet test tests/CNIT455.VPN.Tests -c Release --no-build`: 120 PASS, 2 Windows-only SKIP.
First Windows build/unit tests PASS (66/66). WPF smoke reached template generation and failed on an unspecified authentication mode; corrected to an explicit certificate + username test. Full rerun and portable package validation pending.

## External dependencies
Build: .NET 10 SDK. Run: self-contained Windows x64 package once published. External engines are separately installed OpenVPN Community/WireGuard; optional Shrew/NCP. No external engines, course PDF or real secrets are bundled.
