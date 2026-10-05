using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows;
using CNIT455.VPN.Core;
using CNIT455.VPN.ConfigGenerators;
using Microsoft.Win32;

namespace CNIT455.VPN.App;

public sealed partial class MainViewModel
{
    private void EnsureCanConfigure()
    {
        if (!CanConfigure) throw new InvalidOperationException("Disconnect the current tunnel before changing profiles.");
    }
    private Task CopyGeneratedAsync(string value, string message)
    {
        // Only the validated, placeholder-only configuration generator uses this path.
        // Diagnostics and external output always pass through the redactor instead.
        Clipboard.SetText(value); StatusText = message; return Task.CompletedTask;
    }
    public ObservableCollection<ChecklistStep> ServerChecklist { get; } = [];
    private string clientConfigText = "Generate a template to review its routes and placeholders before saving.";
    public string ClientConfigText { get => clientConfigText; private set => Set(ref clientConfigText, value); }
    public string CaptureGuidance
    {
        get
        {
            var protocol = SelectedCheckoff switch
            {
                "s2s-wireguard" or "client-wireguard" => VpnProtocol.WireGuard,
                "s2s-openvpn" or "client-openvpn" => VpnProtocol.OpenVpn,
                "client-l2tp" => VpnProtocol.L2tpIpsec,
                _ => VpnProtocol.IpsecMobile
            };
            return PacketCaptureGuide.For(protocol);
        }
    }
    public Task CopyCaptureGuidanceAsync() => CopyAsync(CaptureGuidance, "Capture instructions copied. Review interface and filters before running a capture.");
    public Task GenerateWireGuardPskAsync()
    {
        if (Protocol != VpnProtocol.WireGuard) throw new InvalidOperationException("Select a WireGuard connection first.");
        Secrets.Psk = PskGenerator.GenerateWireGuardPsk(); RegisterSecrets(); Raise(nameof(EditorRevision));
        StatusText = "A 32-byte WireGuard PSK was generated in memory. Export explicitly to transfer it securely to the peer.";
        return Task.CompletedTask;
    }
    public Task PreviewClientConfigAsync()
    {
        ClientConfigText = Protocol switch
        {
            VpnProtocol.WireGuard => WireGuardConfigGenerator.Generate(RequireProfile()),
            VpnProtocol.OpenVpn => OpenVpnClientConfigGenerator.Generate(RequireProfile()),
            _ => throw new InvalidOperationException("Client templates are available for OpenVPN and WireGuard.")
        };
        StatusText = "Client template generated with secret placeholders. Review provider and server requirements before using it.";
        return Task.CompletedTask;
    }
    public async Task SaveClientTemplateAsync()
    {
        await PreviewClientConfigAsync();
        var dialog = new SaveFileDialog { FileName = Protocol == VpnProtocol.OpenVpn ? "client-template.ovpn" : "tunnel-template.conf", Filter = "VPN configuration|*.ovpn;*.conf" };
        if (dialog.ShowDialog() == true) { await File.WriteAllTextAsync(dialog.FileName, ClientConfigText); StatusText = "Template saved with placeholders. Supply certificates and secrets before connecting."; }
    }
    private void RefreshServerChecklist()
    {
        ServerChecklist.Clear();
        if (ServerOptions.Template is ServerTemplate.PfSenseIpsec or ServerTemplate.PfSenseOpenVpn or ServerTemplate.PfSenseWireGuard)
            foreach (var step in PfSenseChecklist.Create(ServerOptions, Topology)) ServerChecklist.Add(new ChecklistStep(step));
    }
    private void ValidateTopology()
    {
        if (GroupNumber is < 0 or > 255) throw new ArgumentException("Group number must be between 0 and 255.");
        foreach (var property in typeof(LabTopology).GetProperties().Where(p => p.PropertyType == typeof(string)))
        {
            var value = (string?)property.GetValue(Topology) ?? "";
            var host = property.Name is nameof(LabTopology.VyosExternal) or nameof(LabTopology.PfSenseExternal) or nameof(LabTopology.PfSenseNat);
            if (host ? !IPAddress.TryParse(value.Split('/')[0], out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork : !CidrNetwork.TryParse(value, out _))
                throw new ArgumentException($"{property.Name}: enter a valid IPv4 {(host ? "address" : "CIDR network")}.");
            if (host && value.Contains('/') && !CidrNetwork.TryParse(value, out _)) throw new ArgumentException($"{property.Name}: invalid prefix length.");
        }
        Topology.GroupNumber = GroupNumber;
    }
    private static async Task SavePrivateExportAsync(string path, string contents)
    {
        // Create the destination with its final ACL before any secret bytes are written.
        var security = new FileSecurity(); security.SetAccessRuleProtection(true, false);
        using var identity = WindowsIdentity.GetCurrent();
        security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
        using var stream = new FileInfo(path).Create(FileMode.Create, FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.None, security);
        using var writer = new StreamWriter(stream); await writer.WriteAsync(contents);
    }
}

public sealed class ChecklistStep(string text) : ObservableObject
{
    private bool completed;
    public string Text { get; } = text;
    public bool Completed { get => completed; set => Set(ref completed, value); }
}
