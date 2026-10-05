# VyOS server helpers

Templates generate reviewable text only. They do not connect to routers, apply changes or alter strongSwan-managed files. Run `show version`, select the matching supported **1.4** or **1.5** release family, and enter the real interfaces/endpoints. Rolling and custom builds fail closed until their schemas are reviewed. These are source-reviewed templates, not lab-appliance acceptance-test results.

| Template | Behavior |
|---|---|
| Legacy mobile IPsec | Refuses unverified PSK/XAUTH recipe; explains the server contract needed |
| L2TP/IPsec | PSK, local or RADIUS authentication, safe explicit /24 pool |
| WireGuard site-to-site | Peer/key placeholders, AllowedIPs, routes, NAT exclusion |
| OpenVPN site-to-site | pfSense CA-backed VyOS client; PKI prerequisites and TLS settings review |
| Generic IKEv2 | X.509 server + local EAP; not a replacement for lab PSK/XAUTH |
| Generic IPsec site-to-site | IKEv2/PSK and policy selectors; lab's pfSense pair uses its own checklist |

Unknown/invalid fields produce a clear error instead of partial commands. Template secret values are placeholders. Put real secrets directly into a protected router session. The IKEv2 RADIUS variant is not emitted because its full syntax is outside the reviewed recipe. Use the exact release schema to extend support.

Review `compare` before `commit`, then validate before `save`. Keep independent console access during changes. Check existing named objects and rule numbers; the NAT exemption rule must be unused and precede matching translation rules. Audit destination NAT separately and scope public port forwards to the public interface/address. For all three site-to-site links, check native source/destination addresses in inner captures. [VyOS NAT policy example](https://docs.vyos.io/en/1.5/configexamples/policy-based-ipsec-and-firewall.html).

Firewall changes are described, not blindly inserted into unknown chains. Permit outer VPN traffic to the router and only the required inner traffic through it. A route is not an access-control rule. Confirm reverse routes, VPN pools/selectors and DNS reachability.

Read-only diagnostics selected by template:

```text
show version
show ip route
show vpn ike sa
show vpn ipsec sa
show vpn ipsec remote-access summary
show l2tp-server sessions
show l2tp-server statistics
show interfaces wireguard wg0 summary
show openvpn client
```

Use only relevant lines and substitute the actual WireGuard interface. `show configuration commands` is deliberately absent because it can expose secrets. Paste operational results into the current diagnostic session, inspect redaction, and export only the sanitized bundle. Exact source links and review date are in [Sources](Sources.md).
