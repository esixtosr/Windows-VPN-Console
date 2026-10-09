using CNIT455.VPN.Core;
using CNIT455.VPN.Diagnostics;

namespace CNIT455.VPN.App;

public sealed partial class MainViewModel
{
    public string PageTitle => DisplayLabels.For(SelectedPage);
    public bool IsExternalClient => capabilities.TryGetValue(ProviderId, out var caps) && caps.IntegrationType == ProviderIntegrationType.ExternalInteractive;
    public string ClientName => ProviderId switch { "ncp" => "NCP", "shrew" => "Shrew", _ => EngineName };
    public string ConnectLabel => IsExternalClient ? "Open " + ClientName : "Connect";
    public string DisconnectLabel => IsExternalClient ? "Disconnect in " + ClientName : "Disconnect";
    public string StatusLabel => connection.State switch
    {
        VpnState.Connected => ProviderId == "mock" ? "Simulated connection" : "Connected",
        VpnState.Connecting => "Connecting…", VpnState.Disconnecting => "Disconnecting…",
        VpnState.Failed => "Needs attention",
        _ when IsExternalClient => "Managed in " + ClientName,
        VpnState.Disconnected => "Disconnected", _ => "Not checked"
    };
    public string StatusColor => connection.State switch
    {
        VpnState.Connected => "#87F4D9", VpnState.Failed => "#FFBFAB", _ => "#C9D8E4"
    };
    public string EngineHelp => IsExternalClient
        ? $"Connect and disconnect in {ClientName}. Use Check connection here to inspect Windows addresses and routes. Settings below do not reconfigure {ClientName}."
        : ProviderId == "native" && Protocol == VpnProtocol.Ikev2
            ? "Windows handles IKEv2 sign-in. After signing in, check the connection here."
            : "Choose a saved profile, connect, then check the network paths. Credentials stay on this computer.";
    public string SetupHint => string.IsNullOrWhiteSpace(SelectedProfile?.Gateway)
        ? "Start in Edit profile: choose your VPN type and engine, then enter the gateway supplied by your network administrator."
        : IsExternalClient ? $"Use the matching profile in {ClientName}. This console keeps your reference settings and diagnostics together." : "Your profile is ready to review. Connect when you are ready.";
    public string EvidenceTime => CurrentSnapshot is { } current ? $"Last checked {current.CapturedAt.LocalDateTime:g} · snapshot, not a live status" : "Not checked yet · use Check connection";
    private DiagnosticSnapshot? CurrentSnapshot => snapshot is { } current && SelectedProfile is { } profile &&
        current.Profile.Id == profile.Id && current.Profile.ProviderId == profile.ProviderId && current.Profile.Protocol == profile.Protocol &&
        current.Profile.Gateway == profile.Gateway && current.Profile.TunnelMode == profile.TunnelMode &&
        current.Profile.PermittedNetworks.SequenceEqual(profile.PermittedNetworks) ? current : null;
    private NetworkAdapterInfo? ObservedAdapter => CurrentSnapshot is { } current ? ObservedNetwork.ProtectedAdapter(current) : null;
    public string AddressSource => !string.IsNullOrWhiteSpace(connection.TunnelIp) ? "Reported by the VPN engine"
        : ObservedAdapter is not null ? "Observed on the private-route adapter. This is not proof of authentication or target access." : "No unambiguous active adapter was found in the last check.";
    public string NetworkHeadline => CurrentSnapshot is null ? "Check your network paths"
        : ObservedAdapter is not null ? "Private-network route observed" : "More network evidence needed";
    public string NetworkHelp => CurrentSnapshot is not { } current ? "Run a check after connecting. It does not change your network settings."
        : current.Routing.Result != ResultState.Unknown ? current.Routing.Summary
        : ObservedAdapter is not null ? "Windows has routes to the listed private networks. The engine's tunnel state is still not independently verified."
        : "Review Diagnostics for collected routes and any missing evidence. No connection failure is assumed from missing status alone.";
    public string DependencySummary => $"{Dependencies.Count(d => d.Installed && d.Id is not ("administrator" or "powershell"))} VPN engine(s) detected. You only need the engine used by your profile.";
    public IEnumerable<EngineRow> EngineRows => Dependencies.Where(d => d.Id is not ("administrator" or "powershell")).Select(d => new EngineRow(d));
    private DependencyInfo? selectedDependency;
    public DependencyInfo? SelectedDependency { get => selectedDependency; set { if (Set(ref selectedDependency, value)) { Raise(nameof(DependencyVersion)); Raise(nameof(HasDownload)); } } }
    public string DependencyVersion => string.IsNullOrWhiteSpace(SelectedDependency?.Version) ? "Executable version not reported" : "Executable version: " + SelectedDependency.Version;
    public bool HasDownload => Uri.TryCreate(SelectedDependency?.DownloadUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https";
    public bool ShowDirectDisconnect => !IsExternalClient;
    public bool CanStartConnection => !IsBusy && !HasActiveConnection;
    public string SelectedEngineReadiness => Dependencies.FirstOrDefault(d => d.Id == ProviderId) is { } engine
        ? engine.Installed ? "Engine detected on this computer" : "Engine not detected · visit VPN engines" : "Engine detection has not completed";

    private void RaisePresentationProperties()
    {
        foreach (var name in new[] { nameof(IsExternalClient), nameof(ClientName), nameof(ConnectLabel), nameof(DisconnectLabel), nameof(StatusLabel), nameof(StatusColor), nameof(EngineHelp), nameof(SetupHint), nameof(EvidenceTime), nameof(AddressSource), nameof(NetworkHeadline), nameof(NetworkHelp), nameof(LocalAdapter), nameof(SelectedEngineReadiness), nameof(DependencySummary), nameof(EngineRows), nameof(ShowDirectDisconnect), nameof(CanStartConnection) }) Raise(name);
    }
}
