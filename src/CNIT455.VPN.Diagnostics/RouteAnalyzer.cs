using CNIT455.VPN.Core;
namespace CNIT455.VPN.Diagnostics;
public static class RouteAnalyzer
{
    public static RouteEntry? BestRoute(IEnumerable<RouteEntry> routes,string address)
    {
        if(!CidrNetwork.TryAddress(address,out var ip)) return null;
        var candidates=routes.Select(r=>(Route:r,Valid:CidrNetwork.TryParse(r.Destination,out var c),Cidr:c)).Where(x=>x.Valid&&x.Cidr.Contains(ip)).OrderByDescending(x=>x.Cidr.PrefixLength).ThenBy(x=>x.Route.EffectiveMetric).ToList();
        return candidates.Count==0?null:candidates[0].Route;
    }
    private static bool? UsesTunnel(IReadOnlyList<RouteEntry> routes,uint ip,Func<RouteEntry,bool> tunnel)
    {
        var candidates=routes.Where(r=>CidrNetwork.TryParse(r.Destination,out var n)&&n.Contains(ip)).ToList();
        if(candidates.Count==0)return null;
        var prefix=candidates.Max(r=>{CidrNetwork.TryParse(r.Destination,out var n);return n.PrefixLength;});
        candidates=candidates.Where(r=>{CidrNetwork.TryParse(r.Destination,out var n);return n.PrefixLength==prefix;}).ToList();
        var metric=candidates.Min(r=>r.EffectiveMetric);
        var choices=candidates.Where(r=>r.EffectiveMetric==metric).Select(tunnel).Distinct().ToList();
        return choices.Count==1?choices[0]:null;
    }
    public static RouteAnalysis Analyze(VpnProfile profile,IEnumerable<RouteEntry> routeSource,int? vpnInterfaceIndex=null)
    {
        var routes=routeSource.Where(r=>CidrNetwork.TryParse(r.Destination,out _)).ToList();
        var evidence=new List<string>();
        bool Tunnel(RouteEntry r)=>vpnInterfaceIndex.HasValue?r.InterfaceIndex==vpnInterfaceIndex.Value:r.IsVpn;
        if(routes.Count==0)return new(ResultState.Unknown,"No IPv4 route table was collected.",evidence);
        if(!routes.Any(Tunnel))return new(ResultState.Unknown,"The active tunnel interface could not be identified. Connect the VPN and collect again.",evidence);
        bool unknown=false,failed=false;
        foreach(var target in profile.PermittedNetworks.Where(s=>s!="0.0.0.0/0"))
        {
            if(!CidrNetwork.TryParse(target,out var network)){unknown=true;evidence.Add($"Invalid target: {target}");continue;}
            // Partition at every route boundary so a more-specific local route cannot hide behind a /24 VPN route.
            var points=new SortedSet<ulong>{network.Network,(ulong)network.Last+1};
            foreach(var route in routes)
            {
                CidrNetwork.TryParse(route.Destination,out var n);
                if(!n.Overlaps(network))continue;
                points.Add(Math.Max(n.Network,network.Network));points.Add(Math.Min((ulong)n.Last+1,(ulong)network.Last+1));
            }
            foreach(var point in points.Where(x=>x<=network.Last))
            {
                var address=CidrNetwork.FormatAddress((uint)point);var best=BestRoute(routes,address);var uses=UsesTunnel(routes,(uint)point,Tunnel);
                evidence.Add($"{target} at {address}: {Describe(best)}; {(uses is true?"VPN":uses is false?"LOCAL":"UNKNOWN / ambiguous equal-cost routes")}");
                if(uses is null)unknown=true;else if(!uses.Value)failed=true;
            }
        }
        var broad=routes.Where(r=>CidrNetwork.TryParse(r.Destination,out var n)&&n.PrefixLength<=1).ToList();
        foreach(var ip in new[]{0u,0x80000000u})
        {
            var via=UsesTunnel(broad,ip,Tunnel);
            evidence.Add($"IPv4 default half {CidrNetwork.FormatAddress(ip)}/1: {Describe(BestRoute(broad,CidrNetwork.FormatAddress(ip)))}");
            if(via is null)unknown=true;else if(via.Value!=(profile.TunnelMode==TunnelMode.Full))failed=true;
        }
        evidence.Add("Scope: IPv4 route policy. IPv6, DNS leaks, gateway bypass routes, reachability, server authorization and encryption need separate verification.");
        return new(failed?ResultState.Fail:unknown?ResultState.Unknown:ResultState.Pass,failed?"Actual IPv4 routes conflict with the selected tunnel policy.":unknown?"Route evidence is incomplete or ambiguous.":$"IPv4 {profile.TunnelMode.ToString().ToLowerInvariant()} tunnel routing matches the selected policy.",evidence);
    }
    public static string Describe(RouteEntry? r)=>r is null?"no route":$"{r.Destination} via {r.NextHop}, {r.InterfaceAlias} (if {r.InterfaceIndex}, metric {r.EffectiveMetric})";
}
