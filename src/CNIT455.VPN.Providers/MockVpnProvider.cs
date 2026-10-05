using CNIT455.VPN.Core;
namespace CNIT455.VPN.Providers;

public sealed class MockVpnProvider(SecretRedactor? redactor = null) : VpnProviderBase(redactor)
{
    private readonly Dictionary<Guid,VpnStatus> statuses = [];
    private readonly SemaphoreSlim gate = new(1,1);
    public TimeSpan StageDelay { get; set; } = TimeSpan.FromMilliseconds(220);
    public override string Id => "mock";
    public override Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ProviderCapabilities(Id,"Mock (simulated)",Enum.GetValues<VpnProtocol>(),Enum.GetValues<AuthenticationMode>(),true,true,true,false,"Developer Mode only. All routes, addresses and connection stages are simulated; no host networking changes."));
    public override Task<DependencyInfo> DetectInstallation(CancellationToken cancellationToken = default) => Task.FromResult(new DependencyInfo(Id,"Mock",true,null,"0.1.0","Built in; simulation only."));
    public override IReadOnlyList<ValidationIssue> ValidateProfile(VpnProfile profile) => [];
    public override async Task<ProviderResult> ConnectAsync(VpnProfile profile, VpnSecrets secrets, CancellationToken cancellationToken = default)
    {
        Register(secrets); await gate.WaitAsync(cancellationToken);
        try
        {
            statuses[profile.Id] = new(VpnState.Connecting,"SIMULATION: connecting");
            var stages = new[] { (VpnStage.Gateway,MockFailure.NoGateway,"Gateway response"), (VpnStage.IkePhase1,MockFailure.PskMismatch,"IKE / pre-shared-key negotiation"), (VpnStage.Xauth,MockFailure.XauthFailure,"User authentication"), (VpnStage.Radius,MockFailure.RadiusFailure,"RADIUS authentication"), (VpnStage.Tunnel,MockFailure.WireGuardHandshakeFailure,"Tunnel handshake"), (VpnStage.Routing,MockFailure.RouteFailure,"Route installation"), (VpnStage.Dns,MockFailure.DnsFailure,"DNS lookup") };
            foreach (var (stage,failure,label) in stages)
            {
                Log(stage,$"SIMULATION: {label} started."); await Task.Delay(StageDelay,cancellationToken);
                if (profile.MockFailure == failure)
                {
                    var message = $"SIMULATION: {label} failed. Earlier simulated stages completed; subsequent stages were not reached. Check the selected {failure} scenario.";
                    statuses[profile.Id] = new(VpnState.Failed,message); Log(stage,message,LogSeverity.Error); return new(false,message,statuses[profile.Id]);
                }
                Log(stage,$"SIMULATION: {label} completed.");
            }
            var routes = profile.TunnelMode == TunnelMode.Full ? "0.0.0.0/0 (simulated)" : string.Join(", ",profile.PermittedNetworks);
            statuses[profile.Id] = new(VpnState.Connected,"SIMULATION: connected; no real tunnel exists.","10.254.0.10","Mock adapter",DateTimeOffset.Now,new Dictionary<string,string>{{"Simulated routes",routes}});
            return new(true,statuses[profile.Id].Message,statuses[profile.Id]);
        }
        catch (OperationCanceledException) { statuses[profile.Id] = new(VpnState.Disconnected,"Simulation canceled"); throw; }
        finally { gate.Release(); }
    }
    public override async Task<ProviderResult> DisconnectAsync(VpnProfile profile, CancellationToken cancellationToken = default)
    { await gate.WaitAsync(cancellationToken); try { statuses[profile.Id] = new(VpnState.Disconnected,"SIMULATION: disconnected and simulated routes removed."); Log(VpnStage.Disconnect,statuses[profile.Id].Message); return new(true,statuses[profile.Id].Message,statuses[profile.Id]); } finally { gate.Release(); } }
    public override Task<VpnStatus> GetStatusAsync(VpnProfile profile, CancellationToken cancellationToken = default) => Task.FromResult(statuses.GetValueOrDefault(profile.Id,new(VpnState.Disconnected,"SIMULATION: idle")));
}
