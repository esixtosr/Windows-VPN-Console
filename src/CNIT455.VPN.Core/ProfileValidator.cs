using System.Net;
namespace CNIT455.VPN.Core;
public static class ProfileValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(VpnProfile p)
    {
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(p.Name) || p.Name.Length>100 || p.Name.Any(char.IsControl)) issues.Add(new("Name","Use a name of 1–100 printable characters."));
        if (!Enum.IsDefined(p.Protocol) || !Enum.IsDefined(p.Authentication) || !Enum.IsDefined(p.TunnelMode) || !Enum.IsDefined(p.LabPolicy) || !Enum.IsDefined(p.AuthBackend)) issues.Add(new("Profile","Unknown protocol, authentication, or policy enum value."));
        if (string.IsNullOrWhiteSpace(p.ProviderId)) issues.Add(new("Provider","Select a VPN engine."));
        if (!IsHost(p.Gateway)) issues.Add(new("Gateway","Enter an IPv4 address or DNS hostname, without a scheme, slash, port, or whitespace."));
        if (p.Port is < 0 or > 65535 || (p.Protocol is VpnProtocol.WireGuard or VpnProtocol.OpenVpn && p.Port==0)) issues.Add(new("Port","A UDP/TCP endpoint port must be 1–65535."));
        if (p.ListenPort is <0 or >65535) issues.Add(new("Listen port","Use 0 (automatic) or 1–65535."));
        var networks = new HashSet<string>();
        foreach (var s in p.PermittedNetworks)
        {
            if (!CidrNetwork.TryParse(s,out var n)) { issues.Add(new("Routes",$"Invalid IPv4 CIDR: {s}")); continue; }
            if (!networks.Add(n.ToString())) issues.Add(new("Routes",$"Duplicate route: {n}"));
            if (p.IsLab && n.ToString()=="192.168.5.0/24") issues.Add(new("Routes","192.168.5.0/24 is reserved by the lab; confirm and correct the network.",false));
        }
        foreach(var s in p.ForbiddenNetworks) if(!CidrNetwork.TryParse(s,out _)) issues.Add(new("Forbidden networks",$"Invalid IPv4 CIDR: {s}"));
        if (p.TunnelMode==TunnelMode.Split && p.PermittedNetworks.Count==0) issues.Add(new("Routes","Split tunneling needs at least one permitted route."));
        if (p.TunnelMode==TunnelMode.Split && networks.Contains("0.0.0.0/0")) issues.Add(new("Routes","A default route conflicts with split-tunnel policy."));
        if (!string.IsNullOrEmpty(p.TunnelAddress) && !CidrNetwork.TryParse(p.TunnelAddress,out _)) issues.Add(new("Tunnel address","Enter an IPv4 address/prefix."));
        if (p.Protocol==VpnProtocol.WireGuard && string.IsNullOrWhiteSpace(p.ImportedConfigPath) && !IsWireGuardKey(p.PeerPublicKey)) issues.Add(new("Peer key","WireGuard peer public key must decode to exactly 32 bytes."));
        if (!string.IsNullOrEmpty(p.PublicKey) && !IsWireGuardKey(p.PublicKey)) issues.Add(new("Public key","WireGuard public key must decode to exactly 32 bytes."));
        foreach(var dns in p.Dns.Split(new[]{',',';',' '},StringSplitOptions.RemoveEmptyEntries)) if(!IPAddress.TryParse(dns,out _)) issues.Add(new("DNS",$"Invalid DNS address: {dns}"));
        if (p.Authentication is AuthenticationMode.Certificate or AuthenticationMode.CertificateAndUsername && p.Protocol is VpnProtocol.Ikev2 or VpnProtocol.Sstp && string.IsNullOrWhiteSpace(p.CertificateThumbprint)) issues.Add(new("Certificate","Select a valid Windows certificate."));
        if (p.Authentication is AuthenticationMode.PskAndUsername or AuthenticationMode.UsernamePassword && p.Protocol is VpnProtocol.L2tpIpsec or VpnProtocol.Ikev2 or VpnProtocol.Sstp && string.IsNullOrWhiteSpace(p.Username)) issues.Add(new("Username","Enter the account name used for authentication."));
        if (p.IsLab && p.GroupNumber is <0 or >255) issues.Add(new("Group","Group must be 0–255."));
        return issues;
    }
    public static bool IsHost(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length>253 || text.Any(c=> !(char.IsAsciiLetterOrDigit(c)||c is '.' or '-'))) return false;
        if (text.All(c=>char.IsAsciiDigit(c)||c=='.')) return CidrNetwork.TryAddress(text,out _);
        return text.Split('.').All(x=>x.Length is >0 and <=63 && char.IsAsciiLetterOrDigit(x[0]) && char.IsAsciiLetterOrDigit(x[^1]));
    }
    public static bool IsWireGuardKey(string? key) { try { return Convert.FromBase64String(key??"").Length==32; } catch(FormatException) {return false;} }
    public static IReadOnlyList<ValidationIssue> ValidatePool(string pool,IEnumerable<string> localNetworks)
    {
        if(!CidrNetwork.TryParse(pool,out var cidr)) return [new("VPN pool","Enter a valid IPv4 CIDR pool.")];
        return localNetworks.Where(s=>CidrNetwork.TryParse(s,out var other)&&cidr.Overlaps(other)).Select(s=>new ValidationIssue("VPN pool",$"VPN pool overlaps {s}. Use a separate subnet.")).ToList();
    }
}
