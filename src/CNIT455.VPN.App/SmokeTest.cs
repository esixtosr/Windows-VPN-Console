using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using CNIT455.VPN.Core;
using CNIT455.VPN.ConfigGenerators;
using CNIT455.VPN.Diagnostics;
using CNIT455.VPN.Providers;

namespace CNIT455.VPN.App;

public sealed partial class MainViewModel
{
    private DiagnosticSnapshot CreateSmokeSnapshot()
    {
        var profile = RequireProfile();
        var observed = new DiagnosticSnapshot
        {
            Profile = profile with { PermittedNetworks = [.. profile.PermittedNetworks], ForbiddenNetworks = [.. profile.ForbiddenNetworks] },
            Status = connection, Logs = history.ToList(), ServerOutput = redactor.Redact(PastedServerOutput),
            CollectionNotes = "SIMULATION ONLY: deterministic fixture. No network probe, operating-system route change, or real VPN operation occurred.",
            Adapters = [new(9, "Mock adapter", "Simulated adapter", "Up", ["10.254.0.10"], ["10.254.0.1"], []), new(4, "Mock physical", "Simulated local adapter", "Up", ["192.0.2.10"], [], ["192.0.2.254"])],
            Routes = [new("0.0.0.0/0", "192.0.2.254", 4, "Mock physical", 10, 10)]
        };
        if (connection.State == VpnState.Connected)
        {
            if (profile.TunnelMode == TunnelMode.Full)
            {
                observed.Routes.Add(new("0.0.0.0/1", "0.0.0.0", 9, "Mock adapter", 1, 1, true));
                observed.Routes.Add(new("128.0.0.0/1", "0.0.0.0", 9, "Mock adapter", 1, 1, true));
            }
            else foreach (var network in profile.PermittedNetworks) observed.Routes.Add(new(network, "0.0.0.0", 9, "Mock adapter", 1, 1, true));
        }
        observed.Routing = RouteAnalyzer.Analyze(profile, observed.Routes, connection.State == VpnState.Connected ? 9 : null);
        observed.Findings = TroubleshootingAnalyzer.Analyze(observed).ToList();
        return observed;
    }
    public async Task<object> RunSmokeAsync(MainWindow window, string reportPath)
    {
        statusTimer.Stop(); DeveloperMode = true;
        ((MockVpnProvider)providers.Single(x => x.Id == "mock")).StageDelay = TimeSpan.FromMilliseconds(10);
        var output = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        var screenDirectory = Path.Combine(output, "screenshots"); Directory.CreateDirectory(screenDirectory);
        var visited = new List<string>(); var assertions = new List<string>();
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Smoke assertion failed: " + message); assertions.Add(message); }
        async Task Drain() => await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        foreach (var page in Navigation.ToArray())
        {
            SelectedPage = page; await window.CapturePageAsync(Path.Combine(screenDirectory, page.Replace(' ', '-').ToLowerInvariant() + ".png")); visited.Add(page);
        }
        Check(visited.Count == 9, "All nine pages instantiated and captured");
        GroupNumber = 41; await ApplyGroupAsync();
        Check(Topology.PublicNetwork == "44.104.41.0/24" && Topology.VyosDmz == "172.18.41.0/24", "Group number derives public and DMZ networks");
        var simulation = new VpnProfile { Name = "CI simulated connection", Protocol = VpnProtocol.L2tpIpsec, ProviderId = "mock", Gateway = "192.0.2.1", Username = "simulation", IsLab = false, PermittedNetworks = ["198.51.100.0/24"] };
        Profiles.Add(simulation); SelectedProfile = simulation;
        Secrets.Psk = "SmokeSecret-NotForRealVpn"; Secrets.Password = "SmokePassword-NotForRealVpn";
        await SaveProfileAsync();
        var serialized = await File.ReadAllTextAsync(Path.Combine(dataRoot, "Profiles", simulation.Id.ToString() + ".json"));
        Check(!serialized.Contains(Secrets.Psk) && !serialized.Contains(Secrets.Password), "Profile persistence excludes secrets");
        SelectedPage = "Dashboard"; await ConnectAsync(); await Drain();
        Check(connection.State == VpnState.Connected, "Mock connects with simulated stages");
        await CollectDiagnosticsAsync();
        Check(snapshot?.Routing.Result == ResultState.Pass, "Simulated split route fixture passes route analysis");
        PastedServerOutput = "password=Test123\nPSK=SmokeSecret-NotForRealVpn\nshow route: simulated";
        await CopyDiagnosticsAsync(); var copied = Clipboard.GetText();
        Check(!copied.Contains(Secrets.Psk) && !copied.Contains(Secrets.Password) && !copied.Contains("Test123") && copied.Contains("show route: simulated"), "Clipboard redacts secrets and includes latest pasted output");
        await window.CapturePageAsync(Path.Combine(screenDirectory, "mock-connected.png"));
        var evidencePath = Path.Combine(output, "smoke-evidence.zip");
        await new EvidenceExporter(redactor).ExportAsync(evidencePath, snapshot!, evidence.Values.SelectMany(x => x).Select(x => x.ToItem()));
        using (var zip = ZipFile.OpenRead(evidencePath))
        {
            Check(zip.Entries.Count >= 8, "Evidence bundle contains required reports");
            foreach (var entry in zip.Entries)
            {
                using var reader = new StreamReader(entry.Open()); var value = await reader.ReadToEndAsync();
                Check(!value.Contains(Secrets.Psk) && !value.Contains(Secrets.Password) && !value.Contains("Test123"), "Evidence redacted: " + entry.Name);
            }
        }
        await DisconnectAsync(); Check(connection.State == VpnState.Disconnected, "Mock disconnects");
        foreach (var failure in Enum.GetValues<MockFailure>().Where(x => x != MockFailure.None))
        {
            simulation.MockFailure = failure; await ConnectAsync(); await Drain();
            Check(connection.State == VpnState.Failed, "Mock failure stage: " + failure);
        }
        SelectedPage = "Diagnostics"; await CollectDiagnosticsAsync(); await window.CapturePageAsync(Path.Combine(screenDirectory, "mock-failure-diagnostics.png"));
        simulation.MockFailure = MockFailure.None; await DisconnectAsync();
        foreach (var protocol in Enum.GetValues<VpnProtocol>())
        {
            Protocol = protocol; ProviderId = "mock"; Advanced = true; SelectedPage = "Connections"; await Drain();
            await window.CapturePageAsync(Path.Combine(screenDirectory, "connection-" + protocol.ToString().ToLowerInvariant() + ".png"));
        }
        Protocol = VpnProtocol.WireGuard; simulation.PeerPublicKey = Convert.ToBase64String(new byte[32]); simulation.TunnelAddress = "10.254.0.10/24";
        await GenerateWireGuardPskAsync(); Check(Convert.FromBase64String(Secrets.Psk).Length == 32, "WireGuard PSK uses 32 random bytes");
        await PreviewClientConfigAsync(); Check(ClientConfigText.Contains("<PRIVATE_KEY>") && ClientConfigText.Contains("AllowedIPs"), "WireGuard template uses placeholders");
        Protocol = VpnProtocol.OpenVpn; Authentication = AuthenticationMode.CertificateAndUsername; await PreviewClientConfigAsync(); Check(ClientConfigText.Contains("remote-cert-tls server"), "OpenVPN template verifies server certificates");
        Protocol = VpnProtocol.IpsecMobile; SelectedPolicy = LabPolicy.Extended;
        Check(simulation.PermittedNetworks.Contains(Topology.VyosHq) && !simulation.ForbiddenNetworks.Contains(Topology.VyosHq), "Extended policy removes HQ from forbidden routes");
        SelectedPolicy = LabPolicy.Strict; Check(!simulation.PermittedNetworks.Contains(Topology.VyosHq), "Strict policy excludes HQ");
        ServerOptions.Template = ServerTemplate.LegacyIpsecMobile; await GenerateServerAsync(); Check(ServerText.Contains("XAUTH"), "Legacy mobile IPsec limitation is explicit");
        ServerOptions.Template = ServerTemplate.PfSenseIpsec; await GenerateServerAsync(); Check(ServerChecklist.Count > 0, "pfSense helper exposes checkable steps");
        PskLength = 48; await GeneratePskAsync(); Check(generatedPsk.Length == 48 && !DisplayedPsk.Contains(generatedPsk), "PSK remains hidden until explicitly revealed");
        LabMode = false; Check(!Navigation.Contains("Lab 2") && !Navigation.Contains("Check-Off"), "Generic mode removes course pages");
        DeveloperMode = false; Check(ProviderId != "mock" && ProviderChoices.All(x => x.Id != "mock"), "Mock unavailable outside Developer Mode");
        LabMode = true; SelectedPage = "Dashboard";
        return new { success = true, pages = visited, assertions, screenshots = Directory.GetFiles(screenDirectory).Select(Path.GetFileName).Order().ToArray(), simulated = true, realNetworkVerified = false, diagnosticCharacters = copied.Length };
    }
}
