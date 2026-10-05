using CNIT455.VPN.ConfigGenerators;
using CNIT455.VPN.Core;
using Xunit;

namespace CNIT455.VPN.Tests;

public sealed class GeneratorTests
{
    private static readonly string Key = Convert.ToBase64String(Enumerable.Range(1, 32).Select(x => (byte)x).ToArray());
    private static VpnProfile WireGuardProfile() => new()
    {
        Protocol = VpnProtocol.WireGuard, Gateway = "vpn.example.test", PeerPublicKey = Key,
        TunnelAddress = "10.77.0.2/32", PermittedNetworks = ["192.168.6.0/24"], Dns = "10.77.0.1"
    };

    [Theory]
    [InlineData(16)][InlineData(24)][InlineData(32)][InlineData(48)][InlineData(64)]
    public void PskHasRequestedLengthAndEasyAlphabet(int length)
    {
        var value = PskGenerator.Generate(length, true);
        Assert.Equal(length, value.Length);
        Assert.All(value, c => Assert.Contains(c, "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789"));
        Assert.Equal(length, PskGenerator.Generate(length).Length);
    }

    [Fact] public void PskRejectsUnsupportedLengths() => Assert.Throws<ArgumentOutOfRangeException>(() => PskGenerator.Generate(17));
    [Fact] public void WireGuardPskIsThirtyTwoRandomBytes()
    {
        var values = Enumerable.Range(0, 20).Select(_ => PskGenerator.GenerateWireGuardPsk()).ToArray();
        Assert.All(values, value => Assert.Equal(32, Convert.FromBase64String(value).Length));
        Assert.Equal(values.Length, values.Distinct().Count());
    }

    [Fact] public void WireGuardPreviewDoesNotUseSuppliedSecrets()
    {
        var secrets = new VpnSecrets { PrivateKey = PskGenerator.GenerateWireGuardPsk(), Psk = PskGenerator.GenerateWireGuardPsk() };
        var preview = WireGuardConfigGenerator.Generate(WireGuardProfile(), secrets);
        Assert.Contains("<PRIVATE_KEY>", preview); Assert.Contains("<PRESHARED_KEY>", preview);
        Assert.DoesNotContain(secrets.PrivateKey, preview); Assert.DoesNotContain(secrets.Psk, preview);
        var exported = WireGuardConfigGenerator.Generate(WireGuardProfile(), secrets, true);
        Assert.Contains(secrets.PrivateKey, exported); Assert.Contains(secrets.Psk, exported);
    }

    [Fact] public void WireGuardOperationalExportRequiresBothValidSecretKeys()
    {
        Assert.Throws<ArgumentException>(() => WireGuardConfigGenerator.Generate(WireGuardProfile(), null, true));
        Assert.Throws<ArgumentException>(() => WireGuardConfigGenerator.Generate(WireGuardProfile(), new() { PrivateKey = Key, Psk = "short" }, true));
    }

    [Fact] public void AllowedIpsNormalizeAndDeduplicateWithoutWidening()
    {
        Assert.Equal(new[] { "192.168.6.0/24", "2001:db8::/64" }, WireGuardConfigGenerator.CalculateAllowedIps(TunnelMode.Split,
            ["192.168.6.4/24", "192.168.6.0/24", "2001:0db8::abcd/64"]));
        Assert.Equal(new[] { "0.0.0.0/0", "::/0" }, WireGuardConfigGenerator.CalculateAllowedIps(TunnelMode.Full, []));
    }

    [Theory]
    [InlineData("0.0.0.0/0")][InlineData("10.0.0.1/0")][InlineData("::/0")][InlineData("2001:db8::/0")]
    [InlineData("192.168.1/24")][InlineData("127.1/24")][InlineData("192.168.1.0/+24")]
    [InlineData("192.168.1.0/24\nPostUp = bad")][InlineData("fe80::1%3/128")]
    public void SplitRejectsDefaultOrMalformedNetwork(string network) => Assert.Throws<ArgumentException>(() => WireGuardConfigGenerator.CalculateAllowedIps(TunnelMode.Split, [network]));

