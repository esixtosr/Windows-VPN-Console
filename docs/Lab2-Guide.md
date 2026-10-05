# CNIT 455 — Lab 2 guide

The preset summarizes the supplied requirements; it does not contain or reproduce the course PDF. Set the group number before creating profiles. Group 33 is only the default. Edit the topology for your actual deployment.

| Role | Default pattern |
|---|---|
| Public segment | `44.104.x.0/24` |
| VyOS public | `44.104.x.4/24` |
| pfSense public | `44.104.x.5/24` |
| Additional pfSense NAT address | `44.104.x.6/24` |
| VyOS HQ / Remote | `192.168.2.0/24` / `192.168.6.0/24` |
| pfSense HQ / Remote | `192.168.3.0/24` / `192.168.4.0/24` |
| pfSense DMZ / VyOS DMZ | `192.168.1.0/24` / `172.18.x.0/24` |

`192.168.5.0/24` is excluded by the supplied assignment. The original and new routers are described with reused public addresses. These values do **not** establish two distinct peer endpoints. Confirm each VM/interface mapping and enter the actual peer address before generating a site-to-site configuration. The extra `.6` NAT address is not proof of the Remote pfSense's identity.

| VPN | Required path | Internet behavior |
|---|---|---|
| IPsec site-to-site | pfSense HQ ↔ pfSense Remote | Native LAN addresses, no VPN NAT |
| WireGuard site-to-site | VyOS HQ ↔ VyOS Remote | UDP, keypairs + PSK, no VPN NAT |
| OpenVPN site-to-site | pfSense DMZ ↔ VyOS DMZ | UDP, pfSense CA, no VPN NAT |
| IPsec client | Windows → Remote VyOS → VyOS Remote | Split |
| L2TP/IPsec client | Windows → HQ VyOS → VyOS HQ | Split |
| OpenVPN client | Windows → HQ pfSense → pfSense HQ | Full |
| WireGuard client | Windows → Remote pfSense → pfSense Remote | Full |

For IPsec mobile, select **Strict** (VyOS Remote only), **Extended** (also VyOS HQ through WireGuard), or **Custom** permitted networks. The instructor's checkoff expectation resolves the conflicting assignment wording. Client routes are not server authorization; enforce exclusions in the routers' forwarding rules too.

Start with LOCAL TEST for user-authenticated access. Record a successful connection, allocated address and private-host test before switching the backend to AD/RADIUS/LDAP. If transport previously worked and directory authentication now fails, inspect NPS/LDAP reachability, shared secret, account state and group policy before changing VPN proposals.

Each of the seven Check-Off sets begins at UNKNOWN. A marked result is human-supplied evidence, not automatic certification. Record the host/service tested, timestamp, expected route, observed route and server evidence. A ping timeout does not prove that a firewall intentionally denied access.

Use Diagnostics to collect routes/adapters/logs; paste sanitized server output when local visibility is incomplete. For full tunnels, verify Internet egress and both IP families. For split tunnels, confirm private routes use the VPN while Internet routes stay physical. Save the evidence bundle only after reviewing its preview. See [pfSense](pfSense.md), [VyOS](VyOS.md), and each protocol guide.

## Packet-capture checkoff

Capture a brief, non-sensitive test flow on a public interface and its inner LAN/tunnel interface. Record the time, negotiated SA/TLS/handshake and counter changes. Inner captures should show the native addresses; public captures should correlate to the protected flow without its plaintext payload. UDP/4500, UDP/1194 or UDP/51820 alone is insufficient evidence of encryption. Keep the result UNKNOWN if the interface or capture interval is uncertain. Raw captures are not collected automatically. See [Netgate packet capture](https://docs.netgate.com/pfsense/en/latest/diagnostics/packetcapture/index.html).
