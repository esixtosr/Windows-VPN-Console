using System.Diagnostics;
using CNIT455.VPN.Core;

namespace CNIT455.VPN.Providers;

/// <summary>Legacy Shrew client handoff. A launched window is never treated as an established VPN.</summary>
public sealed class ShrewSoftProvider(SecretRedactor? redactor = null) : VpnProviderBase(redactor)
{
    public override string Id => "shrew";
    private static string? Executable => ProviderEnvironment.FindProgram(@"ShrewSoft\VPN Client\ipsecc.exe", @"ShrewSoft VPN Client\ipsecc.exe");
    private static string? Manager => ProviderEnvironment.FindProgram(@"ShrewSoft\VPN Client\ipseca.exe", @"ShrewSoft VPN Client\ipseca.exe");
    private const string Limitations = "Legacy IKEv1/XAUTH client. Windows 11 compatibility is not verified by Shrew's published support matrix. Import .vpn files with Shrew VPN Access Manager, then enter the exact site name. Credentials are entered only in Shrew. Safe per-profile disconnect/status/log APIs are unavailable; use Shrew's window and paste sanitized diagnostic output here.";
    public override Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ProviderCapabilities(Id, "Shrew Soft (interactive)", [VpnProtocol.IpsecMobile], [AuthenticationMode.ProviderDefault], true, false, false, false, Limitations));
    public override async Task<DependencyInfo> DetectInstallation(CancellationToken cancellationToken = default)
    {
        var services = await ProviderEnvironment.ExternalServicesAsync("Shrew", cancellationToken);
        return ProviderEnvironment.Dependency(Id, "Shrew Soft VPN Client", Executable, Limitations + " Services: " + services, "https://www.shrew.net/download/vpn");
    }
    public override IReadOnlyList<ValidationIssue> ValidateProfile(VpnProfile profile)
    {
        var issues = base.ValidateProfile(profile).ToList();
        if (profile.Protocol != VpnProtocol.IpsecMobile) issues.Add(new("Protocol", "Shrew handles legacy mobile IPsec, not native IKEv2 or L2TP."));
        if (profile.ExternalProfileName.Length > 255 || profile.ExternalProfileName.Any(char.IsControl)) issues.Add(new("External profile", "Use the exact printable site name shown in Shrew VPN Access Manager (maximum 255 characters)."));
        if (string.IsNullOrWhiteSpace(profile.ExternalProfileName) && string.IsNullOrWhiteSpace(profile.ImportedConfigPath)) issues.Add(new("External profile", "Select an existing Shrew site name, or select a .vpn file to import through Shrew VPN Access Manager."));
        if (!string.IsNullOrWhiteSpace(profile.ImportedConfigPath) && (!Path.GetExtension(profile.ImportedConfigPath).Equals(".vpn", StringComparison.OrdinalIgnoreCase) || !File.Exists(profile.ImportedConfigPath))) issues.Add(new("Imported file", "Select an existing Shrew .vpn file. Import is completed in the vendor client."));
        return issues;
    }
    public override Task<ProviderResult> ConnectAsync(VpnProfile profile, VpnSecrets secrets, CancellationToken cancellationToken = default)
    {
        Register(secrets); cancellationToken.ThrowIfCancellationRequested();
        if (Guard(profile) is { } rejected) return Task.FromResult(rejected);
        if (Executable is not { } executable) return Task.FromResult(Fail("Shrew Soft is not installed in a recognized Program Files location. Install and verify the vendor client separately before using this legacy adapter."));
        try
        {
            string message;
            ProcessStartInfo start;
            if (string.IsNullOrWhiteSpace(profile.ExternalProfileName))
            {
                if (Manager is not { } manager) return Task.FromResult(Fail("Shrew VPN Access Manager is missing. Import the .vpn file in the vendor client and enter its exact site name."));
                start = new(manager) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(manager)! };
                message = "Shrew VPN Access Manager opened. Use File > Import for the selected .vpn file, then enter its site name in this console. No connection has been attempted.";
            }
            else
            {
                start = new(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! };
                start.ArgumentList.Add("-r"); start.ArgumentList.Add(profile.ExternalProfileName);
                // Intentionally omit -p, -u and -a: authenticate in the visible vendor UI.
                message = "Shrew connection window opened for the named site. Enter XAUTH credentials and connect in that window. Tunnel state remains UNKNOWN until independently verified.";
            }
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Shrew window did not start.");
            Log(VpnStage.Initialization, message, LogSeverity.Warning);
            return Task.FromResult(new ProviderResult(true, message, new(VpnState.ExternalClient, message)));
        }
        catch (Exception ex) { return Task.FromResult(Fail("Shrew launch failed: " + ex.Message)); }
    }
    public override Task<ProviderResult> DisconnectAsync(VpnProfile profile, CancellationToken cancellationToken = default) => Task.FromResult(new ProviderResult(false, "Disconnect in the Shrew connection window. This adapter will not stop shared VPN services or terminate unrelated processes.", new(VpnState.Unknown, "Shrew disconnect must be completed in the vendor client.")));
    public override Task<VpnStatus> GetStatusAsync(VpnProfile profile, CancellationToken cancellationToken = default) => Task.FromResult(new VpnStatus(VpnState.Unknown, "Shrew tunnel state is not exposed through a verified per-profile API. Review the Shrew connection window, routes and destination reachability."));
}