    [Fact] public void SplitRejectsEquivalentPairedDefaultRoutes()
    {
        Assert.Throws<ArgumentException>(() => WireGuardConfigGenerator.CalculateAllowedIps(TunnelMode.Split, ["0.0.0.0/1", "128.0.0.0/1"]));
        Assert.Throws<ArgumentException>(() => WireGuardConfigGenerator.CalculateAllowedIps(TunnelMode.Split, ["::/1", "8000::/1"]));
    }

    [Theory]
    [InlineData("vpn.test\nPostUp = whoami")][InlineData("vpn.test:51820")][InlineData("127.1")]
    [InlineData("fe80::1%4")][InlineData("bad;cmd.test")][InlineData("-bad.test")]
    public void WireGuardRejectsMalformedOrInjectableEndpoint(string gateway)
    {
        var profile = WireGuardProfile(); profile.Gateway = gateway;
        Assert.Throws<ArgumentException>(() => WireGuardConfigGenerator.Generate(profile));
    }

    [Fact] public void WireGuardBracketsIpv6Endpoint()
    {
        var profile = WireGuardProfile(); profile.Gateway = "2001:db8::1";
        Assert.Contains("Endpoint = [2001:db8::1]:51820", WireGuardConfigGenerator.Generate(profile));
    }

    [Theory][InlineData("1.4")][InlineData("1.5")]
    public void L2tpLocalAndRadiusUseDocumentedFields(string version)
    {
        var options = new ServerConfigOptions { Template = ServerTemplate.L2tp, VyosVersion = version };
        var local = ServerConfigGenerator.Generate(options, LabTopology.Create(42));
        Assert.True(local.Supported, local.Text);
        Assert.Contains("outside-address '44.104.42.4'", local.Text);
        Assert.Contains("range '10.255.20.2-10.255.20.254'", local.Text);
        Assert.Contains("protocols 'mschap-v2'", local.Text);
        Assert.DoesNotContain("set nat source", local.Text);
        options.Authentication = AuthBackend.Radius; options.RadiusServer = "192.168.2.10";
        options.RadiusSourceAddress = "192.168.2.1"; options.RadiusPort = 1812;
        var radius = ServerConfigGenerator.Generate(options, LabTopology.Create(42));
        Assert.True(radius.Supported, radius.Text);
        Assert.Contains("authentication mode 'radius'", radius.Text);
        Assert.Contains("server '192.168.2.10' key '<RADIUS_SECRET>'", radius.Text);
        Assert.DoesNotContain("local-users", radius.Text);
    }

    [Theory][InlineData("rolling")][InlineData("custom")][InlineData("1.6")][InlineData("1.5\n")]
    public void UnreviewedVersionNeverProducesCommands(string version)
    {
        var output = ServerConfigGenerator.Generate(new() { Template = ServerTemplate.L2tp, VyosVersion = version }, LabTopology.Create());
        Assert.False(output.Supported); Assert.DoesNotContain("set vpn", output.Text);
    }

    [Fact] public void LegacyMobileIsExplicitlyUnsupported()
    {
        var output = ServerConfigGenerator.Generate(new() { Template = ServerTemplate.LegacyIpsecMobile }, LabTopology.Create());
        Assert.False(output.Supported); Assert.Contains("XAUTH", output.Text); Assert.DoesNotContain("set vpn", output.Text);
    }

    [Fact] public void ServerRejectsOverlappingPoolAndIpv6Inputs()
    {
        var options = new ServerConfigOptions { Template = ServerTemplate.L2tp, ClientPool = "192.168.2.0/24" };
        Assert.False(ServerConfigGenerator.Generate(options, LabTopology.Create()).Supported);
        options.ClientPool = "2001:db8::/64";
        Assert.False(ServerConfigGenerator.Generate(options, LabTopology.Create()).Supported);
    }

