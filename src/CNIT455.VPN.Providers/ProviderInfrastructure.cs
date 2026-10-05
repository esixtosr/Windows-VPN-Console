using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using CNIT455.VPN.Core;

namespace CNIT455.VPN.Providers;

public static class ProviderRegistry
{
    public static IReadOnlyList<IVpnProvider> CreateDefault(SecretRedactor redactor) =>
        [new WindowsNativeVpnProvider(redactor), new WireGuardProvider(redactor), new OpenVpnProvider(redactor), new ShrewSoftProvider(redactor), new NcpProvider(redactor), new MockVpnProvider(redactor)];
}

public abstract class VpnProviderBase(SecretRedactor? redactor = null) : IVpnProvider
{
    protected readonly SecretRedactor Redactor = redactor ?? new();
    public abstract string Id { get; }
    public event EventHandler<VpnLogEvent>? LogReceived;
    protected void Log(VpnStage stage, string message, LogSeverity severity = LogSeverity.Information) =>
        LogReceived?.Invoke(this, new(DateTimeOffset.Now, severity, Id, stage, Redactor.Redact(message)));
    protected void Register(VpnSecrets secrets)
    {
        foreach (var secret in new[] { secrets.Password, secrets.Psk, secrets.PrivateKey, secrets.RadiusSecret })
            if (!string.IsNullOrEmpty(secret)) Redactor.RegisterSecret(secret);
    }
    protected ProviderResult Fail(string message, VpnStage stage = VpnStage.Initialization)
    { var clean = Redactor.Redact(message); Log(stage, clean, LogSeverity.Error); return new(false, clean, new(VpnState.Failed, clean)); }
    protected ProviderResult? Guard(VpnProfile profile, bool administrator = false)
    {
        if (!OperatingSystem.IsWindows()) return Fail("This engine requires Windows 11. Select Mock in Developer Mode to exercise the UI here.");
        if (administrator && !ProviderEnvironment.IsAdministrator) return Fail("Administrator rights are required by this VPN engine. Restart the console as administrator, then retry.");
        var errors = ValidateProfile(profile).Where(x => x.IsError).ToArray();
        return errors.Length == 0 ? null : Fail(string.Join("; ", errors.Select(x => x.Message)));
    }
    public abstract Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
    public abstract Task<DependencyInfo> DetectInstallation(CancellationToken cancellationToken = default);
    public virtual IReadOnlyList<ValidationIssue> ValidateProfile(VpnProfile profile) => ProfileValidator.Validate(profile);
    public abstract Task<ProviderResult> ConnectAsync(VpnProfile profile, VpnSecrets secrets, CancellationToken cancellationToken = default);
    public abstract Task<ProviderResult> DisconnectAsync(VpnProfile profile, CancellationToken cancellationToken = default);
    public abstract Task<VpnStatus> GetStatusAsync(VpnProfile profile, CancellationToken cancellationToken = default);
    public virtual Task StartLogStreamAsync(VpnProfile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public virtual Task StopLogStreamAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public virtual async Task<string> GetDiagnosticsAsync(VpnProfile profile, CancellationToken cancellationToken = default)
    { var dependency = await DetectInstallation(cancellationToken); var status = await GetStatusAsync(profile, cancellationToken); return Redactor.Redact($"{dependency.Name}: {dependency.Details}\nEngine version: {dependency.Version ?? "unknown"}\nStatus: {status.State}: {status.Message}\n" + string.Join("\n", status.Details?.Select(x => $"{x.Key}: {x.Value}") ?? [])); }
}

public static class ProviderEnvironment
{
    public static bool IsAdministrator
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return false;
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
    public static string? FindProgram(params string[] relativePaths)
    {
        if (!OperatingSystem.IsWindows()) return null;
        foreach (var folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }.Where(Path.IsPathFullyQualified).Distinct())
            foreach (var relative in relativePaths)
            { var path = Path.Combine(folder, relative); if (File.Exists(path)) return path; }
        return null;
    }
    public static string? PowerShellPath => OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe") : null;
    public static DependencyInfo Dependency(string id, string name, string? path, string details, string? url = null, string? installCommand = null) =>
        new(id, name, path is not null && File.Exists(path), path, path is not null && File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).FileVersion : null, details, url, installCommand);
    internal static Task<ProcessResult> PowerShellAsync(string script, object data, CancellationToken token)
    {
        if (PowerShellPath is null || !File.Exists(PowerShellPath)) throw new PlatformNotSupportedException("Windows PowerShell is unavailable.");
        // Only our static script is on the command line. Profile values and secrets are data on stdin.
        var complete = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; try { $d=([Console]::In.ReadToEnd() | ConvertFrom-Json); " + script + " } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }";
        return SafeProcess.RunAsync(PowerShellPath, ["-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(complete))], JsonSerializer.Serialize(data), token);
    }
    internal static async Task<string> ExternalServicesAsync(string vendor, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) return "unavailable on this operating system";
        try
        {
            var result = await PowerShellAsync("$s=Get-Service | Where-Object { $_.DisplayName -like ('*'+$d.Vendor+'*') -or $_.Name -like ($d.Vendor+'*') }; if($s){$s | ForEach-Object { $_.Name + '=' + [string]$_.Status }}else{'No matching services found'}", new { Vendor=vendor }, token);
            return result.ExitCode == 0 ? result.StandardOutput.Trim() : "could not query service state";
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return "could not query service state"; }
    }
    public static Task<DependencyInfo> DetectPowerShellAsync() => Task.FromResult(Dependency("powershell", "Windows PowerShell", PowerShellPath, "Windows VpnClient cmdlets provision native VPN profiles."));
}
