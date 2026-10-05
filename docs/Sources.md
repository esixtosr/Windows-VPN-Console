# Configuration source register

Reviewed 2026-10-05. The course-specific network/policy requirements come from the user's supplied specification. The lab PDF is neither distributed nor copied. Official references below establish command families and worksheet behavior; actual routers, Windows clients and directory policies still require live acceptance testing.

| Feature | Official source |
|---|---|
| VyOS 1.4 L2TP, local/RADIUS, IPsec, diagnostics | https://docs.vyos.io/en/1.4/configuration/vpn/l2tp.html |
| VyOS 1.5 L2TP | https://docs.vyos.io/en/1.5/configuration/vpn/l2tp.html |
| VyOS 1.4 WireGuard | https://docs.vyos.io/en/1.4/configuration/interfaces/wireguard.html |
| VyOS 1.5 WireGuard | https://docs.vyos.io/en/1.5/configuration/interfaces/wireguard.html |
| VyOS 1.4 OpenVPN | https://docs.vyos.io/en/1.4/configuration/interfaces/openvpn.html |
| VyOS 1.5 OpenVPN | https://docs.vyos.io/en/1.5/configuration/interfaces/openvpn.html |
| VyOS 1.4 IKEv2 remote access | https://docs.vyos.io/en/1.4/configuration/vpn/ipsec/remoteaccess_ipsec.html |
| VyOS 1.5 IKEv2 remote access | https://docs.vyos.io/en/1.5/configuration/vpn/ipsec/remoteaccess_ipsec.html |
| VyOS 1.5 site-to-site IPsec | https://docs.vyos.io/en/1.5/configuration/vpn/ipsec/site2site_ipsec.html |
| VyOS IPsec status examples | https://docs.vyos.io/en/1.4/configexamples/ipsec-cisco-policy-based.html |
| VyOS NAT exclusions | https://docs.vyos.io/en/1.5/configexamples/policy-based-ipsec-and-firewall.html |
| pfSense site-to-site IPsec | https://docs.netgate.com/pfsense/en/latest/recipes/ipsec-s2s-psk.html |
| pfSense OpenVPN site-to-site TLS | https://docs.netgate.com/pfsense/en/latest/recipes/openvpn-s2s-tls.html |
| pfSense OpenVPN remote access | https://docs.netgate.com/pfsense/en/latest/recipes/openvpn-ra.html |
| pfSense OpenVPN backend selection | https://docs.netgate.com/pfsense/en/latest/vpn/openvpn/configure-server-backend.html |
| pfSense OpenVPN tunnel routing | https://docs.netgate.com/pfsense/en/latest/vpn/openvpn/configure-server-tunnel.html |
| pfSense LDAP and AD fields | https://docs.netgate.com/pfsense/en/latest/usermanager/ldap.html |
| pfSense WireGuard remote access | https://docs.netgate.com/pfsense/en/latest/recipes/wireguard-ra.html |
| pfSense capture interpretation | https://docs.netgate.com/pfsense/en/latest/diagnostics/packetcapture/index.html |
| WireGuard provider key generation | https://www.wireguard.com/quickstart/ |

No source here establishes a supported VyOS 1.4/1.5 PSK + IKEv1/XAUTH mobile recipe. This is a documented uncertainty, not a claim that strongSwan itself lacks the protocol. The generator rejects that request rather than silently substituting IKEv2 or editing generated daemon files. Rolling builds are not inferred compatible from a version-family resemblance.
