# IPsec mobile: establish the server contract first

The requested lab service combines a PSK, user authentication and split-tunnel mobile access to Remote VyOS. IKEv1/XAUTH/Mode Config is a plausible interpretation, but the exact server image and configuration must establish it. The app deliberately does not treat an IKEv2 certificate VPN as an interchangeable solution.

The reviewed [VyOS 1.4](https://docs.vyos.io/en/1.4/configuration/vpn/ipsec/remoteaccess_ipsec.html) and [1.5 remote-access guides](https://docs.vyos.io/en/1.5/configuration/vpn/ipsec/remoteaccess_ipsec.html) describe IKEv2 with X.509 server authentication and EAP. This research did not establish a supported CLI recipe for the requested legacy PSK/XAUTH combination. Accordingly, the Legacy IPsec generator emits **NOT GENERATED**, an explanation, and verification guidance. It does not edit VyOS-generated strongSwan files. Obtain the instructor's known-good image/recipe before continuing this particular server task.

Record the gateway, IKE version, main/aggressive mode if applicable, identities, proposals, XAUTH requirement, Mode Config pool, NAT-T behavior and advertised networks. Confirm the installed Windows client's capability for that exact combination. Native Windows IKEv2 and L2TP/IPsec do not provide generic Shrew-compatible XAUTH.

Use a valid imported/installed Shrew profile rather than fabricating Shrew's secret encoding. If Shrew requires an interactive password prompt, use it. A detected executable or launched window does not prove current Windows 11 compatibility or an established tunnel. NCP is optional, separately installed/licensed, and must never be the only path. Provider limitations are shown in Dependencies.

## Validation workflow

1. Test a local server account. Separate gateway/IKE exchange, PSK authentication, user authentication, child SA, address assignment and routes.
2. Select Strict, Extended or Custom policy. Strict permits VyOS Remote only. Extended additionally requires the WireGuard link, its forwarding rules, HQ AllowedIPs for the mobile pool and the complete return route.
3. Test known private hosts, Internet routing and forbidden networks. Preserve evidence before changing authentication.
4. Migrate to the exact supported domain backend. When local login works but AD login fails, inspect RADIUS/NPS or LDAP evidence; do not label the PSK wrong without evidence.

Check-Off remains UNKNOWN until the actual protocol and outcome can be observed. The app can collect local routes/logs and combine pasted server output; it cannot infer XAUTH success solely from an IPsec process being present.
