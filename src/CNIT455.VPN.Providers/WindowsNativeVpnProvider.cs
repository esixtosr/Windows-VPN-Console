using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using CNIT455.VPN.Core;

namespace CNIT455.VPN.Providers;

public sealed class WindowsNativeVpnProvider(SecretRedactor? redactor = null) : VpnProviderBase(redactor)
{
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly Dictionary<Guid,DateTimeOffset> connectedSince=[];
    public override string Id=>"native";
    public static string ConnectionName(VpnProfile profile)=>"CNIT455-"+profile.Id.ToString("N");
    public override Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken=default)=>Task.FromResult(new ProviderCapabilities(Id,"Windows native VPN",[VpnProtocol.L2tpIpsec,VpnProtocol.Ikev2,VpnProtocol.Sstp],[AuthenticationMode.PskAndUsername,AuthenticationMode.UsernamePassword,AuthenticationMode.ProviderDefault],true,true,true,false,"L2TP uses PSK + MSCHAPv2; SSTP uses MSCHAPv2 over TLS. IKEv2 uses Windows EAP authentication UI. Certificate/EAP policy must be configured by an administrator; this release does not auto-select certificates.")
    {
        IntegrationType = ProviderIntegrationType.WindowsNative,
        Flags = ProviderCapabilityFlags.SupportsNativeConnect | ProviderCapabilityFlags.SupportsNativeDisconnect | ProviderCapabilityFlags.SupportsEap | ProviderCapabilityFlags.SupportsPsk | ProviderCapabilityFlags.SupportsLiveStatus | ProviderCapabilityFlags.SupportsSplitTunnel,
        CapabilityNotes = "Windows native IKEv2 support must not be treated as proof of PSK-server plus EAP-client compatibility for every gateway."
    });
    public override Task<DependencyInfo> DetectInstallation(CancellationToken cancellationToken=default)=>Task.FromResult(ProviderEnvironment.Dependency(Id,"Windows native VPN",ProviderEnvironment.PowerShellPath,"Requires Windows VpnClient module and Remote Access Connection Manager (RasMan). Profiles belong to the current Windows account."));
    public override IReadOnlyList<ValidationIssue> ValidateProfile(VpnProfile profile)
    {
        var issues=base.ValidateProfile(profile).ToList();
        if(profile.Protocol is not (VpnProtocol.L2tpIpsec or VpnProtocol.Ikev2 or VpnProtocol.Sstp)) issues.Add(new("Protocol","Windows native VPN does not implement IKEv1 XAUTH mobile IPsec."));
        if(profile.Protocol==VpnProtocol.L2tpIpsec && profile.Authentication is not (AuthenticationMode.PskAndUsername or AuthenticationMode.ProviderDefault)) issues.Add(new("Authentication","This L2TP adapter requires PSK + username authentication."));
        if(profile.Protocol is VpnProtocol.Ikev2 or VpnProtocol.Sstp && profile.Authentication is not (AuthenticationMode.UsernamePassword or AuthenticationMode.ProviderDefault)) issues.Add(new("Authentication","Use UsernamePassword or ProviderDefault; automatic certificate-policy provisioning is not supported."));
        if(profile.Username.Length>256 || profile.Domain.Length>15) issues.Add(new("Username","Windows RAS limits username to 256 characters and the separate domain field to 15 characters. A UPN may be used as username."));
        return issues;
    }
    internal const string ProvisionScript="""
        Import-Module VpnClient -ErrorAction Stop
        $existing=Get-VpnConnection -ErrorAction Stop | Where-Object Name -eq $d.Name
        if($existing -and $existing.ConnectionStatus -ne 'Disconnected'){throw 'This app-owned profile is already active. Disconnect before editing it.'}
        if($existing){Remove-VpnConnection -Name $d.Name -Force -ErrorAction Stop}
        $p=@{Name=$d.Name;ServerAddress=$d.Gateway;TunnelType=$d.Tunnel;EncryptionLevel='Required';AuthenticationMethod=$d.Method;SplitTunneling=[bool]$d.Split;RememberCredential=$false;Force=$true;ErrorAction='Stop'}
        if($d.Tunnel -eq 'L2tp'){$p.L2tpPsk=$d.Psk}
        Add-VpnConnection @p | Out-Null
        foreach($route in $d.Routes){Add-VpnConnectionRoute -ConnectionName $d.Name -DestinationPrefix $route -ErrorAction Stop | Out-Null}
        'Profile provisioned'
        """;
    public override async Task<ProviderResult> ConnectAsync(VpnProfile profile,VpnSecrets secrets,CancellationToken cancellationToken=default)
    {
        Register(secrets); if(Guard(profile) is {} rejected) return rejected;
        if(profile.Protocol==VpnProtocol.L2tpIpsec && string.IsNullOrWhiteSpace(secrets.Psk)) return Fail("Enter the L2TP pre-shared key before connecting.");
        if(profile.Protocol!=VpnProtocol.Ikev2 && (string.IsNullOrWhiteSpace(profile.Username)||string.IsNullOrEmpty(secrets.Password))) return Fail("Enter username and password before connecting; blank values must not silently use Windows sign-in credentials.",VpnStage.Authentication);
        if(secrets.Password.Length>256) return Fail("Windows RAS limits passwords to 256 characters.",VpnStage.Authentication);
        await gate.WaitAsync(cancellationToken);
        var provisioned=false;
        try
        {
            var data=new { Name=ConnectionName(profile),profile.Gateway,Tunnel=profile.Protocol switch { VpnProtocol.L2tpIpsec=>"L2tp",VpnProtocol.Ikev2=>"Ikev2",_=>"Sstp" },Method=profile.Protocol==VpnProtocol.Ikev2?"Eap":"MSChapv2",Split=profile.TunnelMode==TunnelMode.Split,Routes=profile.PermittedNetworks,Psk=secrets.Psk };
            Log(VpnStage.Initialization,"Provisioning the app-owned Windows VPN profile. Windows stores the L2TP PSK in its protected VPN configuration until this profile is removed on disconnect.");
            var prepared=await ProviderEnvironment.PowerShellAsync(ProvisionScript,data,cancellationToken);
            if(prepared.ExitCode!=0) { await RemoveAsync(profile,CancellationToken.None); return Fail("Windows profile provisioning failed: "+prepared.StandardError+" Check the VpnClient module and profile permissions."); }
            provisioned=true;
            if(profile.Protocol==VpnProtocol.Ikev2)
            {
                var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"rasphone.exe")) {UseShellExecute=false}; start.ArgumentList.Add("-d"); start.ArgumentList.Add(ConnectionName(profile));
                using var dialog=Process.Start(start);
                Log(VpnStage.Eap,"Windows EAP authentication dialog opened. Configure the matching trusted server/certificate policy in Windows; actual connection status is polled separately.");
                return new(true,"Complete authentication in the Windows VPN dialog. Connection is not yet confirmed.",new(VpnState.ExternalClient,"Waiting for Windows EAP authentication"));
            }
            var error=await RasNative.DialAsync(ConnectionName(profile),profile.Username,secrets.Password,profile.Domain,(state,code)=>Log(state==0x2000?VpnStage.Tunnel:VpnStage.Authentication,$"Windows RAS state {state}; result {code}."),cancellationToken);
            if(error!=0) { await RemoveAsync(profile,CancellationToken.None); return Fail($"Windows RAS returned error {error}: {RasNative.Error(error)}. Profile provisioning succeeded. Check gateway reachability, PSK, server authentication and Windows RasClient events; protocol stages not exposed by RAS remain unknown.",VpnStage.Tunnel); }
            connectedSince[profile.Id]=DateTimeOffset.Now;
            var status=await GetStatusAsync(profile,cancellationToken);
            return new(status.State==VpnState.Connected,status.Message,status);
        }
        catch(OperationCanceledException) { if(provisioned) await RemoveAsync(profile,CancellationToken.None); throw; }
        catch(Exception ex) { if(provisioned) await RemoveAsync(profile,CancellationToken.None); return Fail("Windows VPN operation failed: "+ex.Message); }
        finally { gate.Release(); }
    }
    private static Task<ProcessResult> RemoveAsync(VpnProfile profile,CancellationToken token)=>ProviderEnvironment.PowerShellAsync("Get-VpnConnection | Where-Object Name -eq $d.Name | ForEach-Object { Remove-VpnConnection -Name $_.Name -Force -ErrorAction Stop }",new {Name=ConnectionName(profile)},token);
    public override async Task<ProviderResult> DisconnectAsync(VpnProfile profile,CancellationToken cancellationToken=default)
    {
        if(!OperatingSystem.IsWindows()) return Fail("Windows is required.");
        await gate.WaitAsync(cancellationToken);
        try
        {
            var result=await SafeProcess.RunAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"rasdial.exe"),[ConnectionName(profile),"/disconnect"],cancellationToken:cancellationToken);
            var status=await GetStatusAsync(profile,cancellationToken);
            if(status.State is VpnState.Connected or VpnState.Connecting or VpnState.Unknown) return Fail("Disconnect could not be confirmed. "+result.StandardOutput+result.StandardError,VpnStage.Disconnect);
            var removed=await RemoveAsync(profile,cancellationToken);
            if(removed.ExitCode!=0) return Fail("Tunnel disconnected, but the temporary Windows profile could not be removed. Remove "+ConnectionName(profile)+" in Windows VPN settings. "+removed.StandardError,VpnStage.Disconnect);
            await RasNative.ReleaseCallbackAsync(ConnectionName(profile)); connectedSince.Remove(profile.Id); Log(VpnStage.Disconnect,"Windows VPN disconnected and app-owned profile removed.");
            return new(true,"Disconnected; temporary Windows profile removed.",new(VpnState.Disconnected,"Disconnected"));
        }
        catch(Exception ex) when(ex is not OperationCanceledException) {return Fail(ex.Message,VpnStage.Disconnect);}
        finally {gate.Release();}
    }
    public override async Task<VpnStatus> GetStatusAsync(VpnProfile profile,CancellationToken cancellationToken=default)
    {
        if(!OperatingSystem.IsWindows()) return new(VpnState.Unknown,"Windows native VPN is unavailable on this OS.");
        try
        {
            var result=await ProviderEnvironment.PowerShellAsync("$v=Get-VpnConnection | Where-Object Name -eq $d.Name; if(!$v){@{State='Disconnected';Ip=''}|ConvertTo-Json -Compress}else{$ip=Get-NetIPAddress -InterfaceAlias $d.Name -ErrorAction SilentlyContinue | Where-Object AddressFamily -eq 'IPv4' | Select-Object -First 1 -ExpandProperty IPAddress; @{State=[string]$v.ConnectionStatus;Ip=[string]$ip}|ConvertTo-Json -Compress}",new {Name=ConnectionName(profile)},cancellationToken);
            if(result.ExitCode!=0) return new(VpnState.Unknown,Redactor.Redact(result.StandardError));
            using var json=JsonDocument.Parse(result.StandardOutput); var state=json.RootElement.GetProperty("State").GetString(); var ip=json.RootElement.GetProperty("Ip").GetString();
            var actual=state switch {"Connected"=>VpnState.Connected,"Disconnected"=>VpnState.Disconnected,"Connecting"=>VpnState.Connecting,_=>VpnState.Unknown};
            return new(actual,"Windows reports "+state,ip,ConnectionName(profile),connectedSince.TryGetValue(profile.Id,out var since)?since:null);
        }
        catch(Exception ex) when(ex is not OperationCanceledException) {return new(VpnState.Unknown,Redactor.Redact(ex.Message));}
    }
}

