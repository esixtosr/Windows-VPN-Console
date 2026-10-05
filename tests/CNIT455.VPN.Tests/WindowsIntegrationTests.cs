using System.Security.Cryptography;
using System.Text.Json;
using CNIT455.VPN.Core;
using CNIT455.VPN.Providers;
using Xunit;
namespace CNIT455.VPN.Tests;
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute() { if(!OperatingSystem.IsWindows())Skip="Windows API integration requires a Windows runner."; }
}
public sealed class WindowsIntegrationTests
{
    [WindowsFact]public async Task NativeL2tpProvisioningInstallsAndRemovesOnlyOwnedProfile()
    {
        var profile=new VpnProfile{Gateway="192.0.2.1",Username="cnit455-ci",Protocol=VpnProtocol.L2tpIpsec,PermittedNetworks=["198.51.100.0/24"]};
        var name=WindowsNativeVpnProvider.ConnectionName(profile);
        var data=new {Name=name,Gateway=profile.Gateway,Tunnel="L2tp",Method="MSChapv2",Split=true,Routes=profile.PermittedNetworks,Psk=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))};
        try
        {
            var result=await ProviderEnvironment.PowerShellAsync(WindowsNativeVpnProvider.ProvisionScript,data,CancellationToken.None);
            Assert.True(result.ExitCode==0,"Native VPN provisioning returned nonzero exit status; no secret output is included.");
            var query=await ProviderEnvironment.PowerShellAsync("$v=Get-VpnConnection -Name $d.Name -ErrorAction Stop; @{Name=$v.Name;Tunnel=[string]$v.TunnelType;Split=$v.SplitTunneling;State=[string]$v.ConnectionStatus;Routes=@($v.Routes | ForEach-Object DestinationPrefix)} | ConvertTo-Json -Compress",new{Name=name},CancellationToken.None);
            Assert.Equal(0,query.ExitCode);using var json=JsonDocument.Parse(query.StandardOutput);var v=json.RootElement;
            Assert.Equal(name,v.GetProperty("Name").GetString());Assert.Equal("L2tp",v.GetProperty("Tunnel").GetString());Assert.True(v.GetProperty("Split").GetBoolean());Assert.Equal("Disconnected",v.GetProperty("State").GetString());Assert.Contains(v.GetProperty("Routes").EnumerateArray(),x=>x.GetString()=="198.51.100.0/24");
        }
        finally
        {
            var removed=await ProviderEnvironment.PowerShellAsync("Get-VpnConnection | Where-Object Name -eq $d.Name | ForEach-Object {Remove-VpnConnection -Name $_.Name -Force -ErrorAction Stop}; if(Get-VpnConnection | Where-Object Name -eq $d.Name){throw 'App-owned CI profile was not removed.'}",new{Name=name},CancellationToken.None);
            Assert.Equal(0,removed.ExitCode);
        }
    }
    [WindowsFact]public async Task CredentialManagerRoundTripAndRemoval()
    {
        var id=Guid.NewGuid();var store=new WindowsSecretStore();var value=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        try{await store.SaveAsync(id,new VpnSecrets{Password=value,Psk=value,PrivateKey=value,RadiusSecret=value});var actual=await store.LoadAsync(id);Assert.NotNull(actual);Assert.Equal(value,actual.Password);Assert.Equal(value,actual.Psk);Assert.Equal(value,actual.PrivateKey);Assert.Equal(value,actual.RadiusSecret);}
        finally{await store.DeleteAsync(id);}
        Assert.Null(await store.LoadAsync(id));
    }
}
