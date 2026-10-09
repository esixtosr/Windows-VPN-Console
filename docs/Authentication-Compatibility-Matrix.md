# Authentication Compatibility Matrix

This matrix documents what the console can present, validate, control, or hand off for each VPN protocol. It is deliberately conservative: a username format, adapter, route, or external-client window is not proof that authentication succeeded.

| Protocol | Supported auth workflows in profile model | Preferred engine path | External fallback | Domain-compatible usernames | Current support level |
|---|---|---|---|---|---|
| IKEv2 IPsec | PSK, certificate, EAP username/password, PSK server auth plus EAP-MSCHAPv2 client auth | Under investigation; Windows native IKEv2 is not assumed to satisfy PSK-server plus EAP-client for every gateway | NCP Secure Entry interactive profile | Yes: `DOMAIN\user`, `user@domain`, or local username where the gateway supports it | External fallback documented; no integrated live IKEv2 PSK+EAP claim yet |
| IKEv1/XAUTH | PSK plus XAUTH username/password | No integrated Windows engine selected yet | Shrew Soft or NCP interactive profile where installed and tested | Yes when the gateway/RADIUS backend accepts the format | Legacy external fallback only |
| L2TP/IPsec | PSK plus PPP username/password | Windows native VPN profile | None required for standard Windows L2TP | Yes through Windows/RAS authentication | Native profile management supported for app-owned profiles |
| OpenVPN | Certificate, username/password, or both as defined by imported `.ovpn` profile | OpenVPN Community process with safe profile handling | OpenVPN GUI/client outside the console | Yes when the server plugin/backend accepts the format | Managed external engine; challenge/MFA/encrypted-key prompts remain client-specific |
| WireGuard | Keypair plus optional WireGuard pre-shared key | Official WireGuard for Windows tooling/service | WireGuard GUI outside the console | Not native to WireGuard | Managed external engine; no username/password authentication is exposed |
| Mock | Simulated modes for development | Built into console | Not applicable | Simulated only | Developer/test use only |

## CNIT455 IKEv2 lab target

The known lab target is:

- IKEv2.
- Server authentication: pre-shared key.
- User authentication: EAP-MSCHAPv2.
- Local test username: configured per profile.
- Domain authentication later: handled by the VPN gateway through RADIUS/NPS/Active Directory.

The console must not convert this profile to certificate-only IKEv2, L2TP/IPsec, or IKEv1/XAUTH. Until an integrated Windows-compatible IKEv2 engine is verified for this exact combination, NCP remains the honest fallback.

## Status rules

- A provider state of CONNECTED means only that the selected engine reported a connected state.
- An observed adapter address, route, or DNS setting is network evidence, not authentication proof.
- A domain-looking username is not AD authentication proof.
- ESP/IPsec encryption requires security-association evidence or packet-capture evidence.
- If the gateway or external client rejects credentials, the console should report the most specific observable cause without storing or exposing secrets.