    [Fact] public void WireGuardNatExclusionsAreOnlySpecifiedNetworkPairs()
    {
        var options = new ServerConfigOptions { Template = ServerTemplate.WireGuardSiteToSite, PeerEndpoint = "44.104.42.8", Routes = ["10.99.0.0/24"] };
        var output = ServerConfigGenerator.Generate(options, LabTopology.Create(42));
        Assert.True(output.Supported, output.Text);
        Assert.Contains("allowed-ips '192.168.6.0/24'", output.Text);
        Assert.Contains("rule 90 source address '192.168.2.0/24'", output.Text);
        Assert.Contains("rule 90 destination address '192.168.6.0/24'", output.Text);
        Assert.Contains("rule 91 destination address '10.99.0.0/24'", output.Text);
        Assert.DoesNotContain("0.0.0.0/0", output.Text); Assert.DoesNotContain("masquerade'", output.Text);
        options.RemoteNetwork = "0.0.0.0/0";
        Assert.False(ServerConfigGenerator.Generate(options, LabTopology.Create(42)).Supported);
    }

    [Fact] public void ServerRejectsInjectedIdentifiersAndInvalidExternalPrefix()
    {
        var options = new ServerConfigOptions { Template = ServerTemplate.L2tp, ExternalInterface = "eth0\nset system host-name attacker" };
        Assert.False(ServerConfigGenerator.Generate(options, LabTopology.Create()).Supported);
        options.ExternalInterface = "eth0"; options.ExternalAddress = "44.104.33.4/999";
        Assert.False(ServerConfigGenerator.Generate(options, LabTopology.Create()).Supported);
    }

    [Fact] public void OpenVpnTemplateKeepsVerificationAndNoCredentialValues()
    {
        var profile = new VpnProfile { Protocol = VpnProtocol.OpenVpn, Gateway = "vpn.test", Authentication = AuthenticationMode.CertificateAndUsername,
            TunnelMode = TunnelMode.Full, Username = "secret-identity", Notes = "password=do-not-copy" };
        var text = OpenVpnClientConfigGenerator.Generate(profile);
        Assert.Contains("remote-cert-tls server", text); Assert.Contains("verify-x509-name", text);
        Assert.Contains("auth-nocache", text); Assert.Contains("auth-user-pass\n", text.Replace("\r\n", "\n"));
        Assert.Contains("redirect-gateway def1 ipv6", text);
        Assert.DoesNotContain("secret-identity", text); Assert.DoesNotContain("do-not-copy", text);
        Assert.DoesNotContain("script-security", text);
    }

    [Fact] public void OpenVpnSplitTemplateIgnoresPushedRoutesAndUsesExplicitNetmasks()
    {
        var profile = new VpnProfile { Protocol = VpnProtocol.OpenVpn, Gateway = "vpn.test", Authentication = AuthenticationMode.UsernamePassword,
            TunnelMode = TunnelMode.Split, PermittedNetworks = ["192.168.6.9/24"] };
        var text = OpenVpnClientConfigGenerator.Generate(profile);
        Assert.Contains("route-nopull", text); Assert.Contains("route 192.168.6.0 255.255.255.0", text);
        Assert.DoesNotContain("redirect-gateway", text); Assert.DoesNotContain("key \"", text);
        profile.Gateway = "vpn.test\nup malicious";
        Assert.Throws<ArgumentException>(() => OpenVpnClientConfigGenerator.Generate(profile));
    }

    [Fact] public void CatalogHasSevenDistinctCheckoffsUnknownUntilMeasured()
    {
        var catalog = CheckoffCatalog.Create(LabTopology.Create(42));
        Assert.Equal(7, catalog.Count); Assert.Equal(7, catalog.Select(x => x.Id).Distinct().Count());
        Assert.All(catalog.SelectMany(x => x.Items), item => Assert.Equal(ResultState.Unknown, item.Result));
        Assert.Contains(catalog, x => x.Summary.Contains("172.18.42.0/24"));
    }

    [Fact] public void CaptureInstructionsRequireManualSessionAndCorrectComponent()
    {
        var text = PacketCaptureGuide.For(VpnProtocol.WireGuard, 51821);
        Assert.Contains("udp port 51821", text); Assert.Contains("pktmon status", text);
        Assert.Contains("--comp <PHYSICAL_COMPONENT_ID>", text); Assert.Contains("pktmon etl2pcap", text);
        Assert.Contains("empty", text); Assert.Contains("UNKNOWN", text);
    }
}