internal static class RasNative
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct DialParameters
    {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=257)] public string Entry;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=129)] public string Phone;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=129)] public string Callback;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=257)] public string User;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=257)] public string Password;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=16)] public string Domain;
        public uint SubEntry; public UIntPtr CallbackId; public uint InterfaceIndex; public IntPtr EncryptedPassword;
    }
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,DialCallback> ActiveCallbacks = new();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void DialCallback(uint message,uint state,uint error);
    internal static async Task<uint> DialAsync(string name,string user,string password,string domain,Action<uint,uint> progress,CancellationToken token)
    {
        var completion=new TaskCompletionSource<uint>(TaskCreationOptions.RunContinuationsAsynchronously);
        DialCallback callback=(_,state,error)=>{ try{progress(state,error);}catch{/* Never let event subscriber exceptions cross native boundary. */} if(error!=0||state==0x2000)completion.TrySetResult(error);else if(state==0x2001)completion.TrySetResult(628); };
        ActiveCallbacks[name]=callback;
        var parameters=new DialParameters {Size=(uint)Marshal.SizeOf<DialParameters>(),Entry=name,Phone="",Callback="",User=user,Password=password,Domain=domain};
        IntPtr connection=IntPtr.Zero; var succeeded=false;
        try
        {
            var error=RasDial(IntPtr.Zero,null,ref parameters,0,callback,out connection);
            if(error!=0) return error;
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(120));
            var result=await completion.Task.WaitAsync(timeout.Token); succeeded=result==0; return result;
        }
        finally
        {
            parameters.Password="";
            if(!succeeded) { if(connection!=IntPtr.Zero) RasHangUp(connection); await ReleaseCallbackAsync(name); }
            GC.KeepAlive(callback);
        }
    }
    // Microsoft documents a 3-second post-RasHangUp delay as a safe resource-release boundary.
    // Successful callback delegates remain rooted until the named app-owned connection is disconnected.
    internal static async Task ReleaseCallbackAsync(string name)
    {
        if (!ActiveCallbacks.TryGetValue(name, out var callback)) return;
        await Task.Delay(3000, CancellationToken.None);
        ActiveCallbacks.TryRemove(new KeyValuePair<string,DialCallback>(name, callback));
        GC.KeepAlive(callback);
    }
    internal static string Error(uint error) {var text=new System.Text.StringBuilder(1024);return RasGetErrorString(error,text,1024)==0?text.ToString():"Consult Windows Remote Access error documentation";}
    [DllImport("rasapi32.dll",EntryPoint="RasDialW",CharSet=CharSet.Unicode)] private static extern uint RasDial(IntPtr extensions,string? phonebook,ref DialParameters parameters,uint notifierType,DialCallback notifier,out IntPtr connection);
    [DllImport("rasapi32.dll",EntryPoint="RasHangUpW")] private static extern uint RasHangUp(IntPtr connection);
    [DllImport("rasapi32.dll",EntryPoint="RasGetErrorStringW",CharSet=CharSet.Unicode)] private static extern uint RasGetErrorString(uint error,System.Text.StringBuilder text,uint size);
}
