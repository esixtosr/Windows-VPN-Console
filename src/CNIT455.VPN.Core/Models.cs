using System.Text.Json.Serialization;
namespace CNIT455.VPN.Core;
public enum VpnProtocol { IpsecMobile, L2tpIpsec, Ikev2, Sstp, OpenVpn, WireGuard }
public enum AuthenticationMode { PreSharedKey, Certificate, PskAndUsername, CertificateAndUsername, UsernamePassword, ProviderDefault }
public enum TunnelMode { Split, Full }
public enum LabPolicy { Strict, Extended, Custom }
public enum AuthBackend { LocalTest, Radius, Ldap }
public enum VpnState { Unknown, Disconnected, Connecting, Connected, Disconnecting, Failed, ExternalClient }
public enum ResultState { Unknown, Pass, Fail }
public enum LogSeverity { Debug, Information, Warning, Error }
public enum VpnStage { Initialization, Gateway, IkePhase1, Authentication, Xauth, Eap, Radius, Ldap, IkePhase2, Tunnel, AddressAssignment, Routing, Dns, Connectivity, Disconnect }
public enum MockFailure { None, NoGateway, PskMismatch, XauthFailure, RadiusFailure, RouteFailure, DnsFailure, WireGuardHandshakeFailure }
public sealed record VpnProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "New connection";
    public VpnProtocol Protocol { get; set; } = VpnProtocol.L2tpIpsec;
    public string ProviderId { get; set; } = "native";
    public string Gateway { get; set; } = "";
    public int Port { get; set; } = 0;
    public AuthenticationMode Authentication { get; set; } = AuthenticationMode.PskAndUsername;
    public AuthBackend AuthBackend { get; set; } = AuthBackend.LocalTest;
    public TunnelMode TunnelMode { get; set; } = TunnelMode.Split;
    public List<string> PermittedNetworks { get; set; } = [];
    public List<string> ForbiddenNetworks { get; set; } = [];
    public string Username { get; set; } = "";
    public string Domain { get; set; } = "";
    public string ImportedConfigPath { get; set; } = "";
    public string ExternalProfileName { get; set; } = "";
    public string CertificateThumbprint { get; set; } = "";
    public string PeerPublicKey { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string TunnelAddress { get; set; } = "";
    public string Dns { get; set; } = "";
    public int ListenPort { get; set; } = 0;
    public bool IsLab { get; set; } = true;
    public int GroupNumber { get; set; } = 33;
    public LabPolicy LabPolicy { get; set; } = LabPolicy.Strict;
    public MockFailure MockFailure { get; set; }
    public string Notes { get; set; } = "";
}
// Secrets are separate from serializable profiles. The UI owns their lifetime.
public sealed class VpnSecrets
{
    [JsonIgnore] public string Password { get; set; } = "";
    [JsonIgnore] public string Psk { get; set; } = "";
    [JsonIgnore] public string PrivateKey { get; set; } = "";
    [JsonIgnore] public string RadiusSecret { get; set; } = "";
    public void Clear() { Password = Psk = PrivateKey = RadiusSecret = ""; }
}
public sealed record VpnLogEvent(DateTimeOffset Timestamp, LogSeverity Severity, string Provider, VpnStage Stage, string Message);
public sealed record ValidationIssue(string Field, string Message, bool IsError = true);
public sealed record ProviderCapabilities(string Id, string DisplayName, IReadOnlyList<VpnProtocol> Protocols, IReadOnlyList<AuthenticationMode> AuthenticationModes, bool CanConnect, bool CanDisconnect, bool CanStreamLogs, bool RequiresAdministrator, string Limitations = "");
public sealed record DependencyInfo(string Id, string Name, bool Installed, string? ExecutablePath, string? Version, string Details, string? DownloadUrl = null, string? InstallCommand = null);
public sealed record VpnStatus(VpnState State, string Message, string? TunnelIp = null, string? InterfaceName = null, DateTimeOffset? ConnectedSince = null, IReadOnlyDictionary<string,string>? Details = null);
public sealed record ProviderResult(bool Success, string Message, VpnStatus? Status = null);
public sealed record RouteEntry(string Destination, string NextHop, int InterfaceIndex, string InterfaceAlias, int RouteMetric, int InterfaceMetric, bool IsVpn = false)
{ public long EffectiveMetric => (long)RouteMetric + InterfaceMetric; }
public sealed record NetworkAdapterInfo(int Index, string Name, string Description, string Status, IReadOnlyList<string> Addresses, IReadOnlyList<string> DnsServers, IReadOnlyList<string> Gateways);
public sealed record RouteAnalysis(ResultState Result, string Summary, IReadOnlyList<string> Evidence);
public sealed record Finding(string Category, ResultState Result, string Summary, string NextChecks);
public sealed record CheckoffItem(string Id, string Text, ResultState Result = ResultState.Unknown, string Evidence = "");
public sealed record AccessRule(string Network, string Policy, string Reason);
public sealed class DiagnosticSnapshot
{
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.Now;
    public string OsVersion { get; set; } = Environment.OSVersion.ToString();
    public string AppVersion { get; set; } = "0.1.0";
    public bool IsAdministrator { get; set; }
    public VpnProfile Profile { get; set; } = new();
    public VpnStatus Status { get; set; } = new(VpnState.Unknown, "Not queried");
    public List<RouteEntry> Routes { get; set; } = [];
    public List<NetworkAdapterInfo> Adapters { get; set; } = [];
    public List<DependencyInfo> Dependencies { get; set; } = [];
    public List<VpnLogEvent> Logs { get; set; } = [];
    public List<Finding> Findings { get; set; } = [];
    public RouteAnalysis Routing { get; set; } = new(ResultState.Unknown, "Not measured", []);
    public ResultState GatewayReachable { get; set; }
    public string ServerOutput { get; set; } = "";
    public string SystemEvents { get; set; } = "";
    public string CollectionNotes { get; set; } = "";
}
public interface IVpnProvider
{
    string Id { get; }
    event EventHandler<VpnLogEvent>? LogReceived;
    Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
    Task<DependencyInfo> DetectInstallation(CancellationToken cancellationToken = default);
    IReadOnlyList<ValidationIssue> ValidateProfile(VpnProfile profile);
    Task<ProviderResult> ConnectAsync(VpnProfile profile, VpnSecrets secrets, CancellationToken cancellationToken = default);
    Task<ProviderResult> DisconnectAsync(VpnProfile profile, CancellationToken cancellationToken = default);
    Task<VpnStatus> GetStatusAsync(VpnProfile profile, CancellationToken cancellationToken = default);
    Task StartLogStreamAsync(VpnProfile profile, CancellationToken cancellationToken = default);
    Task StopLogStreamAsync(CancellationToken cancellationToken = default);
    Task<string> GetDiagnosticsAsync(VpnProfile profile, CancellationToken cancellationToken = default);
}
public interface ISecretStore
{
    Task SaveAsync(Guid profileId, VpnSecrets secrets);
    Task<VpnSecrets?> LoadAsync(Guid profileId);
    Task DeleteAsync(Guid profileId);
}
