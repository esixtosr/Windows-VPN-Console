using CNIT455.VPN.Core;
using CNIT455.VPN.Diagnostics;
using CNIT455.VPN.Providers;
using Xunit;
namespace CNIT455.VPN.Tests;
public sealed class ProviderTests
{
    [Fact] public async Task RegistryHasUniqueIdsAndRequiredProviders()
    {
        var providers=ProviderRegistry.CreateDefault(new());
        Assert.Equal(new[]{"mock","native","ncp","openvpn","shrew","wireguard"},providers.Select(p=>p.Id).OrderBy(x=>x));
        foreach(var p in providers){var capabilities=await p.GetCapabilitiesAsync();Assert.Equal(p.Id,capabilities.Id);Assert.NotEmpty(capabilities.Protocols);var installation=await p.DetectInstallation();Assert.Equal(p.Id,installation.Id);}
    }
    [Fact] public void WindowsNativeRejectsLegacyIpsecInsteadOfGuessing()
    {
        var p=LabPresets.CreateClientProfiles(LabTopology.Create())[0];
        Assert.Contains(new WindowsNativeVpnProvider().ValidateProfile(p),i=>i.IsError&&i.Field=="Protocol");
    }
    [Theory][InlineData(MockFailure.NoGateway,VpnStage.Gateway)][InlineData(MockFailure.PskMismatch,VpnStage.IkePhase1)][InlineData(MockFailure.XauthFailure,VpnStage.Xauth)][InlineData(MockFailure.RadiusFailure,VpnStage.Radius)][InlineData(MockFailure.RouteFailure,VpnStage.Routing)][InlineData(MockFailure.DnsFailure,VpnStage.Dns)][InlineData(MockFailure.WireGuardHandshakeFailure,VpnStage.Tunnel)]
    public async Task MockStopsAtSelectedFailureWithoutClaimingLaterSuccess(MockFailure failure,VpnStage expected)
    {
        var p=new VpnProfile {MockFailure=failure};var logs=new List<VpnLogEvent>();var provider=new MockVpnProvider {StageDelay=TimeSpan.Zero};provider.LogReceived+=(_,log)=>logs.Add(log);
        var result=await provider.ConnectAsync(p,new());Assert.False(result.Success);Assert.Equal(VpnState.Failed,(await provider.GetStatusAsync(p)).State);Assert.Equal(expected,logs.Last().Stage);Assert.Equal(LogSeverity.Error,logs.Last().Severity);Assert.All(logs,l=>Assert.Contains("SIMULATION",l.Message));
    }
    [Fact] public async Task MockConnectAndDisconnectNeverClaimsRealNetworkChanges()
    {
        var provider=new MockVpnProvider{StageDelay=TimeSpan.Zero};var p=new VpnProfile();Assert.True((await provider.ConnectAsync(p,new())).Success);var status=await provider.GetStatusAsync(p);Assert.Equal(VpnState.Connected,status.State);Assert.Contains("no real tunnel",status.Message);Assert.True((await provider.DisconnectAsync(p)).Success);Assert.Equal(VpnState.Disconnected,(await provider.GetStatusAsync(p)).State);
    }
    [Fact] public async Task MockCancellationLeavesDisconnectedState()
    {
        var provider=new MockVpnProvider{StageDelay=TimeSpan.FromMilliseconds(200)};var p=new VpnProfile();using var cancellation=new CancellationTokenSource(20);await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>provider.ConnectAsync(p,new(),cancellation.Token));Assert.Equal(VpnState.Disconnected,(await provider.GetStatusAsync(p)).State);
    }
    [Fact] public void WireGuardImportRejectsExecutableHooks()
    {
        var key=Convert.ToBase64String(new byte[32]);var config=$"[Interface]\nPrivateKey={key}\nAddress=10.20.0.2/32\nPostUp=calc.exe\n[Peer]\nPublicKey={key}\nAllowedIPs=0.0.0.0/0\nEndpoint=vpn.example.edu:51820\n";
        Assert.Contains(WireGuardProvider.ValidateConfig(config),x=>x.IsError&&x.Message.Contains("directive",StringComparison.OrdinalIgnoreCase));
    }
    [Fact] public void WireGuardImportRejectsMalformedPrivateKey()
    {
        var config="[Interface]\nPrivateKey=bad\n[Peer]\nPublicKey=bad\n";Assert.Contains(WireGuardProvider.ValidateConfig(config),x=>x.IsError);
    }
}
