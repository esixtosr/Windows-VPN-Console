using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CNIT455.VPN.Core;
using CNIT455.VPN.Providers;
using CNIT455.VPN.ConfigGenerators;

namespace CNIT455.VPN.App;

internal static class ViewFactory
{
    internal static FrameworkElement Create(string page, MainViewModel vm) => page switch
    {
        "Dashboard" => Dashboard(vm), "Connections" => Connections(vm), "Lab 2" => Lab(vm), "Server Config" => Server(vm),
        "Diagnostics" => Diagnostics(vm), "Check-Off" => Checkoff(vm), "Dependencies" => Dependencies(vm), "Settings" => Settings(vm), _ => About(vm)
    };
    private static FrameworkElement ProfilePicker(MainViewModel vm)
    {
        var picker = Ui.Select("SelectedProfile", vm.Profiles, "Name"); picker.SetBinding(UIElement.IsEnabledProperty, Ui.Bind("CanConfigure", false));
        return Ui.Field("Active connection", picker);
    }
    private static FrameworkElement Dashboard(MainViewModel vm)
    {
        var disconnect = Ui.Button("Disconnect", vm.DisconnectCommand);
        disconnect.SetBinding(UIElement.VisibilityProperty, new Binding("ShowDirectDisconnect") { Converter = new BooleanToVisibilityConverter() });
        disconnect.SetBinding(UIElement.IsEnabledProperty, Ui.Bind("HasActiveConnection", false));
        var connection = Ui.Card("Your connection", ProfilePicker(vm),
            Ui.BoundText("EngineName", 15, "#5DE2C2", FontWeights.SemiBold), Ui.BoundText("EngineHelp", 13, "#B9CDDD"),
            Ui.Actions(Ui.BoundButton("ConnectLabel", vm.ConnectCommand, true), disconnect, Ui.Button("Check connection", vm.TestCommand),
                Ui.Button("Edit profile", vm.MakeCommand(() => { vm.SelectedPage = "Connections"; return Task.CompletedTask; }))),
            Ui.BoundText("SelectedEngineReadiness", 11, "#A8BBCC"));
        var metrics = Ui.Two(Ui.Card("Address & adapter", Ui.Text("OBSERVED IPV4 ADDRESS", 10, "#A8BBCC"), Ui.BoundText("TunnelIp", 26, "#F1F5F9", FontWeights.SemiBold),
                Ui.BoundText("TunnelAdapter", 13, "#D6E4EE"), Ui.BoundText("AddressSource", 12, "#A8BBCC")),
            Ui.Card("Network paths", Ui.BoundText("NetworkHeadline", 17, "#F1F5F9", FontWeights.SemiBold),
                Ui.BoundText("TunnelPolicy", 12, "#5DE2C2"), Ui.BoundText("TargetNetworks", 13), Ui.BoundText("NetworkHelp", 12, "#A8BBCC"),
                Ui.Text("INTERNET ROUTE USES", 10, "#A8BBCC"), Ui.BoundText("LocalAdapter", 13)));
        return Ui.Stack(connection, Ui.BoundText("EvidenceTime", 11, "#A8BBCC"), metrics,
            Ui.Disclosure("Connection details & troubleshooting", Ui.BoundText("ConnectionMessage"), Ui.BoundText("ProviderDetails", 12, "#BCD0DD"),
                Ui.Text("ENGINE-REPORTED DURATION", 10, "#A8BBCC"), Ui.BoundText("ConnectedDuration"),
                Ui.BoundText("EngineIntegration", 12, "#5DE2C2"), Ui.BoundText("EngineCapabilities", 12, "#BCD0DD"), Ui.BoundText("EngineLimitations", 12, "#DEBC80"),
                Ui.Actions(Ui.BoundButton("DisconnectLabel", vm.DisconnectCommand), Ui.Button("Copy diagnostic report", vm.CopyDiagnosticsCommand))),
            Ui.BoundText("SetupHint", 12, "#A8BBCC"));
    }
    private static FrameworkElement Connections(MainViewModel vm)
    {
        var root = Ui.Stack(Ui.Card("Saved profiles", ProfilePicker(vm), Ui.Actions(Ui.Button("Save profile", vm.SaveProfileCommand, true), Ui.Button("New profile", vm.NewProfileCommand), Ui.Button("Check settings", vm.ValidateCommand)),
            Ui.Disclosure("More profile actions", Ui.Actions(Ui.Button("Duplicate", vm.DuplicateProfileCommand), Ui.Button("Import profile", vm.ImportProfileCommand), Ui.Button("Export profile", vm.ExportProfileCommand), Ui.Button("Reset to saved", vm.ResetProfileCommand), Ui.Button("Delete profile", vm.DeleteProfileCommand)))));
        root.SetBinding(UIElement.IsEnabledProperty, Ui.Bind("CanConfigure", false));
        if (vm.SelectedProfile is null) { root.Children.Add(Ui.Note("Create a connection to begin.")); return root; }
        var baseFields = Ui.Card("Connection basics",
            Ui.Two(Ui.Field("Profile name", Ui.Input("SelectedProfile.Name")), Ui.Field("VPN type", Ui.Select("Protocol", Enum.GetValues<VpnProtocol>()))),
            Ui.Two(Ui.Field("VPN engine", Ui.Select("ProviderId", vm.ProviderChoices, "Name", "Id")), Ui.Field("Authentication", Ui.Select("Authentication", vm.AuthenticationChoices))),
            Ui.Two(Ui.Field(vm.Protocol == VpnProtocol.WireGuard ? "Peer endpoint / gateway" : "Gateway", Ui.Input("SelectedProfile.Gateway")), Ui.Field("Tunnel policy", Ui.Select("SelectedProfile.TunnelMode", Enum.GetValues<TunnelMode>()), "Split tunnel sends only permitted private routes through the VPN. Full tunnel sends Internet traffic through it; inspect IPv4 and IPv6 routes.")),
            Ui.BoundText("EngineHelp", 12, "#A8BBCC"), Ui.BoundText("SelectedEngineReadiness", 12, "#5DE2C2"),
            Ui.Disclosure("Engine capabilities & limitations", Ui.BoundText("EngineIntegration", 12), Ui.BoundText("EngineCapabilities", 12), Ui.BoundText("EngineLimitations", 12, "#DEBC80")));
        root.Children.Add(baseFields);
        var auth = new StackPanel();
        var mode = vm.Authentication;
        bool username = !vm.IsExternalClient && (mode is AuthenticationMode.PskAndUsername or AuthenticationMode.CertificateAndUsername or AuthenticationMode.UsernamePassword || vm.Protocol == VpnProtocol.OpenVpn && mode == AuthenticationMode.ProviderDefault);
        bool psk = !vm.IsExternalClient && (mode is AuthenticationMode.PskAndUsername or AuthenticationMode.PreSharedKey || vm.Protocol == VpnProtocol.WireGuard);
        bool certificate = !vm.IsExternalClient && (mode is AuthenticationMode.Certificate or AuthenticationMode.CertificateAndUsername);
        if (username)
        {
            auth.Children.Add(Ui.Two(Ui.Field(vm.Protocol == VpnProtocol.IpsecMobile ? "XAUTH username" : "Username", Ui.Input("SelectedProfile.Username"), "XAUTH adds user authentication to a legacy IKEv1 exchange. The server's authentication backend may be local, RADIUS, or LDAP."), Password(vm, "Password", () => vm.Secrets.Password, value => vm.Secrets.Password = value)));
            if (vm.Protocol is VpnProtocol.IpsecMobile or VpnProtocol.L2tpIpsec or VpnProtocol.OpenVpn) auth.Children.Add(Ui.Two(Ui.Field("Server authentication source", Ui.Select("SelectedProfile.AuthBackend", Enum.GetValues<AuthBackend>()), "Local testing should precede domain integration. RADIUS forwards authentication to a service such as NPS; LDAP queries a directory. This choice describes your server configuration."), Ui.Field("Domain (optional)", Ui.Input("SelectedProfile.Domain"))));
        }
        if (psk) auth.Children.Add(Password(vm, vm.Protocol == VpnProtocol.WireGuard ? "WireGuard pre-shared key (Base64)" : "Pre-shared key (PSK)", () => vm.Secrets.Psk, value => vm.Secrets.Psk = value, "A shared secret known by both peers. Never place it in profile notes or logs."));
        if (vm.Protocol == VpnProtocol.WireGuard)
        {
            auth.Children.Add(Password(vm, "Private key", () => vm.Secrets.PrivateKey, value => vm.Secrets.PrivateKey = value));
            auth.Children.Add(Ui.Actions(Ui.Button("Generate keypair", vm.GenerateKeysCommand), Ui.Button("Generate WireGuard PSK", vm.MakeCommand(vm.GenerateWireGuardPskAsync)), Ui.Button("Reveal private key", vm.MakeCommand(() => { MessageBox.Show(vm.Secrets.PrivateKey.Length == 0 ? "No private key is loaded." : vm.Secrets.PrivateKey, "Private key · do not share", MessageBoxButton.OK, MessageBoxImage.Warning); return Task.CompletedTask; }))));
            auth.Children.Add(Ui.Two(Ui.Field("Your public key", Ui.Input("SelectedProfile.PublicKey")), Ui.Field("Peer public key", Ui.Input("SelectedProfile.PeerPublicKey"))));
            auth.Children.Add(Ui.Two(Ui.Field("Tunnel address / prefix", Ui.Input("SelectedProfile.TunnelAddress")), Ui.Field("DNS servers", Ui.Input("SelectedProfile.Dns"))));
            auth.Children.Add(Ui.Two(Ui.Field("Endpoint UDP port", Ui.Input("SelectedProfile.Port")), Ui.Field("Listen UDP port (0 = automatic)", Ui.Input("SelectedProfile.ListenPort"))));
        }
        if (vm.Protocol is VpnProtocol.OpenVpn or VpnProtocol.WireGuard || vm.ProviderId == "shrew")
        {
            auth.Children.Add(Ui.Field("External configuration file", Ui.Input("SelectedProfile.ImportedConfigPath"), "Only the path is stored. The source file may contain secrets and remains your responsibility. OpenVPN certificate and transport choices are read from the imported .ovpn."));
            auth.Children.Add(Ui.Actions(Ui.Button("Browse configuration", vm.ImportConfigCommand)));
            if (vm.Protocol == VpnProtocol.WireGuard) auth.Children.Add(Ui.Actions(Ui.Button("Export WireGuard .conf", vm.ExportWireGuardCommand)));
            if (vm.Protocol is VpnProtocol.WireGuard or VpnProtocol.OpenVpn)
            {
                auth.Children.Add(Ui.Actions(Ui.Button("Preview client template", vm.MakeCommand(vm.PreviewClientConfigAsync)), Ui.Button("Save client template", vm.MakeCommand(vm.SaveClientTemplateAsync))));
                var preview = Ui.Output("ClientConfigText", 12); preview.Height = 180; auth.Children.Add(preview);
            }
        }
        if (vm.IsExternalClient)
        {
            auth.Children.Add(Ui.Field(vm.ProviderId == "shrew" ? "Existing Shrew site name" : "Profile name in NCP (reference only)", Ui.Input("SelectedProfile.ExternalProfileName")));
            auth.Children.Add(Ui.Text($"Enter your username, password and shared key in {vm.ClientName}, not here. {vm.ClientName} also controls the IPsec proposals and routing. This console does not send or sync those settings.", 13, "#BDD0DD"));
            auth.Children.Add(Ui.Actions(Ui.BoundButton("ConnectLabel", vm.ConnectCommand)));
        }
        if (vm.Protocol == VpnProtocol.Ikev2 && vm.ProviderId == "native") auth.Children.Add(Ui.Note("Windows handles IKEv2 sign-in and certificate selection. Its built-in IKEv2 engine does not support the legacy PSK/XAUTH workflow."));
        if (vm.Protocol == VpnProtocol.OpenVpn) auth.Children.Add(Ui.Note("Import a supported .ovpn file for UDP/TCP, CA trust, client certificates, and TLS settings. Full tunnel requires a server redirect-gateway policy or an equivalent reviewed client directive; choosing Full here describes the expected policy."));
        if (certificate && vm.Protocol != VpnProtocol.OpenVpn) auth.Children.Add(Certificates(vm));
        if (auth.Children.Count > 0 && !vm.IsExternalClient) root.Children.Add(Ui.Card("Sign-in & engine settings", auth,
            Ui.Check("Remember secrets on this Windows account", "RememberSecrets"),
            Ui.Actions(Ui.Button("Load remembered secrets", vm.MakeCommand(vm.LoadRememberedSecretsAsync)), Ui.Button("Forget secrets", vm.ForgetSecretsCommand)),
            Ui.Text("Unchecked by default. Save removes any previously remembered secrets for this profile. Passwords, PSKs, and private keys never enter ordinary profile JSON.", 12, "#A8BBCC")));
        else if (auth.Children.Count > 0) root.Children.Add(Ui.Card("Sign in with " + vm.ClientName, auth));
        root.Children.Add(Ui.Check("Show advanced settings", "Advanced"));
        if (vm.LabMode) root.Children.Add(Ui.Check("Apply Lab 2 validation to this profile", "SelectedProfile.IsLab"));
        if (vm.LabMode && vm.Protocol == VpnProtocol.IpsecMobile) root.Children.Add(Ui.Card("Lab access policy", Ui.Field("Interpretation", Ui.Select("SelectedPolicy", Enum.GetValues<LabPolicy>())), Ui.Note(LabPresets.PolicyNote)));
        root.Children.Add(Ui.Card(vm.Protocol == VpnProtocol.WireGuard ? "AllowedIPs and policy" : "Private networks",
            Ui.Text(vm.IsExternalClient ? "List the networks you expect to reach so diagnostics can check their routes. This does not change the external client's routing." : "List private networks in CIDR notation, such as 10.20.0.0/24. Diagnostics compare these with Windows routes.", 12, "#A8BBCC"),
            Ui.Field(vm.Protocol == VpnProtocol.WireGuard ? "AllowedIPs (one CIDR per line)" : "Permitted networks (one CIDR per line)", Ui.Input("NetworksText", true), "AllowedIPs identifies peer-routed prefixes. A default route does not prove server authorization or return traffic."),
            vm.Advanced ? Ui.Field("Explicitly forbidden networks (one CIDR per line)", Ui.Input("ForbiddenText", true)) : Ui.Text("Use Advanced settings to record forbidden networks.", 12, "#A8BBCC")));
        if (vm.Advanced) root.Children.Add(Ui.Card("Profile notes", Ui.Field("Notes (no secrets)", Ui.Input("SelectedProfile.Notes", true)), Ui.Note("Phase 1 establishes the IKE security association; Phase 2 establishes traffic protection. ESP carries encrypted IPsec traffic. NAT-T encapsulates it for NAT traversal. These are engine/server settings, not independent proof of connectivity.")));
        if (vm.DeveloperMode) root.Children.Add(Ui.Card("Developer simulation", Ui.Field("Mock failure stage", Ui.Select("SelectedProfile.MockFailure", Enum.GetValues<MockFailure>())), Ui.Note("Select Mock in the engine field. Every simulated event is labelled MOCK; simulated routes and handshakes are not real network evidence.")));
        root.Children.Add(Ui.Actions(Ui.Button("Save profile", vm.SaveProfileCommand, true), Ui.BoundButton("ConnectLabel", vm.ConnectCommand), Ui.Button("Back to dashboard", vm.MakeCommand(() => { vm.SelectedPage = "Dashboard"; return Task.CompletedTask; }))));
        var activeNotice = Ui.Stack(Ui.Note("Disconnect before editing this profile."), Ui.Actions(Ui.BoundButton("DisconnectLabel", vm.DisconnectCommand)));
        activeNotice.SetBinding(UIElement.VisibilityProperty, new Binding("HasActiveConnection") { Converter = new BooleanToVisibilityConverter() });
        return Ui.Stack(activeNotice, root);
    }
    private static FrameworkElement Password(MainViewModel vm, string label, Func<string> get, Action<string> set, string? tip = null)
    {
        var input = new PasswordBox { Password = get() }; input.PasswordChanged += (_, _) => set(input.Password); return Ui.Field(label, input, tip);
    }
    private static FrameworkElement Certificates(MainViewModel vm)
    {
        var grid = Ui.Table("Certificates", ("Subject", "Subject", 2), ("Issuer", "Issuer", 2), ("Expiration", "NotAfter", 1.3), ("Validity", "ValidityStatus", 2));
        grid.SetBinding(DataGrid.SelectedItemProperty, Ui.Bind("SelectedCertificate"));
        return Ui.Stack(grid,
            Ui.Actions(Ui.Button("Refresh store", vm.RefreshCertificatesCommand), Ui.Button("Copy selected thumbprint", vm.MakeCommand(vm.CopyCertificateThumbprintAsync)), Ui.Button("Import public certificate", vm.ImportCertificateCommand)),
            Ui.Text("Personal stores: Current User and Local Machine. Date validity does not verify chain trust or revocation. Private keys are never exported by this screen.", 12, "#A8BBCC"));
    }
    private static FrameworkElement Lab(MainViewModel vm)
    {
        var topology = Ui.Card("Editable topology",
            Ui.Two(Ui.Field("Public network", Ui.Input("Topology.PublicNetwork")), Ui.Field("VyOS external", Ui.Input("Topology.VyosExternal"))),
            Ui.Two(Ui.Field("pfSense external", Ui.Input("Topology.PfSenseExternal")), Ui.Field("pfSense external / NAT", Ui.Input("Topology.PfSenseNat"))),
            Ui.Two(Ui.Field("VyOS HQ", Ui.Input("Topology.VyosHq")), Ui.Field("VyOS Remote", Ui.Input("Topology.VyosRemote"))),
            Ui.Two(Ui.Field("pfSense HQ", Ui.Input("Topology.PfSenseHq")), Ui.Field("pfSense Remote", Ui.Input("Topology.PfSenseRemote"))),
            Ui.Two(Ui.Field("pfSense DMZ", Ui.Input("Topology.PfSenseDmz")), Ui.Field("VyOS DMZ", Ui.Input("Topology.VyosDmz"))),
            Ui.Actions(Ui.Button("Save topology", vm.SaveSettingsCommand), Ui.Button("Add four lab profiles", vm.SeedLabCommand, true)));
        var coverage = new StackPanel();
        foreach (var vpn in vm.Checkoffs)
        {
            coverage.Children.Add(Ui.Card(vpn.Name, Ui.Text(vpn.Summary, 12, "#ADC3D5"), Ui.Button("Open validation checklist", vm.MakeCommand(() => { vm.SelectedCheckoff = vpn.Id; vm.SelectedPage = "Check-Off"; return Task.CompletedTask; }))));
        }
        return Ui.Stack(Ui.Card("CNIT 455 · Lab 2", Ui.BoundText("CheckoffSummary", 22, "#5DE2C2", FontWeights.SemiBold), Ui.Field("Group number x (0–255)", Ui.Input("GroupNumber")), Ui.Actions(Ui.Button("Derive addresses from group", vm.ApplyGroupCommand)), Ui.Text("Changing the group derives 44.104.x.* and 172.18.x.*. Review the original/new router endpoints before applying profiles.", 12, "#A8BBCC")),
            topology, Ui.Note("Lab addressing uses 192.168.4.0/24 for pfSense Remote and 192.168.6.0/24 for VyOS Remote. 192.168.5.0/24 is not part of the supplied topology and produces a Lab Mode warning."),
            Ui.Card("Expected access matrix", ProfilePicker(vm), Ui.Table("AccessRules", ("Destination", "Network", 1.3), ("Policy", "Policy", 1.2), ("Reason / verification", "Reason", 3))), coverage);
    }
    private static FrameworkElement Server(MainViewModel vm)
    {
        var options = Ui.Card("Template and addressing",
            Ui.Field("Configuration template", Ui.Select("ServerOptions.Template", Enum.GetValues<ServerTemplate>())),
            Ui.Two(Ui.Field("VyOS version", Ui.Input("ServerOptions.VyosVersion")), Ui.Field("External interface", Ui.Input("ServerOptions.ExternalInterface"))),
            Ui.Two(Ui.Field("External IP", Ui.Input("ServerOptions.ExternalAddress")), Ui.Field("Remote public endpoint", Ui.Input("ServerOptions.PeerEndpoint"))),
            Ui.Two(Ui.Field("Local network", Ui.Input("ServerOptions.LocalNetwork")), Ui.Field("Remote network", Ui.Input("ServerOptions.RemoteNetwork"))),
            Ui.Two(Ui.Field("Tunnel interface (wg0 / vtun0)", Ui.Input("ServerOptions.TunnelInterface")), Ui.Field("UDP port", Ui.Input("ServerOptions.Port"))),
            Ui.Two(Ui.Field("Local tunnel address / prefix", Ui.Input("ServerOptions.LocalTunnelAddress")), Ui.Field("Peer tunnel address / prefix", Ui.Input("ServerOptions.PeerTunnelAddress"))),
            Ui.Two(Ui.Field("VPN client pool", Ui.Input("ServerOptions.ClientPool")), Ui.Field("VPN gateway address", Ui.Input("ServerOptions.GatewayAddress"))),
            Ui.Two(Ui.Field("DNS server", Ui.Input("ServerOptions.DnsServer")), Ui.Field("Peer public key (WireGuard)", Ui.Input("ServerOptions.PeerPublicKey"))));
        var auth = Ui.Card("Authentication and proposals",
            Ui.Two(Ui.Field("Authentication backend", Ui.Select("ServerOptions.Authentication", Enum.GetValues<AuthBackend>())), Ui.Field("Local test username", Ui.Input("ServerOptions.Username"))),
            Ui.Two(Ui.Field("RADIUS server", Ui.Input("ServerOptions.RadiusServer")), Ui.Field("RADIUS source address", Ui.Input("ServerOptions.RadiusSourceAddress"))),
            Ui.Two(Ui.Field("RADIUS port", Ui.Input("ServerOptions.RadiusPort")), Ui.Field("Domain", Ui.Input("ServerOptions.Domain"))),
            Ui.Two(Ui.Field("LDAP server", Ui.Input("ServerOptions.LdapServer")), Ui.Field("LDAP base DN", Ui.Input("ServerOptions.LdapBaseDn"))),
            Ui.Two(Ui.Field("LDAP bind DN", Ui.Input("ServerOptions.LdapBindDn")), Ui.Field("Allowed AD group", Ui.Input("ServerOptions.AllowedGroup"))),
            Ui.Two(Ui.Field("CA name", Ui.Input("ServerOptions.CaName")), Ui.Field("Certificate name", Ui.Input("ServerOptions.CertificateName"))),
            Ui.Field("Server identity (IKEv2)", Ui.Input("ServerOptions.ServerIdentity")),
            Ui.Two(Ui.Field("IKE encryption", Ui.Input("ServerOptions.IkeEncryption")), Ui.Field("IKE integrity", Ui.Input("ServerOptions.IkeHash"))),
            Ui.Two(Ui.Field("ESP encryption", Ui.Input("ServerOptions.EspEncryption")), Ui.Field("ESP integrity", Ui.Input("ServerOptions.EspHash"))),
            Ui.Two(Ui.Field("DH group", Ui.Input("ServerOptions.DhGroup")), Ui.Field("NAT exemption rule number", Ui.Input("ServerOptions.NatExemptionRule"))));
        var output = Ui.Output("ServerText"); output.Height = 360;
        var checklist = new ItemsControl(); checklist.SetBinding(ItemsControl.ItemsSourceProperty, Ui.Bind("ServerChecklist", false));
        var step = new FrameworkElementFactory(typeof(CheckBox)); step.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Ui.Bind("Completed"));
        var stepText = new FrameworkElementFactory(typeof(TextBlock)); stepText.SetBinding(TextBlock.TextProperty, Ui.Bind("Text", false)); stepText.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); stepText.SetValue(TextBlock.MaxWidthProperty, 830.0); step.AppendChild(stepText);
        checklist.ItemTemplate = new DataTemplate { VisualTree = step };
        var secret = Ui.Card("Secure PSK generator",
            Ui.Two(Ui.Field("Length", Ui.Select("PskLength", new[] { 16, 24, 32, 48, 64 })), Ui.Check("Easy to type (avoid ambiguous characters)", "EasyPsk")),
            Ui.BoundText("DisplayedPsk", 16, "#5DE2C2"), Ui.Check("Reveal generated PSK", "RevealPsk"),
            Ui.Actions(Ui.Button("Generate PSK", vm.GeneratePskCommand), Ui.Button("Copy secret", vm.CopyPskCommand), Ui.Button("Use for active connection", vm.MakeCommand(vm.UseGeneratedPskAsync))),
            Ui.Text("Copy places a secret on the clipboard. It is never saved automatically. WireGuard PSKs must be Base64 keys; use a provider key generator or an imported configuration for WireGuard.", 12, "#D9BE8C"));
        return Ui.Stack(Ui.Note("Reviewed VyOS templates target 1.4 and 1.5. Rolling/custom versions require an exact schema review. Legacy IKEv1/XAUTH remote access has an explicit limitation. pfSense templates are guided checklists. No commands are executed on any device."), options, auth,
            Ui.Actions(Ui.Button("Generate configuration", vm.GenerateServerCommand, true), Ui.Button("Copy commands", vm.CopyServerCommand), Ui.Button("Save TXT", vm.SaveServerCommand), Ui.Button("Copy diagnostic commands", vm.CopyServerDiagnosticsCommand)), Ui.Card("Configuration preview", output), Ui.Card("pfSense completion checklist", Ui.Text("Generate a pfSense template to populate these steps. Checkmarks stay in this session; record formal validation evidence on Check-Off.", 12, "#A8BBCC"), checklist), secret);
    }
    private static FrameworkElement Diagnostics(MainViewModel vm)
    {
        var output = Ui.Output("DiagnosticText"); output.Height = 380;
        var paste = Ui.Input("PastedServerOutput", true); paste.Height = 110;
        return Ui.Stack(Ui.Card("Collect and share", ProfilePicker(vm), Ui.Actions(Ui.Button("Run diagnostics", vm.TestCommand, true), Ui.Button("Copy for ChatGPT", vm.CopyDiagnosticsCommand), Ui.Button("Export evidence ZIP", vm.ExportEvidenceCommand)), Ui.Text("Collects relevant VPN state, routes, adapters, DNS, installed engines, and VPN event evidence. Reports and logs redact registered secrets and recognized secret fields. Review addresses and usernames before sharing.", 12, "#A8BBCC")),
            Ui.Card("Analysis", Ui.BoundText("RouteSummary", 14, "#5DE2C2"), Ui.Table("Findings", ("Category", "Category", 1), ("Result", "Result", .7), ("Observation", "Summary", 2.2), ("Next checks", "NextChecks", 3))),
            Ui.Disclosure("Windows route table", Ui.Table("Routes", ("Destination", "Destination", 1.4), ("Next hop", "NextHop", 1.3), ("Interface", "InterfaceAlias", 1.5), ("Metric", "EffectiveMetric", .6))),
            Ui.Disclosure("Add server output (optional)", Ui.Text("Paste relevant output from show commands. Secret labels and registered secrets are removed from the report. Review any free-form content before sharing.", 12, "#A8BBCC"), paste),
            Ui.Disclosure("Full redacted report", output), Ui.Text("Route checks do not prove encrypted traffic or access to a private host. Verify those separately when needed.", 12, "#A8BBCC"));
    }
    private static FrameworkElement Checkoff(MainViewModel vm)
    {
        var table = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, MinHeight = 360, MaxHeight = 680 };
        table.SetBinding(ItemsControl.ItemsSourceProperty, Ui.Bind("CheckoffRows", false));
        var wrap = new Style(typeof(TextBlock)); wrap.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap)); wrap.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(8)));
        table.Columns.Add(new DataGridTextColumn { Header = "Validation requirement", Binding = Ui.Bind("Text", false), IsReadOnly = true, Width = new DataGridLength(2, DataGridLengthUnitType.Star), ElementStyle = wrap });
        table.Columns.Add(new DataGridComboBoxColumn { Header = "Observed result", SelectedItemBinding = Ui.Bind("Result"), ItemsSource = Enum.GetValues<ResultState>(), Width = 125 });
        table.Columns.Add(new DataGridTextColumn { Header = "Evidence / timestamp / capture reference", Binding = Ui.Bind("Evidence"), Width = new DataGridLength(2, DataGridLengthUnitType.Star), ElementStyle = wrap });
        return Ui.Stack(Ui.Card("Validation workspace", Ui.BoundText("CheckoffSummary", 21, "#5DE2C2"), Ui.Field("VPN to validate", Ui.Select("SelectedCheckoff", vm.Checkoffs, "Name", "Id")), Ui.Text("Choose UNKNOWN, PASS, or FAIL and record the observation. A VPN counts as validated only when every item is PASS with evidence. These are your recorded observations, not automatic claims by the app.", 12, "#A8BBCC")),
            table, Ui.Actions(Ui.Button("Run current connection diagnostics", vm.TestCommand), Ui.Button("Export check-off evidence", vm.ExportEvidenceCommand, true)),
            Ui.Card("Packet-capture guidance", Ui.BoundText("CaptureGuidance", 12, "#BDD0DD"), Ui.Button("Copy capture instructions", vm.MakeCommand(vm.CopyCaptureGuidanceAsync))),
            Ui.Note("Record successful permitted access and blocked forbidden access separately. A timeout alone cannot distinguish firewall enforcement from an unreachable destination. Capture evidence must show native private addresses and the absence of public-facing NAT on site-to-site traffic."));
    }
    private static FrameworkElement Dependencies(MainViewModel vm)
    {
        var table = Ui.Table("EngineRows", ("VPN engine", "Name", 1.5), ("Availability", "Availability", .8), ("How it works", "Role", 1.8));
        table.MaxHeight = 340;
        var selection = Ui.Select("SelectedDependency", vm.Dependencies, "Name");
        var download = Ui.Button("Open official download", vm.MakeCommand(() => vm.SelectedDependency is { } info ? vm.OpenOfficialDownloadAsync(info) : Task.CompletedTask));
        download.SetBinding(UIElement.IsEnabledProperty, Ui.Bind("HasDownload", false));
        return Ui.Stack(Ui.Card("Available on this computer", Ui.BoundText("DependencySummary", 13, "#BDD0DD"),
                Ui.Actions(Ui.Button("Refresh detection", vm.RefreshDependenciesCommand, true)), table),
            Ui.Card("Engine details & downloads", Ui.Field("Engine or prerequisite", selection), Ui.BoundText("SelectedDependency.Details", 12, "#BDD0DD"),
                Ui.Actions(download, Ui.Button("Copy install command", vm.MakeCommand(() => vm.SelectedDependency is { } info ? vm.CopyInstallCommandAsync(info) : Task.CompletedTask))),
                Ui.Disclosure("Version & file location", Ui.BoundText("DependencyVersion", 12), Ui.BoundText("SelectedDependency.ExecutablePath", 12, "#A8BBCC"))),
            Ui.Disclosure("Installation & administrator access", Ui.Text("Nothing is installed automatically. A detected engine is not proof of a working VPN. NCP uses its own license; Shrew is a legacy client.", 12, "#A8BBCC"),
                Ui.Text("Start normally. If an engine requires administrator access for an operation, close the console and reopen it with Run as administrator. Your profiles stay in the same Windows account.", 12, "#A8BBCC")));
    }
    private static FrameworkElement Settings(MainViewModel vm)
    {
        return Ui.Stack(Ui.Card("Workspace", Ui.Check("Lab Mode · CNIT 455 topology and check-off guidance", "LabMode"), Ui.Check("Developer Mode · expose the simulated Mock engine", "DeveloperMode"), Ui.Field("Log retention in days (1–365)", Ui.Input("LogRetentionDays")),
            Ui.Actions(Ui.Button("Save settings", vm.SaveSettingsCommand, true), Ui.Button("Clear stored logs", vm.MakeCommand(vm.DeleteStoredLogsAsync))), Ui.Text("LOCAL DATA", 11, "#A8BBCC"), Ui.BoundText("DataLocation", 12, "#BDD0DD")),
            Ui.Card("Privacy and secrets", Ui.Text("Ordinary profiles contain non-secret JSON. Passwords, pre-shared keys, private keys, and RADIUS secrets stay in memory by default. Optional remembered secrets are protected by Windows for the current account. Imported VPN files may already contain secrets; protect the original files.", 13, "#BDD0DD"),
            Ui.Text("Copy for ChatGPT copies a redacted report to your clipboard. The application does not send it to ChatGPT, a telemetry service, or another server. Usernames, addresses, and topology are included because they help troubleshooting; review them before sharing.", 13, "#BDD0DD")),
            Ui.Card("Windows certificate inventory", Certificates(vm)),
            Ui.Card("Explain this", Ui.Text("PSK — pre-shared secret for peer authentication. IKE — negotiation for IPsec. ESP — protected IPsec traffic. XAUTH — legacy user authentication. RADIUS — centralized authentication service. LDAP — directory access. NAT-T — IPsec encapsulation through NAT. Mode Config — client address and network provisioning. AllowedIPs — prefixes assigned to a WireGuard peer. Phase 1 — IKE security association. Phase 2 — traffic security association.", 13, "#BDD0DD")));
    }
    private static FrameworkElement About(MainViewModel vm) => Ui.Stack(Ui.Card("Windows VPN Console", Ui.Text(DisplayLabels.Version, 26, "#5DE2C2", FontWeights.SemiBold), Ui.Text("Your connections, network checks and VPN tools in one workspace.", 17, "#DDEAF3"),
        Ui.Text("Built with .NET 10 and WPF. VPN cryptography is delegated to Windows or installed third-party engines. No VPN binary is redistributed with this application.", 13, "#ADC3D5")),
        Ui.Card("Independent utility", Ui.Text("Windows VPN Console is an independent network administration utility. It is not affiliated with Purdue University, VyOS, Netgate/pfSense, OpenVPN, WireGuard, NCP, or Shrew Soft.", 13, "#ADC3D5")),
        Ui.Card("Designed for observable evidence", Ui.Text("UNKNOWN is an intentional result. The app never promotes an unobserved gateway response, encryption state, authentication phase, route, or firewall rule to PASS. Use real peer connectivity and capture evidence to complete a lab check-off.", 13, "#ADC3D5")),
        Ui.Card("License and documentation", Ui.Text("Application source: MIT License. External clients retain their own licenses. Read README, THIRD_PARTY_NOTICES, and the bundled Docs directory for provider limitations, installation, security, and the release process.", 13, "#ADC3D5")));
}
