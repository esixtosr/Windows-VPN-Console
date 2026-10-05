# pfSense worksheets

The application generates guided checklists and never reverse engineers or edits pfSense XML. Navigation labels can vary by pfSense release/package. Record the deployed version and keep a configuration backup under your normal administrative process.

**IPsec site-to-site:** match PSK, identities and Phase 1 proposals on both routers. Phase 2 pairs pfSense HQ and Remote networks, reversed at the other end. Leave address translation unset, authorize IPsec/LAN forwarding and audit outbound/1:1/DNAT rules. Add any client VPN pool selectors required for cross-site access on both ends. [Official PSK example](https://docs.netgate.com/pfsense/en/latest/recipes/ipsec-s2s-psk.html).

**OpenVPN DMZ link:** pfSense owns the CA and server. Use UDP/TLS with a unique VyOS client certificate and an unused pool larger than `/30` for the helper's client/server design. The remote DMZ requires both a system route and a client-specific override bound to the certificate common name. Avoid redirect-gateway on this site-to-site service. [TLS site-to-site guide](https://docs.netgate.com/pfsense/en/latest/recipes/openvpn-s2s-tls.html).

**OpenVPN client access:** configure LDAP under System > User Manager > Authentication Servers; test under Diagnostics > Authentication before selecting it as the VPN backend. Enter host/TLS trust, base DN, bind identity and allowed group. Do not paste bind passwords into diagnostics. Full-tunnel clients need DNS and Internet egress NAT, while private IPsec-bound flows keep native addresses. [LDAP reference](https://docs.netgate.com/pfsense/en/latest/usermanager/ldap.html) and [backend selection](https://docs.netgate.com/pfsense/en/latest/vpn/openvpn/configure-server-backend.html).

**WireGuard client access:** use the supported package, add a tunnel, register the client's public key, host AllowedIPs and matching PSK, and permit UDP input. Assign interfaces/rules as directed by the package. Client defaults travel through the VPN; server peer AllowedIPs identify the client. [Remote-access guide](https://docs.netgate.com/pfsense/en/latest/recipes/wireguard-ra.html).

The Check-Off page provides evidence items for every connection. Record status/counters, bidirectional host tests, routing, authentication and encrypted-flow observations individually. For packet capture, compare WAN and inner interfaces at the same time; inbound WAN packets can appear in a capture even when the firewall drops them. [Capture behavior](https://docs.netgate.com/pfsense/en/latest/diagnostics/packetcapture/index.html).
