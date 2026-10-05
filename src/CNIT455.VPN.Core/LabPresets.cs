namespace CNIT455.VPN.Core;
public sealed class LabTopology
{
    public int GroupNumber { get; set; } = 33;
    public string PublicNetwork { get; set; } = "";
    public string VyosExternal { get; set; } = "";
    public string PfSenseExternal { get; set; } = "";
    public string PfSenseNat { get; set; } = "";
    public string VyosHq { get; set; } = "192.168.2.0/24";
    public string PfSenseHq { get; set; } = "192.168.3.0/24";
    public string PfSenseRemote { get; set; } = "192.168.4.0/24";
    public string VyosRemote { get; set; } = "192.168.6.0/24";
    public string PfSenseDmz { get; set; } = "192.168.1.0/24";
    public string VyosDmz { get; set; } = "";
    public static LabTopology Create(int groupNumber = 33)
    {
        if (groupNumber is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(groupNumber), "Group must be an IPv4 octet, 0–255.");
        return new() { GroupNumber=groupNumber, PublicNetwork=$"44.104.{groupNumber}.0/24", VyosExternal=$"44.104.{groupNumber}.4", PfSenseExternal=$"44.104.{groupNumber}.5", PfSenseNat=$"44.104.{groupNumber}.6", VyosDmz=$"172.18.{groupNumber}.0/24" };
    }
}
public static class LabPresets
{
    public const string PolicyNote = "The lab wording differs: Strict permits VyOS Remote only; Extended also permits VyOS HQ via WireGuard. Follow the instructor's checkoff expectation. Endpoints for original/new routers must be confirmed for your deployment.";
    public static IReadOnlyList<VpnProfile> CreateClientProfiles(LabTopology t) =>
    [
        new() { Name="Lab 2 · IPsec Mobile",Protocol=VpnProtocol.IpsecMobile,ProviderId="shrew",Gateway=t.VyosExternal,Authentication=AuthenticationMode.PskAndUsername,PermittedNetworks=[t.VyosRemote],ForbiddenNetworks=[t.VyosHq,t.PfSenseHq,t.PfSenseRemote,t.PfSenseDmz,t.VyosDmz],GroupNumber=t.GroupNumber,Notes=PolicyNote },
        new() { Name="Lab 2 · L2TP/IPsec",Protocol=VpnProtocol.L2tpIpsec,ProviderId="native",Gateway=t.VyosExternal,Authentication=AuthenticationMode.PskAndUsername,PermittedNetworks=[t.VyosHq],GroupNumber=t.GroupNumber },
        new() { Name="Lab 2 · OpenVPN",Protocol=VpnProtocol.OpenVpn,ProviderId="openvpn",Gateway=t.PfSenseExternal,Port=1194,Authentication=AuthenticationMode.ProviderDefault,TunnelMode=TunnelMode.Full,PermittedNetworks=[t.PfSenseHq,t.PfSenseRemote],GroupNumber=t.GroupNumber,Notes="pfSense LDAP authentication; pfSense Remote via IPsec when configured." },
        new() { Name="Lab 2 · WireGuard",Protocol=VpnProtocol.WireGuard,ProviderId="wireguard",Gateway=t.PfSenseNat,Port=51820,Authentication=AuthenticationMode.PreSharedKey,TunnelMode=TunnelMode.Full,PermittedNetworks=["0.0.0.0/0"],GroupNumber=t.GroupNumber,Notes="Confirm remote pfSense endpoint. pfSense Remote and HQ via IPsec when configured. IPv6 must be routed or disabled on the tunnel according to policy." }
    ];
    public static IReadOnlyList<AccessRule> AccessMatrix(VpnProfile p, LabTopology t)
    {
        var rules = new List<AccessRule>();
        foreach (var n in new[]{t.VyosRemote,t.VyosHq,t.PfSenseHq,t.PfSenseRemote,t.PfSenseDmz,t.VyosDmz}.Distinct())
        {
            var denied = p.ForbiddenNetworks.Any(x=> CidrNetwork.TryParse(x,out var c) && CidrNetwork.TryParse(n,out var d) && c.Contains(d));
            var allowed = p.PermittedNetworks.Any(x=> CidrNetwork.TryParse(x,out var c) && CidrNetwork.TryParse(n,out var d) && c.Contains(d));
            // A full default route describes transport, not authorization by the server.
            if (p.Protocol==VpnProtocol.WireGuard && p.IsLab) allowed = n==t.PfSenseHq || n==t.PfSenseRemote;
            rules.Add(new(n,denied?"DENY":allowed?"ALLOW":"NOT PERMITTED", allowed && ((p.Protocol==VpnProtocol.IpsecMobile && n==t.VyosHq)||(p.Protocol==VpnProtocol.OpenVpn && n==t.PfSenseRemote)||(p.Protocol==VpnProtocol.WireGuard && n==t.PfSenseHq))?"Via site-to-site; verify return route and firewall":"Expected policy; reachability is not yet measured"));
        }
        rules.Add(new("Internet",p.TunnelMode==TunnelMode.Split?"LOCAL / NOT VPN":"VPN","Validated from actual routes; policy alone is not proof"));
        return rules;
    }
    public static void ApplyPolicy(VpnProfile p, LabTopology t, LabPolicy policy)
    {
        p.LabPolicy=policy;
        if (p.Protocol!=VpnProtocol.IpsecMobile || policy==LabPolicy.Custom) return;
        p.PermittedNetworks = policy==LabPolicy.Strict?[t.VyosRemote]:[t.VyosRemote,t.VyosHq];
        p.ForbiddenNetworks = new[]{t.VyosHq,t.PfSenseHq,t.PfSenseRemote,t.PfSenseDmz,t.VyosDmz}.Except(p.PermittedNetworks).ToList();
    }
}