/// <summary>Optional commercial client handoff; unverified/global CLI commands are never invoked.</summary>
public sealed class NcpProvider(SecretRedactor? redactor = null) : VpnProviderBase(redactor)
{
    public override string Id => "ncp";
    private static string? Executable => ProviderEnvironment.FindProgram(@"NCP\SecureClient\ncpmon.exe");
    private const string Limitations = "Separately licensed NCP Secure Entry client. Interactive handoff only in v0.1.0. NCP advertises an API/CLI, but a current, version-specific, profile-scoped control and status contract was not verified. Configure/select the profile and enter credentials in NCP; no NCP binaries or licensing are bundled.";
    public override Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ProviderCapabilities(Id, "NCP Secure Entry (interactive)", [VpnProtocol.IpsecMobile, VpnProtocol.Ikev2], [AuthenticationMode.ProviderDefault], true, false, false, false, Limitations));
    public override async Task<DependencyInfo> DetectInstallation(CancellationToken cancellationToken = default)
    {
        var services = await ProviderEnvironment.ExternalServicesAsync("NCP", cancellationToken);
        return ProviderEnvironment.Dependency(Id, "NCP Secure Entry", Executable, Limitations + " Services: " + services, "https://www.ncp-e.com/en/service-resources/download-vpn-client");
    }
    public override IReadOnlyList<ValidationIssue> ValidateProfile(VpnProfile profile)
    {
        var issues = base.ValidateProfile(profile).ToList();
        if (profile.Protocol is not (VpnProtocol.IpsecMobile or VpnProtocol.Ikev2)) issues.Add(new("Protocol", "Select external mobile IPsec or IKEv2 for this NCP handoff."));
        return issues;
    }
    public override Task<ProviderResult> ConnectAsync(VpnProfile profile, VpnSecrets secrets, CancellationToken cancellationToken = default)
    {
        Register(secrets); cancellationToken.ThrowIfCancellationRequested();
        if (Guard(profile) is { } rejected) return Task.FromResult(rejected);
        if (Executable is not { } executable) return Task.FromResult(Fail("NCP Secure Entry is not installed in a recognized Program Files location. Configure a licensed vendor installation separately, or select another compatible engine."));
        try
        {
            using var process = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! }) ?? throw new InvalidOperationException("NCP monitor did not start.");
            const string message = "NCP monitor opened. Select the required profile and connect there. This console does not pass secrets, alter NCP configuration, or infer connection success from the window.";
            Log(VpnStage.Initialization, message, LogSeverity.Warning);
            return Task.FromResult(new ProviderResult(true, message, new(VpnState.ExternalClient, message)));
        }
        catch (Exception ex) { return Task.FromResult(Fail("NCP launch failed: " + ex.Message)); }
    }
    public override Task<ProviderResult> DisconnectAsync(VpnProfile profile, CancellationToken cancellationToken = default) => Task.FromResult(new ProviderResult(false, "Disconnect the selected profile in NCP. A global NCP disconnect command could affect a connection this console does not own.", new(VpnState.Unknown, "NCP disconnect requires the vendor client.")));
    public override Task<VpnStatus> GetStatusAsync(VpnProfile profile, CancellationToken cancellationToken = default) => Task.FromResult(new VpnStatus(VpnState.Unknown, "NCP status is not available through a verified per-profile API. Read NCP's status and validate routes separately."));
}
