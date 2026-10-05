# L2TP over IPsec

Use the Windows-native provider with PSK + username authentication. The lab profile targets HQ VyOS, permits the configured VyOS HQ subnet and keeps Internet traffic outside the tunnel. Supply the gateway, username and transient PSK/password, validate, then connect. Windows prompts for privileged changes when needed; a denied elevation must leave an actionable result.

The server helper targets reviewed [VyOS 1.4](https://docs.vyos.io/en/1.4/configuration/vpn/l2tp.html) and [1.5 syntax](https://docs.vyos.io/en/1.5/configuration/vpn/l2tp.html). It configures an explicit client pool, PPP gateway, IPsec PSK and local or RADIUS authentication. The current pool helper accepts an IPv4 /24, excluding the gateway at `.1`. Choose an unused pool that overlaps neither LANs nor other tunnels. Secret placeholders must be filled privately on the router.

Integrate input rules for UDP/500, UDP/4500 and ESP into the existing firewall. Accept UDP/1701 only with an IPsec match. Allow authorized pool-to-HQ forwarding and route the pool back to the router. Do not add Internet masquerade merely because a vendor example includes it: this lab requires split routing.

First prove LOCAL TEST. Then enter the RADIUS server/port/source, register the VyOS source address as an NPS client, use a matching RADIUS secret, and restrict NPS authorization to the intended AD group. Change the backend to RADIUS and compare results. Direct LDAP is not offered by this server template.

Read-only operational commands:

```text
show l2tp-server sessions
show l2tp-server statistics
show vpn ike sa
show vpn ipsec sa
show ip route
```

Verify the assigned address and actual Windows target route. Internet must stay on the physical adapter; check IPv6 as well. Capture the same test flow outside and inside the tunnel, with matching IPsec counter changes. If PPP is up without proof of the underlying IPsec protection, the encryption item is UNKNOWN.
