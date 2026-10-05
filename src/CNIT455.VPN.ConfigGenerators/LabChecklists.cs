using CNIT455.VPN.Core;

namespace CNIT455.VPN.ConfigGenerators;

public sealed record LabVpnCheckoff(string Id, string Name, string Summary, IReadOnlyList<CheckoffItem> Items);

public static class CheckoffCatalog
{
    public static IReadOnlyList<LabVpnCheckoff> Create(LabTopology t, LabPolicy policy = LabPolicy.Strict)
    {
        return
        [
            Entry("s2s-ipsec", "IPsec Site-to-Site", $"pfSense HQ {t.PfSenseHq} ↔ pfSense Remote {t.PfSenseRemote}",
                "Both IKE and child/IPsec SAs are established with increasing packet counters",
                "Matching PSK and negotiated proposals confirmed without copying the PSK",
                $"Known hosts in {t.PfSenseHq} and {t.PfSenseRemote} can communicate in both directions",
                "LAN-side capture confirms native source/destination addresses (no cross-VPN NAT)",
                "Both firewall policies and Phase 2 selectors cover the permitted LANs and return traffic",
                "Correlated public-side capture and SA counters show protected test flow; no plaintext test payload",
                "NAT/DNAT rules do not match traffic crossing this VPN",
                "Evidence bundle saved with sanitized status, tests, and capture references"),
            Entry("s2s-wireguard", "WireGuard Site-to-Site", $"VyOS HQ {t.VyosHq} ↔ VyOS Remote {t.VyosRemote}",
                "Recent handshake and increasing RX/TX counters observed on both VyOS peers",
                "Distinct keypairs and matching additional PSK configured (keys not attached)",
                "UDP endpoint addresses and ports match the two actual routers",
                $"AllowedIPs and static routes cover {t.VyosHq} and {t.VyosRemote} in opposite directions",
                "Known hosts communicate bidirectionally using native source/destination addresses",
                "NAT exclusions and firewall forwarding verified on both peers",
                "Public-side capture correlates UDP encrypted packets with test traffic; no inner plaintext",
                "Evidence bundle saved with sanitized status, tests, and capture references"),
            Entry("s2s-openvpn", "OpenVPN Site-to-Site", $"pfSense DMZ {t.PfSenseDmz} ↔ VyOS DMZ {t.VyosDmz}",
                "TLS session established over UDP and transfer counters increase",
                "pfSense CA signs the server certificate and unique VyOS client certificate; validity checked",
                $"Server route/client-specific override and VyOS route cover {t.PfSenseDmz} ↔ {t.VyosDmz}",
                "Both DMZs communicate using native addresses, without public-facing NAT affecting the flow",
                "NAT exemptions and DMZ firewall forwarding verified on both sides",
                "Public-side capture and TLS/session evidence correlate protected test flow",
                "Certificate private keys are absent from evidence",
                "Evidence bundle saved with sanitized status, tests, and capture references"),
            Entry("client-ipsec", "IPsec Client", $"Remote VyOS; {policy} policy",
                "Installed provider and exact server IKE/XAUTH/Mode Config capabilities verified",
                "IPsec tunnel/SA established (external-client launch alone is not a connected result)",
                "PSK authentication confirmed from sanitized negotiation or server evidence",
                "Local user authentication passed before AD/RADIUS migration",
                "AD-backed user authentication succeeds and unauthorized user/group is denied",
                $"Known target in {t.VyosRemote} responds over the VPN",
                policy == LabPolicy.Extended ? $"Extended mode: {t.VyosHq} reachable via WireGuard with client-pool return route" :
                    policy == LabPolicy.Custom ? "Custom policy: all explicitly permitted routes and exclusions tested" : $"Strict mode: {t.VyosHq} remains inaccessible",
                "Actual route table and test flow confirm Internet stays on physical adapter, including IPv6",
                "Other forbidden lab networks remain inaccessible; firewall evidence distinguishes deny from host outage",
                "Public capture and IPsec SAs correlate encrypted test traffic",
                "Evidence bundle saved with sanitized status, tests, and capture references"),
            Entry("client-l2tp", "L2TP Client", $"HQ VyOS {t.VyosHq}; split tunnel",
                "Windows L2TP connection and underlying IPsec SAs established",
                "PSK configured and UDP/1701 accepted only when protected by IPsec",
                "Local login and pool address allocation passed",
                "AD/NPS RADIUS authentication passed after local test; unauthorized group denied",
                $"Target in {t.VyosHq} reachable with correct client-pool return path",
                "Actual target route uses VPN, Internet route uses physical adapter, and IPv6 path checked",
                "Public capture and IPsec counters correlate protected test traffic",
                "Evidence bundle saved with sanitized status, tests, and capture references"),
            Entry("client-openvpn", "OpenVPN Client", $"pfSense HQ {t.PfSenseHq}; full tunnel",
                "OpenVPN TLS/authentication completes over UDP",
                "pfSense-side AD LDAP authentication succeeds; configured group restriction tested",
                "Tunnel address, trusted server certificate and VPN DNS verified",
                $"Known host in {t.PfSenseHq} responds",
                $"When required, {t.PfSenseRemote} reachable via IPsec, including VPN pool Phase 2 selectors and return path",
                "Actual route table uses VPN defaults or paired /1 routes; endpoint bypass remains physical",
                "Internet test confirms VPN egress, DNS path and IPv6 leakage policy",
                "Internet outbound NAT covers the client pool; intersite private flows remain un-NATed",
                "Public capture and session counters correlate protected test traffic",
                "Evidence bundle saved with sanitized status, tests, and capture references"),
            Entry("client-wireguard", "WireGuard Client", $"pfSense Remote {t.PfSenseRemote}; full tunnel",
                "Latest handshake and increasing RX/TX counters observed",
                "Client/server public keys and required additional PSK configured; private key stays private",
                "Assigned address matches the pfSense peer AllowedIPs host route",
                $"Known host in {t.PfSenseRemote} responds",
                $"When required, {t.PfSenseHq} reachable via IPsec, including VPN pool selectors and return path",
                "AllowedIPs and actual IPv4/IPv6 routes send Internet through VPN; endpoint bypass remains physical",
                "VPN egress, DNS resolution and IPv6 leakage policy tested",
                "Internet outbound NAT covers the client pool; intersite private flows remain un-NATed",
                "Public capture and handshake/counters correlate encrypted traffic",
                "Evidence bundle saved with sanitized status, tests, and capture references")
        ];
    }

