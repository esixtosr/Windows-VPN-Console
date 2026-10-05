using System.Net;
using System.Text;
using CNIT455.VPN.Core;

namespace CNIT455.VPN.ConfigGenerators;

/// <summary>Creates a deliberately incomplete, secret-free TLS client template for review.</summary>
public static class OpenVpnClientConfigGenerator
{
    public static string Generate(VpnProfile profile)
    {
        if (profile.Protocol != VpnProtocol.OpenVpn) throw new ArgumentException("Select an OpenVPN profile.");
        if (!GeneratorValidation.Host(profile.Gateway)) throw new ArgumentException("Enter a valid gateway IP address or DNS name without a port.");
        if (profile.Port is < 0 or > 65535) throw new ArgumentException("Port must be 1-65535, or zero for UDP/1194.");
        if (profile.Authentication is not (AuthenticationMode.Certificate or AuthenticationMode.CertificateAndUsername or AuthenticationMode.UsernamePassword))
            throw new ArgumentException("Choose certificate, username/password, or certificate + username authentication to generate a TLS template.");
        var networks = WireGuardConfigGenerator.CalculateAllowedIps(profile.TunnelMode, profile.PermittedNetworks);
        var dns = profile.Dns.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (dns.Any(x => !GeneratorValidation.Ipv4(x))) throw new ArgumentException("This OpenVPN Windows template accepts IPv4 DNS servers only; import a reviewed profile for IPv6 DNS.");
        var b = new StringBuilder()
            .AppendLine("# TEMPLATE ONLY: replace placeholders using the server administrator's exported profile.")
            .AppendLine("# Verify CA, expected server certificate CN, data ciphers and required tls-auth/tls-crypt.")
            .AppendLine("# Replace inline PEM placeholders with your exported material. The template contains no secret values.")
            .AppendLine("client").AppendLine("dev tun").AppendLine("proto udp")
            .AppendLine($"remote {profile.Gateway} {(profile.Port == 0 ? 1194 : profile.Port)}")
            .AppendLine("nobind").AppendLine("persist-key").AppendLine("persist-tun")
            .AppendLine("remote-cert-tls server")
            .AppendLine("verify-x509-name \"<EXPECTED_SERVER_CERTIFICATE_CN>\" name")
            .AppendLine("<ca>").AppendLine("<CA_CERTIFICATE_PEM>").AppendLine("</ca>")
            .AppendLine("tls-version-min 1.2")
            .AppendLine("auth-nocache");
        if (profile.Authentication is AuthenticationMode.Certificate or AuthenticationMode.CertificateAndUsername)
            b.AppendLine("<cert>").AppendLine("<CLIENT_CERTIFICATE_PEM>").AppendLine("</cert>").AppendLine("<key>").AppendLine("<CLIENT_PRIVATE_KEY_PEM>").AppendLine("</key>");
        if (profile.Authentication is AuthenticationMode.UsernamePassword or AuthenticationMode.CertificateAndUsername)
            b.AppendLine("auth-user-pass");
        if (profile.TunnelMode == TunnelMode.Full)
        {
            b.AppendLine("redirect-gateway def1 ipv6");
            b.AppendLine("# Full-tunnel intent only: server must provide IPv6 tunnel addressing/routing or an explicit blocking policy.");
            b.AppendLine("# Validate effective IPv4 AND IPv6 routes, DNS and Internet egress; this file is not proof of protection.");
        }
        else
        {
            b.AppendLine("route-nopull");
            b.AppendLine("# route-nopull also suppresses pushed DNS; explicit DNS below must be reachable over permitted routes.");
            foreach (var network in networks)
            {
                if (CidrNetwork.TryParse(network, out var ipv4))
                    b.AppendLine($"route {CidrNetwork.FormatAddress(ipv4.Network)} {CidrNetwork.FormatAddress(ipv4.Mask)}");
                else b.AppendLine($"route-ipv6 {network}");
            }
        }
        foreach (var server in dns) b.AppendLine($"dhcp-option DNS {server}");
        b.AppendLine("verb 3");
        return b.ToString();
    }
}
