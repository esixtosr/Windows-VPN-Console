using CNIT455.VPN.Core;
using CNIT455.VPN.Providers;
using Xunit;
namespace CNIT455.VPN.Tests;
public sealed class ProviderSecurityTests
{
    [Theory][InlineData("cipher none")][InlineData("data-ciphers AES-256-GCM:none")][InlineData("data-ciphers-fallback none")]
    public void OpenVpnRejectsUnencryptedCipher(string directive)=>Assert.Contains(OpenVpnProvider.ValidateConfig("client\nremote vpn.test 1194\n<ca>\nCA\n</ca>\n"+directive),x=>x.Message.Contains("Unencrypted"));
    [Theory][InlineData("proto tcp-client\nauth-user-pass")][InlineData("proto udp\nremote vpn.test 443 tcp-client\nauth-user-pass")][InlineData("proto udp")]
    public void LabOpenVpnRequiresUdpAndUserAuthentication(string directives)
    {
        var path=Path.GetTempFileName();
        try{File.WriteAllText(path,"client\nremote vpn.test 1194\n<ca>\nCA\n</ca>\n"+directives);var p=new VpnProfile{Protocol=VpnProtocol.OpenVpn,Authentication=AuthenticationMode.ProviderDefault,Gateway="vpn.test",Port=1194,ImportedConfigPath=path,PermittedNetworks=["192.168.3.0/24"]};Assert.Contains(new OpenVpnProvider().ValidateProfile(p),x=>x.IsError&&(x.Field=="Authentication"||x.Field=="Transport"));p.IsLab=false;Assert.DoesNotContain(new OpenVpnProvider().ValidateProfile(p),x=>x.IsError&&(x.Field=="Authentication"||x.Field=="Transport"));}finally{File.Delete(path);}
    }
    [Fact] public void LabWireGuardRequiresAdditionalPsk()
    {
        var key=Convert.ToBase64String(new byte[32]);var config=$"[Interface]\nPrivateKey={key}\nAddress=10.1.0.2/32\n[Peer]\nPublicKey={key}\nAllowedIPs=192.168.4.0/24\n";
        Assert.Empty(WireGuardProvider.ValidateConfig(config));Assert.Contains(WireGuardProvider.ValidateConfig(config,true),x=>x.Message.Contains("pre-shared"));Assert.Empty(WireGuardProvider.ValidateConfig(config+$"PresharedKey={key}\n",true));
    }
}
