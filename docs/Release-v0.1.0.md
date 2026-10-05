# CNIT 455 VPN Console v0.1.0

Portable .NET 10/WPF utility for Windows 11 x64. Includes lab and generic profiles, seven checkoff workflows, editable topology, configuration helpers, normalized logs, redacted diagnostics and evidence export.

## Run

1. Download `CNIT455-VPN-Console-v0.1.0-win-x64.zip` and `SHA256SUMS.txt` from this release.
2. Extract the ZIP.
3. Run `CNIT455-VPN.exe`. No Visual Studio, .NET SDK or separately installed .NET runtime is required.
4. Set your group and confirm endpoints. Use Dependencies to inspect optional VPN clients; install those separately from the official links when needed.

## Verified

122 automated tests passed on Windows, including temporary native L2TP profile creation, split route configuration, profile removal and Credential Manager save/read/delete. Both WPF build output and the extracted self-contained executable passed 33 assertions for navigation, mock success/failure/disconnect, profile and clipboard redaction, evidence ZIPs, topology and templates. Screenshots and the x64 package/checksum were inspected.

## Limits

No real lab server connection is certified. OpenVPN, WireGuard, Shrew and NCP were not installed on the CI runner; missing-client handling was verified, but vendor launches and live handshakes need your Windows VM. Shrew/NCP adapters hand off interactively and do not equate a launched process with CONNECTED. Shrew's Windows 11 compatibility is unverified. Modern VyOS legacy PSK/IKEv1/XAUTH mobile syntax is not guessed; the helper explains the unsupported recipe. Native IKEv2 certificate/EAP policy remains a Windows configuration step. See Docs/Acceptance.md and Docs/Providers-Research.md in the ZIP.

No proprietary VPN engine, course PDF or real credential is bundled. The executable is unsigned. Original application code is MIT licensed; runtime notices are included.
