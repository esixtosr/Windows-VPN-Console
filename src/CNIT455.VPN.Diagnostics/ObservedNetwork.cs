using CNIT455.VPN.Core;

namespace CNIT455.VPN.Diagnostics;

/// <summary>Read-only adapter evidence. Never upgrades a provider's tunnel state.</summary>
public static class ObservedNetwork
{
    public static NetworkAdapterInfo? ProtectedAdapter(DiagnosticSnapshot snapshot)
    {
        var targets = snapshot.Profile.PermittedNetworks;
        // Without private routes we cannot infer an external client's adapter.
        if (targets.Count == 0 || targets.Contains("0.0.0.0/0")) return null;
        int? selected = null;
        foreach (var target in targets)
        {
            if (!CidrNetwork.TryParse(target, out var network)) return null;
            var points = new SortedSet<ulong> { network.Network };
            foreach (var route in snapshot.Routes)
            {
                if (!CidrNetwork.TryParse(route.Destination, out var part) || !part.Overlaps(network)) continue;
                points.Add(Math.Max(network.Network, part.Network));
                if (part.Last < network.Last) points.Add((ulong)part.Last + 1);
            }
            foreach (var point in points)
            {
                var route = BestUnambiguousRoute(snapshot.Routes, CidrNetwork.FormatAddress((uint)point));
                if (route is null || !CidrNetwork.TryParse(route.Destination, out var prefix) || prefix.PrefixLength == 0) return null;
                if (selected.HasValue && selected != route.InterfaceIndex) return null;
                selected = route.InterfaceIndex;
            }
        }
        return snapshot.Adapters.SingleOrDefault(a => a.Index == selected && a.Status.Equals("Up", StringComparison.OrdinalIgnoreCase));
    }

    public static RouteEntry? BestUnambiguousRoute(IEnumerable<RouteEntry> routes, string address)
    {
        if (!CidrNetwork.TryAddress(address, out var ip)) return null;
        var candidates = routes.Where(r => CidrNetwork.TryParse(r.Destination, out var network) && network.Contains(ip)).ToArray();
        var best = RouteAnalyzer.BestRoute(candidates, address);
        if (best is null) return null;
        CidrNetwork.TryParse(best.Destination, out var chosen);
        var peers = candidates.Where(r => CidrNetwork.TryParse(r.Destination, out var n) && n.PrefixLength == chosen.PrefixLength && r.EffectiveMetric == best.EffectiveMetric);
        return peers.Select(r => r.InterfaceIndex).Distinct().Count() == 1 ? best : null;
    }

    public static string? Address(NetworkAdapterInfo? adapter) => adapter?.Addresses.FirstOrDefault(address =>
        CidrNetwork.TryAddress(address, out var value) && value != 0 &&
        !address.StartsWith("127.", StringComparison.Ordinal) && !address.StartsWith("169.254.", StringComparison.Ordinal));

    public static NetworkAdapterInfo? InternetAdapter(DiagnosticSnapshot snapshot)
    {
        var route = BestUnambiguousRoute(snapshot.Routes, "1.1.1.1");
        return route is null ? null : snapshot.Adapters.FirstOrDefault(a => a.Index == route.InterfaceIndex && a.Status.Equals("Up", StringComparison.OrdinalIgnoreCase));
    }
}