    private static LabVpnCheckoff Entry(string id, string name, string summary, params string[] items) =>
        new(id, name, summary, items.Select((text, index) => new CheckoffItem($"{id}-{index + 1}", text, ResultState.Unknown)).ToArray());
}

public static class PfSenseChecklist
{
    public static IReadOnlyList<string> Create(ServerConfigOptions o, LabTopology t) => o.Template switch
    {
        ServerTemplate.PfSenseIpsec =>
        [
            "VPN > IPsec > Tunnels: create matching Phase 1 on both appliances. Record their DISTINCT WAN addresses, IKE version, IDs, PSK reference, proposals and lifetime. Never put the PSK in this checklist.",
            $"Phase 2 on HQ: local {Pick(o.LocalNetwork, t.PfSenseHq)}, remote {Pick(o.RemoteNetwork, t.PfSenseRemote)}. Reverse on Remote. Match ESP/PFS settings. Leave NAT/BINAT translation unset.",
            "Firewall > Rules > IPsec and relevant LAN interfaces: permit required source/destination hosts/services in both directions. Check the effective rule order and deny unrelated networks.",
            "WAN rules: permit peer IKE UDP/500, NAT-T UDP/4500, and ESP as required. Check actual packet path and any upstream NAT. A sent UDP probe is not proof of an open port.",
            "Firewall > NAT > Outbound: ensure intersite source/destination pairs are not translated; use narrow manual/hybrid exceptions where applicable. Review 1:1 NAT and port-forward rules separately.",
            "Status > IPsec: establish traffic-triggered SAs and verify packet counters. Diagnostics > Routes: confirm ordinary routing toward the correct gateway/policy path, then test known hosts in both directions.",
            "For OpenVPN/WireGuard client-to-opposite-site access, add the actual client tunnel pool to the appropriate Phase 2 selectors on BOTH ends and permit it in firewall policy. Do not masquerade the pool merely to hide missing selectors.",
            "Diagnostics > Packet Capture: compare inner host traffic on LAN/IPsec with correlated WAN ESP or NAT-T traffic. Record native IPs inside and the absence of the recognizable test payload outside.",
            "Reference: https://docs.netgate.com/pfsense/en/latest/recipes/ipsec-s2s-psk.html"
        ],
        ServerTemplate.PfSenseOpenVpn =>
        [
            "System > Certificates: create/select the pfSense CA; issue a server certificate and unique VyOS/client certificate where mutual TLS is used. Confirm expiration and server identity. Keep private keys out of exports intended for diagnostics.",
            $"SITE-TO-SITE: VPN > OpenVPN > Servers: routed SSL/TLS server in client/server mode, UDP, chosen port {o.Port}, unused tunnel /24 (larger than /30), local {Pick(o.LocalNetwork, t.PfSenseDmz)}, remote {Pick(o.RemoteNetwork, t.VyosDmz)}. Do not enable redirect-gateway for this DMZ link.",
            $"VPN > OpenVPN > Client Specific Overrides: match the VyOS certificate common name and assign remote network {Pick(o.RemoteNetwork, t.VyosDmz)}. Server route and internal iroute are both required.",
            "Import the CA and unique client identity into VyOS PKI. Match negotiated data ciphers, TLS-auth/TLS-crypt mode/key and UDP settings against pfSense's exported client configuration. Use VyOS client mode for this routed TLS link.",
            "REMOTE ACCESS: create a separate UDP server/profile as needed. System > User Manager > Authentication Servers: configure AD LDAP, trusted TLS, search/bind identity, base DN and required group. Test authentication before choosing the server's backend. Bind password is entered on pfSense only.",
            $"Directory worksheet: LDAP host {Value(o.LdapServer)}; base DN {Value(o.LdapBaseDn)}; bind DN {Value(o.LdapBindDn)}; allowed group {Value(o.AllowedGroup)}; domain {Value(o.Domain)}.",
            $"Remote-access server: select LDAP backend, local network {t.PfSenseHq}, and redirect IPv4 gateway for full tunnel. Supply reachable VPN DNS. Carry IPv6 through the tunnel or enforce/document a no-IPv6 policy and test it.",
            $"When required add {t.PfSenseRemote} and the client pool to the IPsec routing/selectors/firewall policy on both sites. Enable outbound Internet NAT for the remote-access pool, while preserving native addresses for all intersite flows.",
            "Firewall > Rules: allow server UDP on WAN; permit authorized tunneled traffic on OpenVPN/assigned interface and DMZ/LAN. Firewall > NAT: exclude DMZ-to-DMZ traffic and constrain public port forwards to WAN destination/interface.",
            "Status > OpenVPN and logs: verify negotiated TLS/auth, assigned addresses, data counters, routes and bidirectional reachability. Capture on WAN plus inner interface; route changes alone do not prove encryption.",
            "References: https://docs.netgate.com/pfsense/en/latest/recipes/openvpn-s2s-tls.html and https://docs.netgate.com/pfsense/en/latest/vpn/openvpn/configure-server-backend.html"
        ],
        ServerTemplate.PfSenseWireGuard =>
        [
            "System > Package Manager: install the supported WireGuard package manually if absent. VPN > WireGuard > Tunnels: create an enabled tunnel with a unique private key, chosen UDP listen port and unused client subnet. Share only its public key.",
            $"VPN > WireGuard > Peers: add client PUBLIC key and the required matching WireGuard PSK. Peer AllowedIPs must identify that client's tunnel address (/32 or /128), not 0.0.0.0/0 on a multi-client server.",
            $"Client configuration: endpoint is this Remote pfSense's actual public address, UDP/{o.Port}; peer key is the server PUBLIC key. Full-tunnel client AllowedIPs: 0.0.0.0/0, ::/0. Use reachable VPN DNS and a unique tunnel address.",
            $"Firewall > Rules: permit UDP/{o.Port} to WAN and assign/authorize WireGuard traffic toward {t.PfSenseRemote}. Review package interface-assignment guidance; do not add a policy-routing gateway on an unrestricted rule accidentally.",
            "Firewall > NAT > Outbound: use the required hybrid/manual Internet masquerade for the client pool; keep intersite traffic exempt from translation. Verify IPv6 routes or an explicit IPv6 blocking policy.",
            $"For access to {t.PfSenseHq} over IPsec: add client-pool selectors on both appliances, allow forwarding and establish the return path. Preserve native tunnel client addressing.",
            "Status > WireGuard: confirm recent handshake, transfer counters and endpoint. Test target hosts, Internet egress, DNS and both IP families. Capture WAN and inner traffic to support the encrypted-flow checkoff.",
            "Reference: https://docs.netgate.com/pfsense/en/latest/recipes/wireguard-ra.html"
        ],
        _ => ["Select a pfSense checklist."]
    };
    private static string Pick(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static string Value(string value) => string.IsNullOrWhiteSpace(value) ? "<fill on server>" : value.Replace('\r', ' ').Replace('\n', ' ');
}

public static class PacketCaptureGuide
{
    public static string For(VpnProtocol protocol, int port = 0)
    {
        if (port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        var filter = protocol switch
        {
            VpnProtocol.WireGuard => $"udp port {(port > 0 ? port : 51820)}",
            VpnProtocol.OpenVpn => $"udp port {(port > 0 ? port : 1194)}",
            _ => "udp port 500 or udp port 4500 or ip proto 50"
        };
        return $"Capture the physical/public interface, not only the decrypted tunnel interface. " +
            $"Use capture filter: {filter}. Record timestamps while sending a recognizable, non-sensitive test flow to a known private host. " +
            "Compare a second LAN/tunnel capture for the original source/destination addresses and payload. " +
            "Correlate negotiated SA/TLS or handshake evidence and increasing counters with the WAN packets. " +
            "An outer UDP port or packet alone does not prove encryption. On WAN, search for the inner plaintext flow as well; " +
            "absence in a short/wrong-interface capture is inconclusive. Mark UNKNOWN when evidence is insufficient. " +
            "Keep raw captures locally and inspect them before explicitly sharing; they may contain unrelated users' traffic. " +
            "Attach a sanitized summary/capture reference to checkoff evidence; the app does not collect packet captures automatically." +
            "\n\nMANUAL WINDOWS 11 PKTMON CAPTURE (Administrator PowerShell):\n" +
            "# These commands change the pktmon capture session and write packet files. Read before running.\n" +
            "# Use an isolated lab VM. Run preflight first; continue only when pktmon is stopped and filter list is empty.\n" +
            "# Otherwise preserve the existing session/filters and coordinate with its owner.\n" +
            "pktmon status\npktmon filter list\npktmon list\n" +
            "# Replace <PUBLIC_ENDPOINT_IP>, <INNER_TEST_HOST_IP> and <PHYSICAL_COMPONENT_ID> from your lab.\n" +
            "# Filters are ORed: include both outer tunnel traffic and any accidental cleartext to the inner host.\n" +
            "pktmon filter add CNIT455-Outer -i <PUBLIC_ENDPOINT_IP>\n" +
            "pktmon filter add CNIT455-Inner -i <INNER_TEST_HOST_IP>\n" +
            "pktmon start --capture --comp <PHYSICAL_COMPONENT_ID> --pkt-size 0 --file-size 32 --file-name CNIT455-public.etl\n" +
            "# Perform the short known test flow. Stop only the session you started above.\n" +
            "pktmon stop\n" +
            "pktmon etl2pcap CNIT455-public.etl --out CNIT455-public.pcapng\n" +
            "# Only when the initial list was empty and no other filters were added: remove your temporary filters.\n" +
            "pktmon filter remove\n" +
            "# Physical component IDs are pktmon IDs, not Get-NetAdapter interface indexes.\n" +
            "# Repeat using the inner interface and a different filename, or capture inside on the router.\n" +
            "# Existing files may be overwritten: run in a new empty capture directory. Do not auto-attach packet files.\n" +
            "References: https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/pktmon-start\n" +
            "https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/pktmon-filter-add\n" +
            "https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/pktmon-etl2pcap\n";
    }
}
