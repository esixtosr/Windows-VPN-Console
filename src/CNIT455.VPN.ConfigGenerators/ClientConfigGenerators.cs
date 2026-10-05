using System.Net;
using System.Security.Cryptography;
using System.Text;
using CNIT455.VPN.Core;

namespace CNIT455.VPN.ConfigGenerators;

public static class PskGenerator
{
    public static string Generate(int length = 32, bool easyToType = false)
    {
        if (length is not (16 or 24 or 32 or 48 or 64)) throw new ArgumentOutOfRangeException(nameof(length), "Choose 16, 24, 32, 48 or 64 characters.");
        const string normal = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#%^&*-_=+";
        const string easy = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var alphabet = easyToType ? easy : normal;
        return string.Create(length, alphabet, static (span, chars) =>
        {
            for (var i = 0; i < span.Length; i++) span[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        });
    }

    // WireGuard PSKs are 32 random bytes, not a 32-character passphrase.
    public static string GenerateWireGuardPsk() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}

public static class WireGuardConfigGenerator
{
    public static string Generate(VpnProfile profile, VpnSecrets? secrets = null, bool includeSecrets = false)
    {
        var problems = Validate(profile, secrets, includeSecrets);
        if (problems.Count > 0) throw new ArgumentException(string.Join(" ", problems.Select(x => x.Message)));
        var addresses = Split(profile.TunnelAddress);
        var allowed = CalculateAllowedIps(profile.TunnelMode, profile.PermittedNetworks);
        var endpoint = IPAddress.TryParse(profile.Gateway, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"[{profile.Gateway}]" : profile.Gateway;
        var result = new StringBuilder();
        result.AppendLine(includeSecrets ? "# SECRET CONFIGURATION: protect this file; do not add it to evidence or Git." : "# TEMPLATE: replace secret placeholders before importing into WireGuard.");
        result.AppendLine("[Interface]");
        result.AppendLine($"PrivateKey = {(includeSecrets ? secrets!.PrivateKey : "<PRIVATE_KEY>")}");
        result.AppendLine($"Address = {string.Join(", ", addresses)}");
        if (!string.IsNullOrWhiteSpace(profile.Dns)) result.AppendLine($"DNS = {string.Join(", ", Split(profile.Dns))}");
        if (profile.ListenPort > 0) result.AppendLine($"ListenPort = {profile.ListenPort}");
        result.AppendLine().AppendLine("[Peer]");
        result.AppendLine($"PublicKey = {profile.PeerPublicKey}");
        result.AppendLine($"PresharedKey = {(includeSecrets ? secrets!.Psk : "<PRESHARED_KEY>")}");
        result.AppendLine($"Endpoint = {endpoint}:{(profile.Port == 0 ? 51820 : profile.Port)}");
        result.AppendLine($"AllowedIPs = {string.Join(", ", allowed)}");
        result.AppendLine("PersistentKeepalive = 25");
        return result.ToString();
    }

    public static IReadOnlyList<string> CalculateAllowedIps(TunnelMode mode, IEnumerable<string> permittedNetworks)
    {
        var networks = permittedNetworks.Select(GeneratorValidation.NormalizeNetwork).Distinct(StringComparer.Ordinal).ToArray();
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Select split or full tunnel mode.");
        if (mode == TunnelMode.Full) return ["0.0.0.0/0", "::/0"];
        if (networks.Length == 0) throw new ArgumentException("Split tunnel requires at least one permitted network.");
        if (GeneratorValidation.CoversAddressFamily(networks, 32) || GeneratorValidation.CoversAddressFamily(networks, 128))
            throw new ArgumentException("Split tunnel routes cannot cover an entire IPv4 or IPv6 address family. Select full tunnel instead.");
        return networks;
    }

    public static IReadOnlyList<ValidationIssue> Validate(VpnProfile profile, VpnSecrets? secrets = null, bool includeSecrets = false)
    {
        var issues = new List<ValidationIssue>();
        if (profile.Protocol != VpnProtocol.WireGuard) issues.Add(new("Protocol", "Select a WireGuard profile."));
        if (!GeneratorValidation.Host(profile.Gateway)) issues.Add(new("Gateway", "Enter an IP address or DNS hostname without a port."));
        if (profile.Port is < 0 or > 65535 || profile.ListenPort is < 0 or > 65535) issues.Add(new("Port", "Port must be 1-65535, or zero for the default."));
        if (!GeneratorValidation.Key(profile.PeerPublicKey)) issues.Add(new("PeerPublicKey", "Peer public key must decode to 32 bytes."));
        var addresses = Split(profile.TunnelAddress);
        if (addresses.Length == 0 || addresses.Any(x => !GeneratorValidation.Cidr(x))) issues.Add(new("TunnelAddress", "Enter one or more tunnel addresses with prefix lengths."));
        if (Split(profile.Dns).Any(x => !GeneratorValidation.Address(x))) issues.Add(new("Dns", "DNS servers must be IP addresses separated by commas."));
        try { CalculateAllowedIps(profile.TunnelMode, profile.PermittedNetworks); }
        catch (ArgumentException e) { issues.Add(new("AllowedIPs", e.Message)); }
        if (includeSecrets && !GeneratorValidation.Key(secrets?.PrivateKey)) issues.Add(new("PrivateKey", "Private key must decode to 32 bytes."));
        if (includeSecrets && !GeneratorValidation.Key(secrets?.Psk)) issues.Add(new("Psk", "WireGuard PSK must decode to 32 bytes."));
        return issues;
    }

    private static string[] Split(string value) => value.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}

internal static class GeneratorValidation
{
    public static bool Host(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) return false;
        if (Address(value)) return true;
        // Do not reinterpret malformed/abbreviated numeric addresses as DNS names.
        return value.Length <= 253 && value.Any(char.IsAsciiLetter) &&
            value.TrimEnd('.').Split('.').All(label => label.Length is >= 1 and <= 63 &&
                char.IsAsciiLetterOrDigit(label[0]) && char.IsAsciiLetterOrDigit(label[^1]) &&
                label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'));
    }
    public static bool Ipv4(string? value) => CidrNetwork.TryAddress(value, out _);
    public static bool Address(string? value) => Ipv4(value) || !string.IsNullOrEmpty(value) &&
        value.All(c => char.IsAsciiHexDigit(c) || c is ':' or '.') &&
        IPAddress.TryParse(value, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
    public static bool Cidr(string? value)
    {
        var parts = value?.Split('/');
        return parts is { Length: 2 } && Address(parts[0]) && parts[1].Length is >= 1 and <= 3 &&
            parts[1].All(char.IsAsciiDigit) && (parts[1].Length == 1 || parts[1][0] != '0') &&
            int.TryParse(parts[1], out var bits) && bits <= (Ipv4(parts[0]) ? 32 : 128);
    }
    public static string NormalizeNetwork(string value)
    {
        if (!Cidr(value)) throw new ArgumentException("Every permitted network must be a valid CIDR without whitespace or scope identifiers.");
        var parts = value.Split('/');
        var bytes = IPAddress.Parse(parts[0]).GetAddressBytes();
        var prefix = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
        for (var index = 0; index < bytes.Length; index++)
        {
            var retained = Math.Clamp(prefix - index * 8, 0, 8);
            bytes[index] &= (byte)(0xff << (8 - retained));
        }
        return $"{new IPAddress(bytes)}/{prefix}";
    }
    public static bool CoversAddressFamily(IEnumerable<string> networks, int bits)
    {
        var cursor = System.Numerics.BigInteger.Zero;
        var ranges = networks.Select(network => network.Split('/'))
            .Where(parts => (Ipv4(parts[0]) ? 32 : 128) == bits)
            .Select(parts =>
            {
                var first = new System.Numerics.BigInteger(IPAddress.Parse(parts[0]).GetAddressBytes(), isUnsigned: true, isBigEndian: true);
                return (First: first, End: first + (System.Numerics.BigInteger.One << (bits - int.Parse(parts[1]))));
            }).OrderBy(range => range.First);
        foreach (var range in ranges)
        {
            if (range.First > cursor) return false;
            cursor = System.Numerics.BigInteger.Max(cursor, range.End);
        }
        return cursor == System.Numerics.BigInteger.One << bits;
    }
    public static bool Key(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace)) return false;
        Span<byte> buffer = stackalloc byte[32];
        return Convert.TryFromBase64String(value, buffer, out var written) && written == 32;
    }
    public static bool Token(string value) => System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9_.@-]{0,63}$");
    public static string Q(string value)
    {
        // Reject syntax rather than trying to invent VyOS/bash escaping.
        if (value.Any(c => c is '\'' or '"' or '\r' or '\n' or '$' or '`' or '\\' || char.IsControl(c)))
            throw new ArgumentException("A configuration field contains shell syntax or control characters.");
        return $"'{value}'";
    }
}
