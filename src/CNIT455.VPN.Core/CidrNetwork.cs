using System.Net;
using System.Net.Sockets;
namespace CNIT455.VPN.Core;
public readonly record struct CidrNetwork(uint Network, int PrefixLength)
{
    public uint Mask => PrefixLength == 0 ? 0 : uint.MaxValue << (32 - PrefixLength);
    public uint Last => Network | ~Mask;
    public static bool TryParse(string? text, out CidrNetwork network)
    {
        network = default;
        var parts = text?.Trim().Split('/');
        if (parts is not { Length: 2 } || !TryAddress(parts[0], out var address) || !int.TryParse(parts[1], out var prefix) || prefix is < 0 or > 32) return false;
        uint mask = prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);
        network = new(address & mask, prefix); return true;
    }
    public static bool TryAddress(string? text, out uint value)
    {
        value = 0;
        var octets = text?.Split('.');
        if (octets is not { Length: 4 } || octets.Any(x => x.Length == 0 || x.Any(c => !char.IsAsciiDigit(c)) || (x.Length > 1 && x[0] == '0') || !byte.TryParse(x, out _))) return false;
        if (!IPAddress.TryParse(text, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork) return false;
        foreach (var b in ip.GetAddressBytes()) value = (value << 8) | b;
        return true;
    }
    public bool Contains(uint address) => (address & Mask) == Network;
    public bool Contains(string address) => TryAddress(address, out var value) && Contains(value);
    public bool Contains(CidrNetwork other) => other.PrefixLength >= PrefixLength && Contains(other.Network);
    public bool Overlaps(CidrNetwork other) => Network <= other.Last && other.Network <= Last;
    public static string FormatAddress(uint address) => $"{address >> 24}.{(address >> 16) & 255}.{(address >> 8) & 255}.{address & 255}";
    public override string ToString() => $"{FormatAddress(Network)}/{PrefixLength}";
}
