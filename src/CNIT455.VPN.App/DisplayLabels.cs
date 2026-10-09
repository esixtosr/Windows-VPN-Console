using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Data;
using CNIT455.VPN.Core;

namespace CNIT455.VPN.App;

// Presentation only: the enum values and stored profile format remain unchanged.
internal sealed class DisplayLabelConverter : IValueConverter
{
    public static DisplayLabelConverter Instance { get; } = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => DisplayLabels.For(value);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

internal static class DisplayLabels
{
    internal static string Version => typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion.Split('+')[0] ?? "Development build";
    internal static string For(object? value) => value switch
    {
        VpnProtocol.Ikev2 => "IKEv2 / IPsec",
        VpnProtocol.IpsecMobile => "IPsec mobile (external client)",
        VpnProtocol.L2tpIpsec => "L2TP / IPsec",
        VpnProtocol.Sstp => "SSTP",
        VpnProtocol.OpenVpn => "OpenVPN",
        VpnProtocol.WireGuard => "WireGuard",
        AuthenticationMode.ProviderDefault => "Use engine's sign-in",
        AuthenticationMode.PskAndUsername => "Shared key + username/password",
        AuthenticationMode.PreSharedKey => "Pre-shared key",
        AuthenticationMode.UsernamePassword => "Username and password",
        AuthenticationMode.CertificateAndUsername => "Certificate + username/password",
        AuthenticationMode.Certificate => "Certificate",
        TunnelMode.Split => "Split — private networks only",
        TunnelMode.Full => "Full — all traffic",
        AuthBackend.LocalTest => "Local test account",
        AuthBackend.Radius => "RADIUS",
        AuthBackend.Ldap => "LDAP",
        ResultState.Unknown => "Not verified",
        ResultState.Pass => "Pass",
        ResultState.Fail => "Needs attention",
        "Dependencies" => "VPN engines",
        "Server Config" => "Server tools",
        "Check-Off" => "Check-off",
        Enum e => Regex.Replace(e.ToString(), "(?<=[a-z])(?=[A-Z])", " "),
        _ => value?.ToString() ?? ""
    };

    internal static string NavigationGlyph(string page) => page switch
    {
        "Dashboard" => "\uE80F", "Connections" => "\uE968", "Diagnostics" => "\uE9D9",
        "Dependencies" => "\uE74C", "Server Config" => "\uE90F", "Settings" => "\uE713",
        "About" => "\uE946", "Lab 2" => "\uE7BE", "Check-Off" => "\uE73A", _ => "\uE8A5"
    };
}

internal sealed class NavigationGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => DisplayLabels.NavigationGlyph(value?.ToString() ?? "");
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
