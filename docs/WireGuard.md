# WireGuard

Install WireGuard for Windows separately. Key generation uses the official provider tools (`wg genkey`, with the private key fed privately to `wg pubkey`), rather than custom Curve25519 code. The additional PSK is 32 cryptographically random bytes encoded as Base64; a human-readable 32-character IPsec PSK is not interchangeable. See [WireGuard quick start](https://www.wireguard.com/quickstart/).

Normal configuration previews include `<PRIVATE_KEY>` and `<PRESHARED_KEY>`. Operational export containing secrets must be an explicit action and the resulting file must be protected. Ordinary profile JSON and diagnostic exports exclude secrets. The public key may be shared; the private key must remain hidden until explicitly revealed.

## Client access

The lab client reaches Remote pfSense and uses full tunneling. Enter a unique tunnel address, server public key, actual public endpoint/port and VPN DNS. The app calculates `0.0.0.0/0, ::/0` for full mode so IPv6 is not silently omitted. The server must route IPv6 or enforce an intentional blocking policy. Split mode instead needs explicit permitted networks.

On pfSense, the client's peer AllowedIPs must contain that client's tunnel host address. They are not the client's full-default AllowedIPs. Permit authorized tunnel traffic and add Internet NAT for the pool. Access to HQ through IPsec also needs pool selectors, forwarding permissions and a return route; keep those private flows un-NATed. See [Netgate remote-access example](https://docs.netgate.com/pfsense/en/latest/recipes/wireguard-ra.html).

## Site-to-site

The two VyOS peers join HQ and Remote networks using UDP, separate keypairs and the same additional PSK. Enter distinct public endpoints. The helper emits routes and AllowedIPs for the remote LAN/tunnel host, with narrow source-NAT exclusion. Reverse endpoint, address, network and key roles on the other router. No blanket NAT belongs on this path.

If Extended mobile-IPsec policy is required, HQ must also accept and route the mobile client's actual pool through WireGuard. Do not guess the pool from the LAN address. See the reviewed [VyOS 1.4](https://docs.vyos.io/en/1.4/configuration/interfaces/wireguard.html) and [1.5 interface references](https://docs.vyos.io/en/1.5/configuration/interfaces/wireguard.html).

Use `show interfaces wireguard wg0 summary` (substitute your interface) to inspect handshake and byte counters. A handshake alone does not prove LAN routing, DNS, full-tunnel egress or Internet NAT. Record those tests separately.
