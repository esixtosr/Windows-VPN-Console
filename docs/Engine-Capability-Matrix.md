# Engine Capability Matrix

This matrix describes what the application can honestly control or observe in v1.0.0 development. A detected executable, service, adapter, route, or process does not by itself prove authentication, encryption, or target reachability.

| Engine | Integration type | Protocols | Authentication support | App-controlled connect/disconnect | Status confidence | Notes |
|---|---|---|---|---|---|---|
| Windows native VPN | Windows Native | L2TP/IPsec, IKEv2, SSTP | L2TP PSK + MSCHAPv2; IKEv2/SSTP through Windows EAP/RAS | Yes for app-owned native profiles; IKEv2 may hand off to Windows UI | Windows-reported state plus route diagnostics | Does not prove every IKEv2 gateway supports PSK-server plus EAP-client auth. |
| WireGuard for Windows | External Managed | WireGuard | Keypair plus optional PSK | Yes for app-owned tunnel services | Service state plus `wg show` handshake/counters | Requires administrator rights and official WireGuard installation. |
| OpenVPN Community | External Managed | OpenVPN | Certificate, username/password, or both for supported profiles | Yes for app-owned OpenVPN process | Management interface state | Rejects scripts, plugins, external secret files, encrypted private-key prompts, and unsupported challenge/MFA. |
| NCP Secure Entry | External Interactive | IKEv2, legacy IPsec | Vendor profile handles PSK, EAP, certificate, or XAUTH as configured | No verified profile-scoped API; app opens NCP | UNKNOWN unless supported by external evidence | Commercial license required; do not infer success from the NCP process/window. |
| Shrew Soft | External Interactive | Legacy IPsec/IKEv1 XAUTH | Vendor profile handles PSK and XAUTH | No safe disconnect/status automation | UNKNOWN unless supported by external evidence | Legacy Windows support only; Windows 11 compatibility is not guaranteed. |
| Mock | Integrated simulation | All app profile types | Simulated | Yes | Simulated only | Developer Mode only; never evidence of a real VPN. |

## Evidence Rules

- Provider-reported CONNECTED is useful but still needs route and target checks.
- Observed adapter address and protected routes are evidence, not proof of authentication.
- ESP encryption requires packet capture or security-association evidence.
- External interactive engines remain UNKNOWN in the console unless a tested provider API is added.
