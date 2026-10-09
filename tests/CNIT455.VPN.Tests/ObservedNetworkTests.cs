using CNIT455.VPN.Core;
using CNIT455.VPN.Diagnostics;
using Xunit;

namespace CNIT455.VPN.Tests;

public sealed class ObservedNetworkTests
{
    private static DiagnosticSnapshot Sample() => new()
    {
        Profile = new() { ProviderId = "ncp", Protocol = VpnProtocol.Ikev2, TunnelMode = TunnelMode.Split, PermittedNetworks = ["198.51.100.0/24"] },
        Status = new(VpnState.Unknown, "External client status unavailable"),
        Routes = [new("0.0.0.0/0", "192.0.2.1", 3, "Ethernet", 10, 10), new("198.51.100.0/24", "0.0.0.0", 8, "Private adapter", 1, 1)],
        Adapters = [new(3, "Ethernet", "Physical adapter", "Up", ["192.0.2.44"], [], ["192.0.2.1"]), new(8, "Private adapter", "Virtual adapter", "Up", ["10.254.0.10"], [], [])]
    };

    [Fact] public void ExternalAddressAndInternetInterfaceAreObservedWithoutClaimingConnected()
    {
        var snapshot = Sample();
        Assert.Equal("10.254.0.10", ObservedNetwork.Address(ObservedNetwork.ProtectedAdapter(snapshot)));
        Assert.Equal("Ethernet", ObservedNetwork.InternetAdapter(snapshot)?.Name);
        Assert.Equal(VpnState.Unknown, snapshot.Status.State);
        Assert.All(snapshot.Routes, r => Assert.False(r.IsVpn));
    }
    [Fact] public void DefaultOnlyDoesNotIdentifyPhysicalNicAsPrivateAdapter()
    {
        var snapshot = Sample(); snapshot.Routes.RemoveAt(1);
        Assert.Null(ObservedNetwork.ProtectedAdapter(snapshot));
        Assert.Contains(NetworkEvidenceClassifier.Observe(snapshot), o => o.Category == "INTERNET ROUTE" && o.Result == ResultState.Unknown);
    }
    [Fact] public void DownAdapterDoesNotReuseItsStaleAddress()
    {
        var snapshot = Sample(); snapshot.Adapters[1] = snapshot.Adapters[1] with { Status = "Down" };
        Assert.Null(ObservedNetwork.ProtectedAdapter(snapshot));
        Assert.DoesNotContain(NetworkEvidenceClassifier.Observe(snapshot), o => o.Category == "ASSIGNED ADDRESS" && o.Result == ResultState.Pass);
    }
    [Fact] public void EquallyPreferredPrivateRoutesRemainAmbiguous()
    {
        var snapshot = Sample(); snapshot.Routes.Add(snapshot.Routes[1] with { InterfaceIndex = 3, InterfaceAlias = "Ethernet" });
        Assert.Null(ObservedNetwork.ProtectedAdapter(snapshot));
        Assert.Contains(NetworkEvidenceClassifier.Observe(snapshot), o => o.Category == "PROTECTED ROUTE" && o.Result == ResultState.Unknown);
    }
    [Fact] public void MoreSpecificRouteOnAnotherAdapterPreventsSingleAdapterClaim()
    {
        var snapshot = Sample(); snapshot.Routes.Add(new("198.51.100.128/25", "192.0.2.1", 3, "Ethernet", 1, 1));
        Assert.Null(ObservedNetwork.ProtectedAdapter(snapshot));
    }
    [Fact] public void MultiplePrivateNetworksMustAgree()
    {
        var snapshot = Sample(); snapshot.Profile.PermittedNetworks.Add("203.0.113.0/24");
        snapshot.Routes.Add(new("203.0.113.0/24", "192.0.2.1", 3, "Ethernet", 1, 1));
        Assert.Null(ObservedNetwork.ProtectedAdapter(snapshot));
    }
    [Theory] [InlineData("0.0.0.0")] [InlineData("169.254.1.1")] [InlineData("127.0.0.1")] [InlineData("fe80::1")]
    public void UnusableAddressesAreNotDisplayedAsAssigned(string address)
    {
        var snapshot = Sample(); snapshot.Adapters[1] = snapshot.Adapters[1] with { Addresses = [address] };
        Assert.Null(ObservedNetwork.Address(ObservedNetwork.ProtectedAdapter(snapshot)));
    }
    [Fact] public void NoTargetsOrFullDefaultCannotInferAnExternalAdapter()
    {
        var snapshot = Sample(); snapshot.Profile.PermittedNetworks.Clear(); Assert.Null(ObservedNetwork.ProtectedAdapter(snapshot));
        snapshot.Profile.PermittedNetworks.Add("0.0.0.0/0"); Assert.Null(ObservedNetwork.ProtectedAdapter(snapshot));
    }
    [Fact] public void HostRouteAtTopOfIpv4RangeDoesNotOverflow()
    {
        var snapshot = Sample(); snapshot.Profile.PermittedNetworks = ["255.255.255.255/32"];
        snapshot.Routes.Add(new("255.255.255.255/32", "0.0.0.0", 8, "Private adapter", 1, 1));
        Assert.Equal(8, ObservedNetwork.ProtectedAdapter(snapshot)?.Index);
    }
    [Fact] public void EqualCostInternetRoutesDoNotArbitrarilySelectAnAdapter()
    {
        var snapshot = Sample(); snapshot.Routes.Add(snapshot.Routes[0] with { InterfaceIndex = 8 });
        Assert.Null(ObservedNetwork.InternetAdapter(snapshot));
    }
}
