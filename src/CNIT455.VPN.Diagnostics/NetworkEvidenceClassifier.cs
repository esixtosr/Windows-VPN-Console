using CNIT455.VPN.Core;

namespace CNIT455.VPN.Diagnostics;

public static class NetworkEvidenceClassifier
{
    public static IReadOnlyList<DiagnosticObservation> Observe(DiagnosticSnapshot snapshot)
    {
        var observations = new List<DiagnosticObservation>
        {
            ProviderObservation(snapshot)
        };

        observations.AddRange(ProtectedRouteObservations(snapshot));
        observations.Add(DefaultRouteObservation(snapshot));
        observations.AddRange(AssignedAddressObservations(snapshot));
        return observations;
    }

    private static DiagnosticObservation ProviderObservation(DiagnosticSnapshot snapshot) => snapshot.Status.State switch
    {
        VpnState.Connected => new("PROVIDER STATE", ResultState.Pass, "Provider reports CONNECTED.", snapshot.Status.Message, "Provider-reported"),
        VpnState.Failed => new("PROVIDER STATE", ResultState.Fail, "Provider reports FAILED.", snapshot.Status.Message, "Provider-reported"),
        VpnState.ExternalClient => new("PROVIDER STATE", ResultState.Unknown, "Provider handed off to an external client.", snapshot.Status.Message, "External client"),
        _ => new("PROVIDER STATE", ResultState.Unknown, "Provider tunnel state is not verified.", snapshot.Status.Message, "Provider-reported")
    };

    private static IEnumerable<DiagnosticObservation> ProtectedRouteObservations(DiagnosticSnapshot snapshot)
    {
        foreach (var target in snapshot.Profile.PermittedNetworks.Where(x => x != "0.0.0.0/0"))
        {
            if (!CidrNetwork.TryParse(target, out var network))
            {
                yield return new("PROTECTED ROUTE", ResultState.Unknown, $"Invalid target network {target}.", "Profile validation should be corrected first.", "Profile");
                continue;
            }

            var probe = CidrNetwork.FormatAddress(network.PrefixLength == 32 ? network.Network : network.Network + 1);
            var best = ObservedNetwork.BestUnambiguousRoute(snapshot.Routes, probe);
            if (best is null)
            {
                yield return new("PROTECTED ROUTE", ResultState.Unknown, $"No unambiguous route was collected for {target}.", "Route table did not include a unique usable interface.", "Observed route table");
                continue;
            }

            var specific = CidrNetwork.TryParse(best.Destination, out var routeNetwork) && routeNetwork.PrefixLength > 0;
            yield return specific
                ? new("PROTECTED ROUTE", ResultState.Pass, $"{probe} in {target} has a specific observed route.", RouteAnalyzer.Describe(best), "Observed sample route; not subnet-wide access or authentication proof")
                : new("PROTECTED ROUTE", ResultState.Fail, $"{target} only follows the default route.", RouteAnalyzer.Describe(best), "Observed route table");
        }
    }

    private static DiagnosticObservation DefaultRouteObservation(DiagnosticSnapshot snapshot)
    {
        var best = ObservedNetwork.BestUnambiguousRoute(snapshot.Routes, "1.1.1.1");
        if (best is null) return new("INTERNET ROUTE", ResultState.Unknown, "No unambiguous IPv4 Internet route was collected.", "Route evidence is missing or has equally preferred interfaces.", "Observed route table");
        var adapter = ObservedNetwork.ProtectedAdapter(snapshot);
        if (adapter is null) return new("INTERNET ROUTE", ResultState.Unknown, "Internet route collected; the private-route adapter is not identified.", RouteAnalyzer.Describe(best), "Observed route to 1.1.1.1; no reachability claim");
        var defaultUsesProtectedInterface = adapter.Index == best.InterfaceIndex;
        if (snapshot.Profile.TunnelMode == TunnelMode.Full)
            return defaultUsesProtectedInterface
                ? new("INTERNET ROUTE", ResultState.Pass, "Internet route appears to use the protected-route interface.", RouteAnalyzer.Describe(best), "Observed route table")
                : new("INTERNET ROUTE", ResultState.Fail, "Full tunnel expected, but Internet route uses a different interface.", RouteAnalyzer.Describe(best), "Observed route table");

        return defaultUsesProtectedInterface
            ? new("INTERNET ROUTE", ResultState.Fail, "Split tunnel expected, but Internet route appears to use the protected-route interface.", RouteAnalyzer.Describe(best), "Observed route table")
            : new("INTERNET ROUTE", ResultState.Pass, "Internet route remains separate from protected-route interface.", RouteAnalyzer.Describe(best), "Observed route table");
    }

    private static IEnumerable<DiagnosticObservation> AssignedAddressObservations(DiagnosticSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.Status.TunnelIp))
        {
            yield return new("ASSIGNED ADDRESS", ResultState.Pass, "Provider reported an assigned tunnel address.", snapshot.Status.TunnelIp, "Provider-reported");
            yield break;
        }

        var adapter = ObservedNetwork.ProtectedAdapter(snapshot);
        if (adapter is not null)
        {
            var address = ObservedNetwork.Address(adapter);
            if (address is null)
            {
                yield return new("ASSIGNED ADDRESS", ResultState.Unknown, $"Protected-route interface {adapter.Name} has no usable IPv4 address.", $"Interface {adapter.Index}: {adapter.Description}", "Observed adapter");
                yield break;
            }
            yield return new("ASSIGNED ADDRESS", ResultState.Pass, $"Protected-route interface {adapter.Name} has observed IPv4 address {address}.", $"Interface {adapter.Index}: {adapter.Description}", "Observed adapter; not authentication proof");
            yield break;
        }
        yield return new("ASSIGNED ADDRESS", ResultState.Unknown, "No unambiguous active private-route adapter was observed.", "Provider did not report a tunnel IP and route/interface evidence is incomplete or conflicting.", "Observed adapter");
    }
}
