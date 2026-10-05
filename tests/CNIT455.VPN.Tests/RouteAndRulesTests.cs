using CNIT455.VPN.Core;
using CNIT455.VPN.Diagnostics;
using Xunit;
namespace CNIT455.VPN.Tests;
public sealed class RouteAndRulesTests
{
    private static RouteEntry R(string prefix,int index,int metric=5,int interfaceMetric=10)=>new(prefix,index==10?"10.99.0.1":"192.0.2.1",index,index==10?"vpn":"ethernet",metric,interfaceMetric,index==10);
    private static VpnProfile P(TunnelMode mode=TunnelMode.Split)=>new(){TunnelMode=mode,PermittedNetworks=["192.168.6.0/24"]};
    [Fact]public void SplitPassRequiresTargetsOnTunnelAndLocalDefault(){var a=RouteAnalyzer.Analyze(P(),[R("0.0.0.0/0",2),R("192.168.6.0/24",10)]);Assert.Equal(ResultState.Pass,a.Result);}
    [Fact]public void SplitFailsWhenTargetMissing()=>Assert.Equal(ResultState.Fail,RouteAnalyzer.Analyze(P(),[R("0.0.0.0/0",2),R("10.1.0.0/24",10)]).Result);
    [Fact]public void MoreSpecificLeakCannotHideBehindVpnNetwork()=>Assert.Equal(ResultState.Fail,RouteAnalyzer.Analyze(P(),[R("0.0.0.0/0",2),R("192.168.6.0/24",10),R("192.168.6.128/25",2)]).Result);
    [Fact]public void FullPassesWithOpenVpnDef1Routes()=>Assert.Equal(ResultState.Pass,RouteAnalyzer.Analyze(P(TunnelMode.Full),[R("0.0.0.0/0",2),R("0.0.0.0/1",10),R("128.0.0.0/1",10)]).Result);
    [Fact]public void HalfOfDefaultIsNotFullTunnel()=>Assert.Equal(ResultState.Fail,RouteAnalyzer.Analyze(P(TunnelMode.Full),[R("0.0.0.0/0",2),R("0.0.0.0/1",10),R("192.168.6.0/24",10)]).Result);
    [Fact]public void CombinedMetricDeterminesPreferredDefault()=>Assert.Equal(ResultState.Fail,RouteAnalyzer.Analyze(P(TunnelMode.Full),[R("0.0.0.0/0",2,20,1),R("0.0.0.0/0",10,1,30)]).Result);
    [Fact]public void EqualCostRouteToDifferentInterfacesIsUnknown()=>Assert.Equal(ResultState.Unknown,RouteAnalyzer.Analyze(P(),[R("0.0.0.0/0",2),R("192.168.6.0/24",10),R("192.168.6.0/24",2)]).Result);
    [Fact]public void UnidentifiedInterfaceNeverPasses()=>Assert.Equal(ResultState.Unknown,RouteAnalyzer.Analyze(P(),[R("0.0.0.0/0",2)]).Result);
    [Fact]public void LongestPrefixWinsBeforeMetric()=>Assert.Equal(10,RouteAnalyzer.BestRoute([R("0.0.0.0/0",2,1),R("192.168.6.0/24",10,999)],"192.168.6.2")!.InterfaceIndex);
    [Theory][InlineData(VpnStage.Radius,"AUTHENTICATION")][InlineData(VpnStage.Xauth,"AUTHENTICATION")][InlineData(VpnStage.Dns,"DNS")][InlineData(VpnStage.Gateway,"TRANSPORT")][InlineData(VpnStage.Routing,"ROUTING")]
    public void RulesKeepFailureDomainsSeparate(VpnStage stage,string category){var d=new DiagnosticSnapshot{Logs=[new(DateTimeOffset.Now,LogSeverity.Error,"test",stage,"failure")]};Assert.Contains(TroubleshootingAnalyzer.Analyze(d),f=>f.Category==category&&f.Result==ResultState.Fail);}
    [Fact]public void WireGuardHandshakeFailureIsTransportEvidence()
    {
        var snapshot=new DiagnosticSnapshot{Profile=new VpnProfile{Protocol=VpnProtocol.WireGuard},Logs=[new(DateTimeOffset.Now,LogSeverity.Error,"wireguard",VpnStage.Tunnel,"Tunnel handshake failed.")]};
        Assert.Contains(TroubleshootingAnalyzer.Analyze(snapshot),x=>x.Category=="TRANSPORT"&&x.Result==ResultState.Fail);
    }
    [Fact]public void MissingDataProducesUnknown(){Assert.All(TroubleshootingAnalyzer.Analyze(new()),f=>Assert.Equal(ResultState.Unknown,f.Result));}
}
