using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CNIT455.VPN.Core;
using CNIT455.VPN.Diagnostics;
using CNIT455.VPN.Providers;
using CNIT455.VPN.ConfigGenerators;
using Microsoft.Win32;

namespace CNIT455.VPN.App;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly bool smoke;
    private readonly string dataRoot;
    private readonly ProfileStore store;
    private readonly WindowsSecretStore secretStore = new();
    private readonly CertificateService certificateService = new();
    private readonly SemaphoreSlim evidenceGate = new(1, 1);
    private readonly SecretRedactor redactor = new();
    private readonly IReadOnlyList<IVpnProvider> providers;
    private readonly Dictionary<string, ProviderCapabilities> capabilities = [];
    private readonly List<VpnLogEvent> history = [];
    private readonly Dictionary<string, ObservableCollection<CheckoffRow>> evidence = [];
    private readonly DispatcherTimer statusTimer;
    private readonly JsonSerializerOptions json = new() { WriteIndented = true };
    private UserSettings settings = new();
    private VpnProfile? selectedProfile;
    private string selectedPage = "Dashboard", statusText = "Ready", connectionText = "DISCONNECTED", diagnosticText = "Select a connection, then run diagnostics. Measurements are reported as UNKNOWN until observed.", serverText = "Choose a template and generate a reviewed configuration worksheet.", logText = "", pastedServerOutput = "", generatedPsk = "";
    private bool isBusy, advanced, logsPaused, rememberSecrets, revealPsk;
    private string logFilter = "All", logSearch = "";
    private VpnStatus connection = new(VpnState.Disconnected, "No connection started");
    private DiagnosticSnapshot? snapshot;
    private LabTopology topology = LabTopology.Create(33);
    private string selectedCheckoff = "";
    private bool shutdownComplete, statusPolling;
    private long observationRevision;
    public MainViewModel(bool smoke = false)
    {
        this.smoke = smoke;
        dataRoot = smoke ? Path.Combine(Path.GetTempPath(), "CNIT455-smoke-" + Guid.NewGuid().ToString("N")) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CNIT455-VPN-Console");
        Directory.CreateDirectory(dataRoot);
        store = new ProfileStore(Path.Combine(dataRoot, "Profiles"));
        providers = ProviderRegistry.CreateDefault(redactor);
        foreach (var provider in providers) provider.LogReceived += (_, item) => Application.Current.Dispatcher.BeginInvoke(() => { if (item.Provider == ProviderId) AppendLog(item); });
        statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        statusTimer.Tick += async (_, _) => { if (!IsBusy && SelectedProfile is not null) await RefreshStatusAsync(); Raise(nameof(ConnectedDuration)); };
        ConnectCommand = Command(ConnectAsync); DisconnectCommand = Command(DisconnectAsync); TestCommand = Command(CollectDiagnosticsAsync); CopyDiagnosticsCommand = Command(CopyDiagnosticsAsync);
        SaveProfileCommand = Command(SaveProfileAsync); NewProfileCommand = Command(NewProfileAsync); DuplicateProfileCommand = Command(DuplicateProfileAsync); DeleteProfileCommand = Command(DeleteProfileAsync);
        ImportProfileCommand = Command(ImportProfileAsync); ExportProfileCommand = Command(ExportProfileAsync); ResetProfileCommand = Command(ResetProfileAsync); ValidateCommand = Command(ValidateAsync);
        ImportConfigCommand = Command(ImportConfigAsync); ExportWireGuardCommand = Command(ExportWireGuardAsync); GenerateKeysCommand = Command(GenerateKeysAsync);
        SaveSettingsCommand = Command(SaveSettingsAsync); ApplyGroupCommand = Command(ApplyGroupAsync); SeedLabCommand = Command(SeedLabAsync); RefreshDependenciesCommand = Command(DetectDependenciesAsync);
        GenerateServerCommand = Command(GenerateServerAsync); CopyServerCommand = Command(() => CopyGeneratedAsync(ServerText, "Configuration copied with secret placeholders.")); SaveServerCommand = Command(SaveServerAsync); CopyServerDiagnosticsCommand = Command(() => CopyGeneratedAsync(ServerConfigGenerator.DiagnosticCommands(ServerOptions), "Server diagnostic commands copied."));
        GeneratePskCommand = Command(GeneratePskAsync); CopyPskCommand = Command(() => { if (generatedPsk.Length == 0) throw new InvalidOperationException("Generate a PSK first."); Clipboard.SetText(generatedPsk); StatusText = "Clipboard contains a secret. Paste only into your VPN configuration."; return Task.CompletedTask; });
        CopyLogsCommand = Command(() => CopyAsync(FormatLogs(), "Redacted logs copied.")); SaveLogsCommand = Command(SaveLogsAsync); ClearLogsCommand = Command(ClearLogsAsync);
        PauseLogsCommand = Command(() => { LogsPaused = !LogsPaused; if (!LogsPaused) RefreshLogText(); return Task.CompletedTask; });
        ExportEvidenceCommand = Command(ExportEvidenceAsync); RefreshCertificatesCommand = Command(RefreshCertificatesAsync); ImportCertificateCommand = Command(ImportCertificateAsync); ForgetSecretsCommand = Command(ForgetSecretsAsync);
    }
    private ICommand Command(Func<Task> action) => new AsyncCommand(async () =>
    {
        if (IsBusy) { StatusText = "Wait for the current operation to finish."; return; }
        await action();
    }, ReportError);
    public ObservableCollection<string> Navigation { get; } = ["Dashboard", "Connections", "Lab 2", "Server Config", "Diagnostics", "Check-Off", "Dependencies", "Settings", "About"];
    public ObservableCollection<VpnProfile> Profiles { get; } = [];
    public ObservableCollection<ProviderChoice> ProviderChoices { get; } = [];
    public ObservableCollection<AuthenticationMode> AuthenticationChoices { get; } = [];
    public ObservableCollection<DependencyInfo> Dependencies { get; } = [];
    public ObservableCollection<AccessRule> AccessRules { get; } = [];
    public ObservableCollection<CertificateInfo> Certificates { get; } = [];
    public ObservableCollection<LabVpnCheckoff> Checkoffs { get; } = [];
    public ObservableCollection<CheckoffRow> CheckoffRows { get; } = [];
    public ObservableCollection<RouteEntry> Routes { get; } = [];
    public ObservableCollection<Finding> Findings { get; } = [];
    public VpnSecrets Secrets { get; private set; } = new();
    public ServerConfigOptions ServerOptions { get; } = new();
    public ICommand ConnectCommand { get; } public ICommand DisconnectCommand { get; } public ICommand TestCommand { get; } public ICommand CopyDiagnosticsCommand { get; }
    public ICommand SaveProfileCommand { get; } public ICommand NewProfileCommand { get; } public ICommand DuplicateProfileCommand { get; } public ICommand DeleteProfileCommand { get; }
    public ICommand ImportProfileCommand { get; } public ICommand ExportProfileCommand { get; } public ICommand ResetProfileCommand { get; } public ICommand ValidateCommand { get; }
    public ICommand ImportConfigCommand { get; } public ICommand ExportWireGuardCommand { get; } public ICommand GenerateKeysCommand { get; }
    public ICommand SaveSettingsCommand { get; } public ICommand ApplyGroupCommand { get; } public ICommand SeedLabCommand { get; } public ICommand RefreshDependenciesCommand { get; }
    public ICommand GenerateServerCommand { get; } public ICommand CopyServerCommand { get; } public ICommand SaveServerCommand { get; } public ICommand CopyServerDiagnosticsCommand { get; }
    public ICommand GeneratePskCommand { get; } public ICommand CopyPskCommand { get; } public ICommand CopyLogsCommand { get; } public ICommand SaveLogsCommand { get; } public ICommand ClearLogsCommand { get; } public ICommand PauseLogsCommand { get; }
    public ICommand ExportEvidenceCommand { get; } public ICommand RefreshCertificatesCommand { get; } public ICommand ImportCertificateCommand { get; } public ICommand ForgetSecretsCommand { get; }
    public string SelectedPage { get => selectedPage; set { if (Set(ref selectedPage, value)) { Raise(nameof(PageDescription)); Raise(nameof(PageTitle)); } } }
    public string PageDescription => SelectedPage switch { "Dashboard" => "Connection health, routing, and the next step.", "Connections" => "Secure profiles for native and external VPN engines.", "Lab 2" => "One workspace for all seven lab VPNs.", "Server Config" => "Version-aware commands and configuration worksheets.", "Diagnostics" => "Observed evidence, routing analysis, and practical next checks.", "Check-Off" => "Record evidence and validate every requirement.", "Dependencies" => "Detect the engines installed on this Windows account.", "Settings" => "Workspace preferences, privacy, and development tools.", _ => "An independent network administration utility." };
    public VpnProfile? SelectedProfile
    {
        get => selectedProfile;
        set
        {
            if (IsBusy || HasActiveConnection) { Raise(nameof(SelectedProfile)); StatusText = "Disconnect the current tunnel before selecting another profile."; return; }
            if (!Set(ref selectedProfile, value)) return;
            observationRevision++; history.Clear(); RefreshLogText(); PastedServerOutput = "";
            Secrets.Clear(); Secrets = new(); RememberSecrets = false;
            snapshot = null; Routes.Clear(); Findings.Clear(); DiagnosticText = "Run diagnostics for this connection to collect current evidence.";
            connection = new(VpnState.Unknown, "Status not yet queried"); ConnectionText = "UNKNOWN";
            RaiseProfileProperties(); RefreshProviderChoices(); RefreshAccessRules();
            Raise(nameof(Secrets)); Raise(nameof(EditorRevision));
        }
    }
    public string EditorRevision => SelectedProfile?.Id.ToString() ?? "";
    public VpnProtocol Protocol { get => SelectedProfile?.Protocol ?? VpnProtocol.L2tpIpsec; set { if (SelectedProfile is null || SelectedProfile.Protocol == value) return; if (!CanConfigure) { Raise(); return; } Secrets.Clear(); RememberSecrets = false; SelectedProfile.Protocol = value; if (value == VpnProtocol.OpenVpn) SelectedProfile.Port = 1194; if (value == VpnProtocol.WireGuard) SelectedProfile.Port = 51820; InvalidateObservation(); RefreshProviderChoices(); RaiseProfileProperties(); Raise(nameof(EditorRevision)); } }
    public string ProviderId { get => SelectedProfile?.ProviderId ?? "native"; set { if (SelectedProfile is null || SelectedProfile.ProviderId == value) return; if (!CanConfigure) { Raise(); return; } Secrets.Clear(); RememberSecrets = false; SelectedProfile.ProviderId = value; InvalidateObservation(); RefreshAuthenticationChoices(); RaiseProfileProperties(); Raise(nameof(EditorRevision)); } }
    public AuthenticationMode Authentication { get => SelectedProfile?.Authentication ?? AuthenticationMode.ProviderDefault; set { if (SelectedProfile is null || SelectedProfile.Authentication == value) return; if (!CanConfigure) { Raise(); return; } Secrets.Clear(); RememberSecrets = false; SelectedProfile.Authentication = value; RaiseProfileProperties(); Raise(nameof(EditorRevision)); } }
    public string NetworksText { get => string.Join(Environment.NewLine, SelectedProfile?.PermittedNetworks ?? []); set { if (SelectedProfile is null) return; SelectedProfile.PermittedNetworks = ParseLines(value); Raise(); Raise(nameof(TargetNetworks)); RefreshAccessRules(); } }
    public string ForbiddenText { get => string.Join(Environment.NewLine, SelectedProfile?.ForbiddenNetworks ?? []); set { if (SelectedProfile is null) return; SelectedProfile.ForbiddenNetworks = ParseLines(value); Raise(); RefreshAccessRules(); } }
    public LabPolicy SelectedPolicy { get => SelectedProfile?.LabPolicy ?? LabPolicy.Strict; set { if (SelectedProfile is null) return; LabPresets.ApplyPolicy(SelectedProfile, Topology, value); BuildCheckoffs(); Raise(nameof(ForbiddenText)); Raise(nameof(NetworksText)); Raise(nameof(TargetNetworks)); RefreshAccessRules(); Raise(); } }
    public bool IsBusy { get => isBusy; private set { if (Set(ref isBusy, value)) { Raise(nameof(CanEdit)); Raise(nameof(CanConfigure)); Raise(nameof(CanStartConnection)); } } }
    public bool CanEdit => !IsBusy;
    public bool HasActiveConnection => connection.State is VpnState.Connected or VpnState.Connecting or VpnState.Disconnecting;
    public bool CanConfigure => !IsBusy && !HasActiveConnection;
    public bool Advanced { get => advanced; set { if (Set(ref advanced, value)) Raise(nameof(EditorRevision)); } }
    public bool DeveloperMode { get => settings.DeveloperMode; set { if (!CanConfigure && ProviderId == "mock") { Raise(); StatusText = "Disconnect the simulated tunnel before changing Developer Mode."; return; } settings.DeveloperMode = value; Raise(); RefreshProviderChoices(); Raise(nameof(EditorRevision)); } }
    public bool LabMode { get => settings.LabMode; set { settings.LabMode = value; UpdateNavigation(); Raise(); Raise(nameof(ModeLabel)); Raise(nameof(EditorRevision)); } }
    public string LogFilter { get => logFilter; set { if (Set(ref logFilter, value)) RefreshLogText(); } }
    public string LogSearch { get => logSearch; set { if (Set(ref logSearch, value)) RefreshLogText(); } }
    public string ModeLabel => LabMode ? "CNIT 455 · LAB 2" : "YOUR LOCAL WORKSPACE";
    public int GroupNumber { get => settings.GroupNumber; set { settings.GroupNumber = value; Raise(); } }
    public int LogRetentionDays { get => settings.LogRetentionDays; set { settings.LogRetentionDays = value; Raise(); } }
    public bool RememberSecrets { get => rememberSecrets; set => Set(ref rememberSecrets, value); }
    public bool LogsPaused { get => logsPaused; set { Set(ref logsPaused, value); Raise(nameof(PauseLabel)); } }
    public string PauseLabel => LogsPaused ? "Resume" : "Pause";
    public LabTopology Topology { get => topology; private set => Set(ref topology, value); }
    public string StatusText { get => statusText; set => Set(ref statusText, redactor.Redact(value)); }
    public string ConnectionText { get => connectionText; private set => Set(ref connectionText, value); }
    public string DiagnosticText { get => diagnosticText; private set => Set(ref diagnosticText, redactor.Redact(value)); }
    public string ServerText { get => serverText; private set => Set(ref serverText, value); }
    public string LogText { get => logText; private set => Set(ref logText, value); }
    public string PastedServerOutput { get => pastedServerOutput; set { if (!Set(ref pastedServerOutput, value)) return; RegisterSecrets(); if (snapshot is not null) { snapshot.ServerOutput = redactor.Redact(value); DiagnosticText = new DiagnosticFormatter(redactor).Format(snapshot); } } }
    public string ConnectionName => SelectedProfile?.Name ?? "Select a connection";
    public string EngineName => capabilities.TryGetValue(ProviderId, out var caps) ? caps.DisplayName : ProviderId;
    public string EngineIntegration => capabilities.TryGetValue(ProviderId, out var caps) ? $"{DisplayLabels.For(caps.IntegrationType)} · {caps.Licensing}" : "Integration not checked";
    public string EngineCapabilities => capabilities.TryGetValue(ProviderId, out var caps) ? string.Join(" · ", Enum.GetValues<ProviderCapabilityFlags>().Where(flag => flag != ProviderCapabilityFlags.None && caps.Flags.HasFlag(flag)).Select(flag => flag.ToString().Replace("Supports", "").Replace("Requires", "Requires "))) : "No capability metadata loaded.";
    public string EngineLimitations => capabilities.TryGetValue(ProviderId, out var caps) ? caps.Limitations : "Select an installed engine to connect.";
    public string TunnelIp => connection.TunnelIp ?? ObservedNetwork.Address(ObservedAdapter) ?? "Not observed";
    public string TunnelAdapter => connection.InterfaceName ?? ObservedAdapter?.Name ?? "Not observed";
    public string LocalAdapter => CurrentSnapshot is { } current ? ObservedNetwork.InternetAdapter(current)?.Name ?? "Not observed" : "Not checked";
    public string TargetNetworks => SelectedProfile is null || SelectedProfile.PermittedNetworks.Count == 0 ? "No networks specified" : string.Join("  ·  ", SelectedProfile.PermittedNetworks);
    public string TunnelPolicy => SelectedProfile?.TunnelMode == TunnelMode.Full ? "Full tunnel" : "Split tunnel";
    public string ConnectedDuration => connection.ConnectedSince is null ? "—" : (DateTimeOffset.Now - connection.ConnectedSince.Value).ToString(@"hh\:mm\:ss");
    public string ConnectionMessage => connection.Message;
    public string RouteSummary => CurrentSnapshot?.Routing.Summary ?? "Routing has not been checked for these settings.";
    public string ProviderDetails => connection.Details is null ? "No provider telemetry collected." : string.Join(Environment.NewLine, connection.Details.Select(x => $"{x.Key}: {x.Value}"));
    public string DataLocation => dataRoot;
    public string SelectedCheckoff { get => selectedCheckoff; set { if (!Set(ref selectedCheckoff, value)) return; RefreshCheckoffRows(); Raise(nameof(CaptureGuidance)); } }
    public string CheckoffSummary => $"{evidence.Values.Count(rows => rows.Count > 0 && rows.All(x => x.Result == ResultState.Pass && !string.IsNullOrWhiteSpace(x.Evidence)))} / {Checkoffs.Count} VPNs validated with recorded evidence";
    public int PskLength { get; set; } = 32;
    public bool EasyPsk { get; set; }
    public bool RevealPsk { get => revealPsk; set { Set(ref revealPsk, value); Raise(nameof(DisplayedPsk)); } }
    public string DisplayedPsk => RevealPsk ? generatedPsk : (generatedPsk.Length == 0 ? "Generate a one-time PSK" : new string('•', generatedPsk.Length));
    public CertificateInfo? SelectedCertificate { get; set; }

    public async Task InitializeAsync()
    {
        var settingsPath = Path.Combine(dataRoot, "settings.json");
        if (File.Exists(settingsPath))
        {
            try { settings = JsonSerializer.Deserialize<UserSettings>(await File.ReadAllTextAsync(settingsPath)) ?? new(); }
            catch (JsonException) { StatusText = "Settings could not be read. Defaults loaded; the original file remains until you save settings."; }
        }
        UpdateNavigation();
        Topology = settings.Topology ?? LabTopology.Create(Math.Clamp(GroupNumber, 0, 255));
        foreach (var provider in providers) capabilities[provider.Id] = await provider.GetCapabilitiesAsync();
        foreach (var profile in await store.LoadAllAsync()) Profiles.Add(profile);

        if (Profiles.Count == 0)
        {
            var initialProfiles = LabMode ? LabPresets.CreateClientProfiles(Topology) : [new VpnProfile { Name = "My VPN", IsLab = false }];
            foreach (var profile in initialProfiles) { Profiles.Add(profile); await store.SaveAsync(profile); }
        }
        SelectedProfile = Profiles.FirstOrDefault();
        foreach (var warning in store.LoadWarnings) AppendLog(new(DateTimeOffset.Now, LogSeverity.Warning, "app", VpnStage.Initialization, warning));
        BuildCheckoffs(); await LoadEvidenceAsync();
        if (LabMode)
        {
            ServerOptions.ExternalAddress = Topology.VyosExternal.Split('/')[0];
            ServerOptions.LocalNetwork = Topology.VyosHq; ServerOptions.RemoteNetwork = Topology.VyosRemote;
        }
        await DetectDependenciesAsync();
        await RefreshCertificatesAsync();
        CleanupOldLogs();
        AppendLog(new(DateTimeOffset.Now, LogSeverity.Information, "app", VpnStage.Initialization, "Workspace loaded. Secrets are not persisted unless explicitly remembered."));
        Raise(nameof(DeveloperMode)); Raise(nameof(LabMode)); Raise(nameof(ModeLabel)); Raise(nameof(GroupNumber)); Raise(nameof(LogRetentionDays));
        if (!smoke) statusTimer.Start();
    }
    private void RefreshProviderChoices()
    {
        ProviderChoices.Clear();
        foreach (var cap in capabilities.Values.Where(x => x.Protocols.Contains(Protocol) && (DeveloperMode || x.Id != "mock")).OrderByDescending(x => Dependencies.Any(d => d.Id == x.Id && d.Installed)).ThenByDescending(x => x.CanConnect)) ProviderChoices.Add(new(cap.Id, cap.DisplayName));
        if (SelectedProfile is not null && !ProviderChoices.Any(x => x.Id == SelectedProfile.ProviderId)) SelectedProfile.ProviderId = ProviderChoices.FirstOrDefault()?.Id ?? "native";
        Raise(nameof(ProviderId)); RefreshAuthenticationChoices();
    }
    private void RefreshAuthenticationChoices()
    {
        AuthenticationChoices.Clear();
        if (capabilities.TryGetValue(ProviderId, out var caps))
        {
            AuthenticationMode[] applicable = Protocol switch
            {
                VpnProtocol.WireGuard => [AuthenticationMode.PreSharedKey, AuthenticationMode.ProviderDefault],
                VpnProtocol.L2tpIpsec => [AuthenticationMode.PskAndUsername],
                VpnProtocol.Sstp => [AuthenticationMode.UsernamePassword],
                VpnProtocol.Ikev2 when ProviderId == "native" => [AuthenticationMode.ProviderDefault],
                VpnProtocol.Ikev2 => [AuthenticationMode.ProviderDefault, AuthenticationMode.UsernamePassword, AuthenticationMode.Certificate],
                VpnProtocol.OpenVpn => [AuthenticationMode.ProviderDefault, AuthenticationMode.UsernamePassword, AuthenticationMode.Certificate, AuthenticationMode.CertificateAndUsername],
                _ => [AuthenticationMode.ProviderDefault, AuthenticationMode.PskAndUsername, AuthenticationMode.CertificateAndUsername]
            };
            var modes = applicable.Where(caps.AuthenticationModes.Contains);
            foreach (var mode in modes) AuthenticationChoices.Add(mode);
            if (SelectedProfile is not null && !AuthenticationChoices.Contains(SelectedProfile.Authentication)) SelectedProfile.Authentication = AuthenticationChoices.FirstOrDefault();
        }
        Raise(nameof(Authentication)); Raise(nameof(EngineName)); Raise(nameof(EngineIntegration)); Raise(nameof(EngineCapabilities)); Raise(nameof(EngineLimitations));
    }
    private void UpdateNavigation()
    {
        string[] pages = LabMode ? ["Dashboard", "Connections", "Lab 2", "Server Config", "Diagnostics", "Check-Off", "Dependencies", "Settings", "About"] : ["Dashboard", "Connections", "Server Config", "Diagnostics", "Dependencies", "Settings", "About"];
        Navigation.Clear(); foreach (var page in pages) Navigation.Add(page);
        if (!Navigation.Contains(SelectedPage)) SelectedPage = "Dashboard";
    }
    private void InvalidateObservation()
    {
        observationRevision++; snapshot = null; Routes.Clear(); Findings.Clear(); history.Clear(); RefreshLogText();
        connection = new(VpnState.Unknown, "Configuration changed; refresh diagnostics to measure this engine."); ConnectionText = "UNKNOWN";
        DiagnosticText = "Run diagnostics for the current connection settings.";
    }
    private void RaiseProfileProperties()
    {
        foreach (var name in new[] { nameof(Protocol), nameof(ProviderId), nameof(Authentication), nameof(NetworksText), nameof(ForbiddenText), nameof(SelectedPolicy), nameof(ConnectionName), nameof(EngineName), nameof(EngineIntegration), nameof(EngineCapabilities), nameof(EngineLimitations), nameof(TargetNetworks), nameof(TunnelPolicy), nameof(TunnelIp), nameof(TunnelAdapter), nameof(ConnectionMessage), nameof(RouteSummary), nameof(ProviderDetails), nameof(ConnectedDuration), nameof(CanConfigure), nameof(HasActiveConnection) }) Raise(name);
        RaisePresentationProperties();
    }
    private IVpnProvider Provider => providers.First(x => x.Id == ProviderId && (DeveloperMode || x.Id != "mock"));
    private VpnProfile RequireProfile() => SelectedProfile ?? throw new InvalidOperationException("Select or create a connection first.");
    private void RegisterSecrets()
    {
        foreach (var value in new[] { Secrets.Password, Secrets.Psk, Secrets.PrivateKey, Secrets.RadiusSecret }) if (!string.IsNullOrWhiteSpace(value)) redactor.RegisterSecret(value);
    }
    public async Task ConnectAsync()
    {
        if (IsBusy) return;
        if (HasActiveConnection) { StatusText = "This tunnel is already active. Disconnect before reconnecting."; return; }
        var profile = RequireProfile();
        var errors = ProfileValidator.Validate(profile).Concat(Provider.ValidateProfile(profile)).Where(x => x.IsError).ToList();
        if (errors.Count > 0) { StatusText = string.Join(Environment.NewLine, errors.Select(x => $"{x.Field}: {x.Message}")); return; }
        IsBusy = true;
        try
        {
            observationRevision++; history.Clear(); RefreshLogText(); snapshot = null; Routes.Clear(); Findings.Clear();
            RegisterSecrets(); ConnectionText = "CONNECTING"; StatusText = "Connecting through " + EngineName + "…";
            await Provider.StartLogStreamAsync(profile);
            var engine = Provider;
            var result = await engine.ConnectAsync(profile, Secrets);
            connection = result.Status ?? await Provider.GetStatusAsync(profile);
            ConnectionText = connection.State.ToString().ToUpperInvariant(); StatusText = result.Message; RaiseProfileProperties();
            if (RememberSecrets && !smoke) await secretStore.SaveAsync(profile.Id, Secrets);
            if (!result.Success) await CollectDiagnosticsCoreAsync();
        }
        finally { IsBusy = false; }
    }
    public async Task DisconnectAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try { observationRevision++; var result = await Provider.DisconnectAsync(RequireProfile()); connection = result.Status ?? await Provider.GetStatusAsync(RequireProfile()); ConnectionText = connection.State.ToString().ToUpperInvariant(); StatusText = result.Message; snapshot = null; Routes.Clear(); Findings.Clear(); await Provider.StopLogStreamAsync(); RaiseProfileProperties(); }
        finally { IsBusy = false; }
    }
    private async Task RefreshStatusAsync()
    {
        if (statusPolling || IsBusy || SelectedProfile is null) return;
        statusPolling = true;
        var profile = SelectedProfile; var providerId = ProviderId; var revision = observationRevision;
        try
        {
            var current = await Provider.GetStatusAsync(profile);
            if (SelectedProfile?.Id != profile.Id || ProviderId != providerId || IsBusy || revision != observationRevision) return;
            connection = current; ConnectionText = connection.State.ToString().ToUpperInvariant(); RaiseProfileProperties();
        }
        catch (Exception error) { if (SelectedProfile?.Id == profile.Id && ProviderId == providerId) AppendLog(new(DateTimeOffset.Now, LogSeverity.Warning, "app", VpnStage.Tunnel, redactor.Redact(error.Message))); }
        finally { statusPolling = false; }
    }
    public async Task CollectDiagnosticsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try { StatusText = "Collecting current adapter, route, engine, and event evidence…"; await CollectDiagnosticsCoreAsync(); StatusText = "Diagnostics collected. Review observed findings before sharing."; }
        finally { IsBusy = false; }
    }
    private async Task CollectDiagnosticsCoreAsync()
    {
        RegisterSecrets();
        snapshot = smoke ? CreateSmokeSnapshot() : await new DiagnosticsService(redactor).CollectAsync(RequireProfile(), Provider, history.ToArray(), PastedServerOutput);
        snapshot.Dependencies = Dependencies.ToList();
        DiagnosticText = new DiagnosticFormatter(redactor).Format(snapshot);
        Routes.Clear(); foreach (var route in snapshot.Routes) Routes.Add(route);
        Findings.Clear(); foreach (var finding in snapshot.Findings) Findings.Add(finding);
        connection = snapshot.Status; ConnectionText = connection.State.ToString().ToUpperInvariant();
        RaiseProfileProperties(); Raise(nameof(LocalAdapter));
    }
    public async Task CopyDiagnosticsAsync() { if (snapshot is null) await CollectDiagnosticsAsync(); if (snapshot is not null) await CopyAsync(DiagnosticText, "Redacted diagnostic report copied for ChatGPT. Review network addresses before sharing."); }
    private Task CopyAsync(string value, string message) { RegisterSecrets(); Clipboard.SetText(redactor.Redact(value)); StatusText = message; return Task.CompletedTask; }
    private async Task SaveProfileAsync()
    {
        var profile = RequireProfile();
        var issues = ProfileValidator.Validate(profile);
        if (issues.Any(x => x.IsError)) { StatusText = string.Join("\n", issues.Select(x => x.Message)); return; }
        RegisterSecrets(); await store.SaveAsync(profile);
        if (!smoke) { if (RememberSecrets) await secretStore.SaveAsync(profile.Id, Secrets); else await secretStore.DeleteAsync(profile.Id); }
        RaiseProfileProperties(); Raise(nameof(EditorRevision));
        StatusText = RememberSecrets ? "Profile saved. Secrets protected for this Windows account." : "Profile saved without secrets.";
    }
    private Task NewProfileAsync() { EnsureCanConfigure(); var p = new VpnProfile { Name = "New VPN connection", IsLab = LabMode, GroupNumber = GroupNumber }; Profiles.Add(p); SelectedProfile = p; SelectedPage = "Connections"; return Task.CompletedTask; }
    private Task DuplicateProfileAsync() { EnsureCanConfigure(); var p = RequireProfile() with { Id = Guid.NewGuid(), Name = RequireProfile().Name + " copy", PermittedNetworks = [.. RequireProfile().PermittedNetworks], ForbiddenNetworks = [.. RequireProfile().ForbiddenNetworks] }; Profiles.Add(p); SelectedProfile = p; StatusText = "Duplicated non-secret settings. Save to keep this profile."; return Task.CompletedTask; }
    private async Task DeleteProfileAsync()
    {
        EnsureCanConfigure(); var p = RequireProfile();
        if (MessageBox.Show($"Delete the saved profile '{p.Name}' and its remembered secrets?", "Delete profile", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await store.DeleteAsync(p.Id); await secretStore.DeleteAsync(p.Id); Profiles.Remove(p); SelectedProfile = Profiles.FirstOrDefault(); StatusText = "Profile deleted.";
    }
    private async Task ResetProfileAsync()
    {
        EnsureCanConfigure(); var existing = (await store.LoadAllAsync()).FirstOrDefault(x => x.Id == RequireProfile().Id);
        if (existing is null) { StatusText = "This profile has not been saved yet."; return; }
        var index = Profiles.IndexOf(RequireProfile()); Profiles[index] = existing; SelectedProfile = existing; StatusText = "Reloaded saved non-secret settings.";
    }
    private Task ValidateAsync() { var p = RequireProfile(); var issues = ProfileValidator.Validate(p).Concat(Provider.ValidateProfile(p)).ToList(); StatusText = issues.Count == 0 ? "Profile validation passed. Connectivity must still be measured." : string.Join("\n", issues.Select(x => $"{(x.IsError ? "ERROR" : "WARNING")} · {x.Field}: {x.Message}")); return Task.CompletedTask; }
    private async Task ImportProfileAsync() { EnsureCanConfigure(); var dialog = new OpenFileDialog { Filter = "VPN profile (*.json)|*.json" }; if (dialog.ShowDialog() != true) return; var p = await store.ImportAsync(dialog.FileName); var existing = Profiles.FirstOrDefault(x => x.Id == p.Id); if (existing is not null) Profiles.Remove(existing); Profiles.Add(p); SelectedProfile = p; StatusText = "Profile imported; secrets must be supplied separately."; }
    private async Task ExportProfileAsync() { var dialog = new SaveFileDialog { Filter = "VPN profile (*.json)|*.json", FileName = "vpn-profile.json" }; if (dialog.ShowDialog() == true) { await store.ExportAsync(RequireProfile(), dialog.FileName); StatusText = "Ordinary profile exported without secret fields."; } }
    private Task ImportConfigAsync() { var dialog = new OpenFileDialog { Filter = "VPN configuration|*.ovpn;*.conf;*.vpn|All files|*.*" }; if (dialog.ShowDialog() == true) { RequireProfile().ImportedConfigPath = dialog.FileName; Raise(nameof(EditorRevision)); StatusText = "Configuration linked in place. Its contents are not copied into profile JSON or diagnostics. Review any embedded secrets in that file."; } return Task.CompletedTask; }
    private async Task GenerateKeysAsync() { var p = RequireProfile(); var keys = await ((WireGuardProvider)providers.First(x => x.Id == "wireguard")).GenerateKeyPairAsync(); p.PublicKey = keys.PublicKey; Secrets.PrivateKey = keys.PrivateKey; RegisterSecrets(); Raise(nameof(EditorRevision)); StatusText = "WireGuard keypair generated with the installed provider. Private key is held only in memory."; }
    private async Task ExportWireGuardAsync()
    {
        var dialog = new SaveFileDialog { Filter = "WireGuard configuration (*.conf)|*.conf", FileName = "tunnel.conf" };
        if (dialog.ShowDialog() != true) return;
        bool include = MessageBox.Show("Include the private key and PSK in this explicit WireGuard configuration export? The file will contain secrets. Choose No for a template with placeholders.", "Export WireGuard configuration", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) switch { MessageBoxResult.Yes => true, MessageBoxResult.No => false, _ => throw new OperationCanceledException("Export cancelled.") };
        var config = WireGuardConfigGenerator.Generate(RequireProfile(), Secrets, include);
        if (include) await SavePrivateExportAsync(dialog.FileName, config); else await File.WriteAllTextAsync(dialog.FileName, config); StatusText = include ? "Secret-bearing WireGuard configuration exported. Protect the file." : "WireGuard template exported with placeholders.";
    }
    public async Task LoadRememberedSecretsAsync() { var saved = await secretStore.LoadAsync(RequireProfile().Id); if (saved is null) { StatusText = "No secrets stored for this profile and Windows account."; return; } Secrets.Clear(); Secrets = saved; RememberSecrets = true; RegisterSecrets(); Raise(nameof(EditorRevision)); StatusText = "Remembered secrets loaded into this session."; }
    private async Task ForgetSecretsAsync() { await secretStore.DeleteAsync(RequireProfile().Id); Secrets.Clear(); RememberSecrets = false; Raise(nameof(EditorRevision)); StatusText = "Remembered and current secrets removed."; }
    private async Task ApplyGroupAsync()
    {
        if (GroupNumber is < 0 or > 255) throw new ArgumentException("Group number must be between 0 and 255.");
        Topology = LabTopology.Create(GroupNumber); settings.Topology = Topology; RefreshAccessRules(); BuildCheckoffs();
        await SaveSettingsAsync(); StatusText = "Topology derived from the group number. Use Add lab profiles to create connections for this topology; existing profiles remain editable.";
    }
    private async Task SeedLabAsync() { EnsureCanConfigure(); ValidateTopology(); foreach (var p in LabPresets.CreateClientProfiles(Topology)) { Profiles.Add(p); await store.SaveAsync(p); } SelectedProfile = Profiles.LastOrDefault(); StatusText = "Four client profiles added from the current topology."; }
    private void RefreshAccessRules() { AccessRules.Clear(); if (SelectedProfile is not null) foreach (var rule in LabPresets.AccessMatrix(SelectedProfile, Topology)) AccessRules.Add(rule); }
    private void BuildCheckoffs()
    {
        Checkoffs.Clear(); foreach (var item in CheckoffCatalog.Create(Topology, SelectedPolicy))
        {
            Checkoffs.Add(item);
            evidence.TryGetValue(item.Id, out var previous);
            evidence[item.Id] = new(item.Items.Select(x => previous?.FirstOrDefault(r => r.Id == x.Id && r.Text == x.Text) ?? new CheckoffRow(x)));
        }
        if (!Checkoffs.Any(x => x.Id == SelectedCheckoff)) SelectedCheckoff = Checkoffs.FirstOrDefault()?.Id ?? ""; else RefreshCheckoffRows(); Raise(nameof(CheckoffSummary));
    }
    private void RefreshCheckoffRows() { CheckoffRows.Clear(); if (evidence.TryGetValue(SelectedCheckoff, out var rows)) foreach (var row in rows) { row.PropertyChanged -= EvidenceChanged; row.PropertyChanged += EvidenceChanged; CheckoffRows.Add(row); } }
    private async void EvidenceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) { Raise(nameof(CheckoffSummary)); try { await SaveEvidenceAsync(); } catch (Exception error) { ReportError(error); } }
    private async Task SaveEvidenceAsync()
    {
        await evidenceGate.WaitAsync();
        try
        {
            RegisterSecrets(); var records = evidence.ToDictionary(x => x.Key, x => x.Value.Select(r => r.ToItem() with { Evidence = redactor.Redact(r.Evidence) }).ToList());
            var path = Path.Combine(dataRoot, "checkoff.json");
            await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(records, json)); File.Move(path + ".tmp", path, true);
        }
        finally { evidenceGate.Release(); }
    }
    private async Task LoadEvidenceAsync() { var path = Path.Combine(dataRoot, "checkoff.json"); if (!File.Exists(path)) return; var data = JsonSerializer.Deserialize<Dictionary<string, List<CheckoffItem>>>(await File.ReadAllTextAsync(path)); if (data is null) return; foreach (var pair in data) if (evidence.ContainsKey(pair.Key)) foreach (var item in pair.Value) { var row = evidence[pair.Key].FirstOrDefault(x => x.Id == item.Id); if (row is not null && row.Text == item.Text) { row.Result = item.Result; row.Evidence = item.Evidence; } } RefreshCheckoffRows(); Raise(nameof(CheckoffSummary)); }
    private async Task ExportEvidenceAsync() { if (snapshot is null) await CollectDiagnosticsAsync(); if (snapshot is null) return; var dialog = new SaveFileDialog { Filter = "Evidence bundle (*.zip)|*.zip", FileName = $"Windows-VPN-Evidence-{DateTime.Now:yyyyMMdd-HHmmss}.zip" }; if (dialog.ShowDialog() != true) return; RegisterSecrets(); await new EvidenceExporter(redactor).ExportAsync(dialog.FileName, snapshot, evidence.Values.SelectMany(x => x).Select(x => x.ToItem())); StatusText = "Redacted evidence bundle exported. Manual PASS entries represent your own recorded observations."; }
    private async Task SaveSettingsAsync() { if (LogRetentionDays is < 1 or > 365) throw new ArgumentException("Log retention must be 1–365 days."); ValidateTopology(); settings.Topology = Topology; BuildCheckoffs(); await File.WriteAllTextAsync(Path.Combine(dataRoot, "settings.json"), JsonSerializer.Serialize(settings, json)); RefreshAccessRules(); Raise(nameof(Topology)); StatusText = "Workspace settings saved."; }
    private async Task DetectDependenciesAsync()
    {
        var selectedId = SelectedDependency?.Id;
        Dependencies.Clear();
        foreach (var provider in providers.Where(x => x.Id != "mock" || DeveloperMode)) { try { Dependencies.Add(await provider.DetectInstallation()); } catch (Exception error) { Dependencies.Add(new(provider.Id, provider.Id, false, null, null, redactor.Redact(error.Message))); } }
        var shellPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        Dependencies.Add(new("powershell", "Windows PowerShell", File.Exists(shellPath), shellPath, null, "Used for Windows route, adapter, event, and VPN profile management."));
        var admin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        Dependencies.Add(new("administrator", "Administrator privileges", admin, null, null, admin ? "This process is elevated." : "Running as your normal account. WireGuard tunnel installation and OpenVPN adapter operations may require an administrator-run session."));
        SelectedDependency = Dependencies.FirstOrDefault(d => d.Id == selectedId) ?? Dependencies.FirstOrDefault(d => d.Id == ProviderId) ?? Dependencies.FirstOrDefault();
        RefreshProviderChoices(); RaisePresentationProperties(); StatusText = "Engine check complete. Nothing was installed or changed.";
    }
    private Task GenerateServerAsync() { RefreshServerChecklist(); var result = ServerConfigGenerator.Generate(ServerOptions, Topology); ServerText = result.Title + "\n\n" + string.Join("\n", result.Warnings.Select(x => "NOTE: " + x)) + "\n\n" + result.Text; StatusText = result.Supported ? "Configuration generated. Review topology, version, and secret placeholders before applying." : "This template has a documented limitation. Review the generated guidance."; return Task.CompletedTask; }
    private async Task SaveServerAsync() { var dialog = new SaveFileDialog { Filter = "Text file (*.txt)|*.txt", FileName = "server-configuration.txt" }; if (dialog.ShowDialog() == true) { await File.WriteAllTextAsync(dialog.FileName, ServerText); StatusText = "Configuration worksheet saved with secret placeholders."; } }
    private Task GeneratePskAsync() { generatedPsk = PskGenerator.Generate(PskLength, EasyPsk); redactor.RegisterSecret(generatedPsk); RevealPsk = false; Raise(nameof(DisplayedPsk)); StatusText = "PSK generated in memory. It is never automatically saved or placed in diagnostics."; return Task.CompletedTask; }
    public Task UseGeneratedPskAsync() { if (generatedPsk.Length == 0) throw new InvalidOperationException("Generate a PSK first."); if (Protocol == VpnProtocol.WireGuard) throw new InvalidOperationException("Use Generate WireGuard PSK on the Connections page; WireGuard requires a 32-byte Base64 key."); Secrets.Psk = generatedPsk; RegisterSecrets(); Raise(nameof(EditorRevision)); StatusText = "Generated PSK assigned to the selected connection for this session."; return Task.CompletedTask; }
    private Task RefreshCertificatesAsync() { Certificates.Clear(); foreach (var cert in certificateService.ListCertificates()) Certificates.Add(cert); return Task.CompletedTask; }
    private async Task ImportCertificateAsync() { var dialog = new OpenFileDialog { Filter = "Public certificate (*.cer, *.crt)|*.cer;*.crt" }; if (dialog.ShowDialog() != true) return; var cert = certificateService.ImportCertificate(dialog.FileName); await RefreshCertificatesAsync(); StatusText = $"Public certificate imported: {cert.Subject}. Private-key containers must be installed through Windows Certificate Manager."; }
    public Task SelectCertificateAsync() { if (SelectedCertificate is null) throw new InvalidOperationException("Select a certificate from the table first."); RequireProfile().CertificateThumbprint = SelectedCertificate.Thumbprint; Raise(nameof(EditorRevision)); StatusText = "Certificate thumbprint selected. The provider must support this authentication mode."; return Task.CompletedTask; }
    public Task OpenOfficialDownloadAsync(DependencyInfo dependency) { if (Uri.TryCreate(dependency.DownloadUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https") Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); return Task.CompletedTask; }
    public Task CopyInstallCommandAsync(DependencyInfo dependency) { if (string.IsNullOrWhiteSpace(dependency.InstallCommand)) { StatusText = "No verified official install command is available for this engine. Use its official download page."; return Task.CompletedTask; } return CopyAsync(dependency.InstallCommand, "Official package install command copied. Review it before running."); }
    public ICommand MakeCommand(Func<Task> action) => Command(action);
    private void AppendLog(VpnLogEvent item)
    {
        RegisterSecrets(); var safe = item with { Message = redactor.Redact(item.Message) }; history.Add(safe); if (history.Count > 2500) history.RemoveRange(0, history.Count - 2500);
        if (!LogsPaused) RefreshLogText();
        if (smoke) return;
        try { var directory = Path.Combine(dataRoot, "Logs"); Directory.CreateDirectory(directory); File.AppendAllText(Path.Combine(directory, $"{DateTime.Now:yyyy-MM-dd}.log"), FormatLog(safe) + Environment.NewLine); } catch (IOException) { }
    }
    private static string FormatLog(VpnLogEvent item) => $"{item.Timestamp:HH:mm:ss}  {item.Severity.ToString().ToUpperInvariant(),-11} {item.Provider,-10} {item.Stage,-17} {item.Message}";
    private string FormatLogs() { RegisterSecrets(); return redactor.Redact(string.Join(Environment.NewLine, history.Select(FormatLog))); }
    private void RefreshLogText()
    {
        RegisterSecrets();
        LogText = redactor.Redact(string.Join(Environment.NewLine, history.Where(x => (LogFilter == "All" || x.Severity.ToString() == LogFilter) && (string.IsNullOrWhiteSpace(LogSearch) || FormatLog(x).Contains(LogSearch, StringComparison.OrdinalIgnoreCase))).Select(FormatLog)));
    }
    private async Task SaveLogsAsync() { var dialog = new SaveFileDialog { Filter = "Redacted log (*.txt)|*.txt", FileName = "vpn-log.txt" }; if (dialog.ShowDialog() == true) { await File.WriteAllTextAsync(dialog.FileName, FormatLogs()); StatusText = "Redacted logs saved."; } }
    private Task ClearLogsAsync() { history.Clear(); LogText = ""; StatusText = "Session log display cleared."; return Task.CompletedTask; }
    public Task DeleteStoredLogsAsync() { var directory = Path.Combine(dataRoot, "Logs"); if (Directory.Exists(directory)) foreach (var path in Directory.EnumerateFiles(directory, "*.log")) File.Delete(path); StatusText = "Stored application logs cleared."; return Task.CompletedTask; }
    private void CleanupOldLogs() { var directory = Path.Combine(dataRoot, "Logs"); if (!Directory.Exists(directory)) return; foreach (var path in Directory.EnumerateFiles(directory, "*.log")) if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddDays(-Math.Clamp(LogRetentionDays, 1, 365))) File.Delete(path); }
    private static List<string> ParseLines(string value) => value.Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    public void ReportError(Exception error) { RegisterSecrets(); StatusText = error is OperationCanceledException ? "Action cancelled." : $"{error.GetType().Name}: {error.Message}"; AppendLog(new(DateTimeOffset.Now, LogSeverity.Error, "app", VpnStage.Initialization, StatusText)); IsBusy = false; }
    public async Task ShutdownAsync()
    {
        if (shutdownComplete) return; shutdownComplete = true; statusTimer.Stop();
        foreach (var provider in providers) { try { if (provider is OpenVpnProvider openVpn) await openVpn.ShutdownAsync(); await provider.StopLogStreamAsync(); } catch (Exception error) { ReportError(error); } }
        await SaveEvidenceAsync(); Secrets.Clear(); generatedPsk = "";
        if (smoke && Directory.Exists(dataRoot)) Directory.Delete(dataRoot, true);
    }
}
