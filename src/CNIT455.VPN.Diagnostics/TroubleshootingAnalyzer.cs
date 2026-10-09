using CNIT455.VPN.Core;
namespace CNIT455.VPN.Diagnostics;
public static class TroubleshootingAnalyzer
{
    public static IReadOnlyList<Finding> Analyze(DiagnosticSnapshot snapshot)
    {
        var result=new List<Finding>();
        var logs=snapshot.Logs;
        bool Error(VpnStage stage)=>logs.Any(l=>l.Stage==stage&&l.Severity==LogSeverity.Error);
        bool Contains(string text)=>logs.Any(l=>l.Message.Contains(text,StringComparison.OrdinalIgnoreCase));
        if(snapshot.GatewayReachable==ResultState.Fail || Error(VpnStage.Gateway))result.Add(new("TRANSPORT",ResultState.Fail,"Gateway communication failed.","Verify gateway address, local route, external interface and upstream network. ICMP silence alone does not prove the gateway is down."));
        if(Error(VpnStage.IkePhase1))result.Add(Contains("no response")?new("TRANSPORT",ResultState.Fail,"IKE request did not receive a response.","Check destination address, input firewall, UDP 500/4500, NAT-T and server status."):new("AUTHENTICATION",ResultState.Fail,"IKE Phase 1 failed.","Check PSK, peer identity, IKE mode and Phase 1 proposal. Consult both peers for the exact cause."));
        if(Error(VpnStage.Xauth))result.Add(new("AUTHENTICATION",ResultState.Fail,"XAUTH failed after reaching user authentication.","Check username/password and the server's local or AD/RADIUS/LDAP policy. Do not rebuild the tunnel before checking server authentication logs."));
        if(Error(VpnStage.Radius))result.Add(new("AUTHENTICATION",ResultState.Fail,"Remote RADIUS authentication failed.","Check RADIUS reachability, shared secret, NPS policy, group membership, time and firewall access. Compare with a successful Local Test."));
        if(Error(VpnStage.Ldap))result.Add(new("AUTHENTICATION",ResultState.Fail,"LDAP authentication failed.","Check the server-side LDAP bind, TLS trust, search base, group requirement and domain credentials."));
        if(Error(VpnStage.Authentication)||Error(VpnStage.Eap))result.Add(new("AUTHENTICATION",ResultState.Fail,"The provider reported authentication failure.","Verify credentials, certificate validity/trust, EAP method and server authentication policy."));
        if(snapshot.Profile.Protocol==VpnProtocol.WireGuard && (Contains("no handshake")||Contains("handshake failure")||(Error(VpnStage.Tunnel)&&Contains("handshake"))))result.Add(new("TRANSPORT",ResultState.Fail,"No WireGuard handshake was reported.","Check endpoint, UDP port, public keys, PSK, firewall and peer configuration. Sending UDP is not proof of reachability."));
        if(snapshot.Routing.Result==ResultState.Fail || Error(VpnStage.Routing))result.Add(new("ROUTING",ResultState.Fail,snapshot.Routing.Result==ResultState.Fail?snapshot.Routing.Summary:"Route installation failed.",snapshot.Profile.Protocol==VpnProtocol.OpenVpn?"Check pushed routes or redirect-gateway settings and the actual interface metrics.":"Check Mode Config, split routes, AllowedIPs, interface metrics, return routes, site-to-site routing and NAT exemptions."));
        if(Error(VpnStage.Dns))result.Add(new("DNS",ResultState.Fail,"The provider reported a DNS failure.","Inspect tunnel DNS servers, suffix and reachability. Compare an IP address test with a hostname test."));
        if(Error(VpnStage.Connectivity) && snapshot.Status.State==VpnState.Connected)result.Add(new("FIREWALL / ROUTING",ResultState.Unknown,"A tunnel is up but destination response is missing.","Check destination host firewall, VPN firewall rules, return route, NAT exemption and site-to-site VPN. A timeout cannot distinguish these causes."));
        if(result.Count==0)result.Add(new("OBSERVATION",ResultState.Unknown,"No specific connection failure was identified by this check.","If using an external client, check its status there. Verify access to a private host separately; collect client and server logs only if a problem remains."));
        return result;
    }
}
