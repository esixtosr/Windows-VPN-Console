using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using CNIT455.VPN.Core;

namespace CNIT455.VPN.Providers;
public sealed record WireGuardKeyPair(string PublicKey,string PrivateKey);
public sealed class WireGuardProvider(SecretRedactor? redactor=null) : VpnProviderBase(redactor)
{
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly Dictionary<Guid,string> runtimeDirectories=[];
    private CancellationTokenSource? logsCancellation;
    private Process? logProcess;
    public override string Id=>"wireguard";
    private static string TunnelName(VpnProfile profile)=>"cnit455-"+profile.Id.ToString("N")[..24];
    private static string? Executable=>ProviderEnvironment.FindProgram(@"WireGuard\wireguard.exe");
    private static string? WgExecutable=>ProviderEnvironment.FindProgram(@"WireGuard\wg.exe");
    public override Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken=default)=>Task.FromResult(new ProviderCapabilities(Id,"WireGuard for Windows",[VpnProtocol.WireGuard],[AuthenticationMode.PreSharedKey,AuthenticationMode.ProviderDefault],true,true,true,true,"Requires official WireGuard for Windows. Keypair always required; PSK is an additional peer secret. Connected requires a recent observed handshake. Installed tunnel services survive app exit; disconnect to remove them."));
    public override Task<DependencyInfo> DetectInstallation(CancellationToken cancellationToken=default)=>Task.FromResult(ProviderEnvironment.Dependency(Id,"WireGuard",Executable,"Official wireguard.exe + wg.exe; administrator required. "+(WgExecutable is null?"wg.exe is missing.":"wg.exe found."),"https://www.wireguard.com/install/","winget install --id WireGuard.WireGuard --exact"));
    public override IReadOnlyList<ValidationIssue> ValidateProfile(VpnProfile profile)
    {
        var issues=base.ValidateProfile(profile).ToList();
        if(profile.Protocol!=VpnProtocol.WireGuard)issues.Add(new("Protocol","Select WireGuard protocol."));
        if(!string.IsNullOrWhiteSpace(profile.ImportedConfigPath))
        {
            if(!File.Exists(profile.ImportedConfigPath))issues.Add(new("ImportedConfigPath","WireGuard configuration file is missing."));
            else if(new FileInfo(profile.ImportedConfigPath).Length>1024*1024)issues.Add(new("ImportedConfigPath","WireGuard profile exceeds 1 MiB."));
            else issues.AddRange(ValidateConfig(File.ReadAllText(profile.ImportedConfigPath)));
        }
        else
        {
            if(!IsKey(profile.PeerPublicKey))issues.Add(new("PeerPublicKey","Enter a 32-byte base64 peer public key."));
            if(string.IsNullOrWhiteSpace(profile.TunnelAddress))issues.Add(new("TunnelAddress","Enter the assigned tunnel address with CIDR prefix."));
        }
        return issues;
    }
    public static IReadOnlyList<ValidationIssue> ValidateConfig(string config)
    {
        var issues=new List<ValidationIssue>();string section="";var interfaces=0;var peers=0;
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var interfaceKeys=new HashSet<string>(["PrivateKey","Address","DNS","ListenPort","MTU"],StringComparer.OrdinalIgnoreCase);
        var peerKeys=new HashSet<string>(["PublicKey","PresharedKey","AllowedIPs","Endpoint","PersistentKeepalive"],StringComparer.OrdinalIgnoreCase);
        void CheckSection()
        {
            if(section.Equals("[Interface]",StringComparison.OrdinalIgnoreCase) && (!seen.Contains("PrivateKey") || !seen.Contains("Address")))issues.Add(new("Config","The interface requires a private key and tunnel address."));
            if(section.Equals("[Peer]",StringComparison.OrdinalIgnoreCase) && (!seen.Contains("PublicKey") || !seen.Contains("AllowedIPs")))issues.Add(new("Config","Each peer requires a public key and AllowedIPs."));
        }
        foreach(var raw in config.Split('\n'))
        {
            var line=raw.Split('#',2)[0].Trim().TrimStart('\ufeff');if(line.Length==0)continue;
            if(line.StartsWith('['))
            {CheckSection();seen.Clear();section=line;if(line.Equals("[Interface]",StringComparison.OrdinalIgnoreCase))interfaces++;else if(line.Equals("[Peer]",StringComparison.OrdinalIgnoreCase))peers++;else issues.Add(new("Config","Unsupported WireGuard section."));continue;}
            var equals=line.IndexOf('=');if(equals<1){issues.Add(new("Config","Malformed WireGuard directive."));continue;}
            var key=line[..equals].Trim();var value=line[(equals+1)..].Trim();var inInterface=section.Equals("[Interface]",StringComparison.OrdinalIgnoreCase);
            if(section.Length==0 || !(inInterface?interfaceKeys:peerKeys).Contains(key))issues.Add(new("Config","Unsafe or unsupported WireGuard directive: "+key));
            if(!seen.Add(key))issues.Add(new("Config","Duplicate WireGuard directive: "+key));
            if(key.EndsWith("Key",StringComparison.OrdinalIgnoreCase)&&!IsKey(value))issues.Add(new("Config","Invalid base64 WireGuard key."));
            if(key.Equals("Address",StringComparison.OrdinalIgnoreCase)||key.Equals("AllowedIPs",StringComparison.OrdinalIgnoreCase))
                if(value.Split(',').Any(v=>!IsIpPrefix(v.Trim())))issues.Add(new("Config","Invalid WireGuard address/prefix or AllowedIPs."));
            if(key.Equals("Endpoint",StringComparison.OrdinalIgnoreCase)&&!IsEndpoint(value))issues.Add(new("Config","Endpoint must be host:port or [IPv6]:port with a port of 1–65535."));
            if(key.Equals("DNS",StringComparison.OrdinalIgnoreCase)&&value.Split(',').Any(v=>!System.Net.IPAddress.TryParse(v.Trim(),out _)&&!ProfileValidator.IsHost(v.Trim())))issues.Add(new("Config","Invalid WireGuard DNS address or search suffix."));
            if(key.Equals("ListenPort",StringComparison.OrdinalIgnoreCase)||key.Equals("PersistentKeepalive",StringComparison.OrdinalIgnoreCase))
                if(!ushort.TryParse(value,out _))issues.Add(new("Config","WireGuard ports and keepalive must be integers from 0–65535."));
            if(key.Equals("MTU",StringComparison.OrdinalIgnoreCase)&&(!int.TryParse(value,out var mtu)||mtu is <576 or >65535))issues.Add(new("Config","WireGuard MTU must be 576–65535."));
        }
        CheckSection();
        if(interfaces!=1||peers<1)issues.Add(new("Config","Expected one [Interface] and at least one [Peer]."));
        return issues;
    }
    private static bool IsIpPrefix(string value)
    {
        var parts=value.Split('/');return parts.Length==2&&System.Net.IPAddress.TryParse(parts[0],out var address)&&int.TryParse(parts[1],out var prefix)&&prefix>=0&&prefix<=(address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork?32:128);
    }
    private static bool IsEndpoint(string value)
    {
        var colon=value.LastIndexOf(':');if(colon<1||!ushort.TryParse(value[(colon+1)..],out var port)||port==0)return false;
        var host=value[..colon];return host.StartsWith('[')&&host.EndsWith(']')?System.Net.IPAddress.TryParse(host[1..^1],out var address)&&address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6:ProfileValidator.IsHost(host);
    }
    private static bool IsKey(string value){try{return Convert.FromBase64String(value).Length==32;}catch{return false;}}
    public async Task<WireGuardKeyPair> GenerateKeyPairAsync(CancellationToken cancellationToken=default)
    {
        if(WgExecutable is not {} executable)throw new InvalidOperationException("Install official WireGuard for Windows to generate keys with wg.exe.");
        var generated=await SafeProcess.RunAsync(executable,["genkey"],cancellationToken:cancellationToken);
        if(generated.ExitCode!=0)throw new InvalidOperationException("wg genkey failed.");
        var privateKey=generated.StandardOutput.Trim();if(!IsKey(privateKey))throw new InvalidDataException("wg genkey did not return a valid key.");Redactor.RegisterSecret(privateKey);
        var derived=await SafeProcess.RunAsync(executable,["pubkey"],privateKey+"\n",cancellationToken);
        if(derived.ExitCode!=0||!IsKey(derived.StandardOutput.Trim()))throw new InvalidOperationException("wg pubkey failed.");
        return new(derived.StandardOutput.Trim(),privateKey);
    }
    public async Task<string> GeneratePskAsync(CancellationToken cancellationToken=default)
    {if(WgExecutable is not {} executable)throw new InvalidOperationException("Install WireGuard for Windows first.");var result=await SafeProcess.RunAsync(executable,["genpsk"],cancellationToken:cancellationToken);var key=result.StandardOutput.Trim();if(result.ExitCode!=0||!IsKey(key))throw new InvalidOperationException("wg genpsk failed.");Redactor.RegisterSecret(key);return key;}
    private string BuildConfig(VpnProfile profile,VpnSecrets secrets)
    {
        if(!string.IsNullOrWhiteSpace(profile.ImportedConfigPath))
        {
            var imported=File.ReadAllText(profile.ImportedConfigPath);
            if(profile.IsLab)
            {
                var peers=imported.Split('\n').Count(line=>line.Trim().Equals("[Peer]",StringComparison.OrdinalIgnoreCase));
                var psks=imported.Split('\n').Count(line=>line.Split('=',2)[0].Trim().Equals("PresharedKey",StringComparison.OrdinalIgnoreCase));
                if(peers!=psks)throw new ArgumentException("Lab WireGuard requires a pre-shared key for every imported peer.");
            }
            return imported;
        }
        if(!IsKey(secrets.PrivateKey))throw new ArgumentException("Enter a valid WireGuard private key.");
        if(profile.IsLab&&!IsKey(secrets.Psk))throw new ArgumentException("Lab WireGuard requires an additional peer pre-shared key.");
        if(!string.IsNullOrWhiteSpace(secrets.Psk)&&!IsKey(secrets.Psk))throw new ArgumentException("WireGuard PSK must be a 32-byte base64 key.");
        foreach(var value in new[]{profile.Gateway,profile.TunnelAddress,profile.Dns,profile.PeerPublicKey}.Concat(profile.PermittedNetworks)) if(value.IndexOfAny(['\r','\n','\0'])>=0)throw new ArgumentException("WireGuard fields must be single-line values.");
        var endpoint=profile.Gateway.Contains(':')&&!profile.Gateway.StartsWith('[')?"["+profile.Gateway+"]":profile.Gateway;
        var text=new StringBuilder().AppendLine("[Interface]").AppendLine("PrivateKey = "+secrets.PrivateKey).AppendLine("Address = "+profile.TunnelAddress);
        if(!string.IsNullOrWhiteSpace(profile.Dns))text.AppendLine("DNS = "+profile.Dns);
        if(profile.ListenPort>0)text.AppendLine("ListenPort = "+profile.ListenPort);
        text.AppendLine().AppendLine("[Peer]").AppendLine("PublicKey = "+profile.PeerPublicKey);
        if(!string.IsNullOrWhiteSpace(secrets.Psk))text.AppendLine("PresharedKey = "+secrets.Psk);
        text.AppendLine("Endpoint = "+endpoint+":"+(profile.Port>0?profile.Port:51820));
        text.AppendLine("AllowedIPs = "+(profile.TunnelMode==TunnelMode.Full?"0.0.0.0/0, ::/0":string.Join(", ",profile.PermittedNetworks))).AppendLine("PersistentKeepalive = 25");return text.ToString();
    }
    public override async Task<ProviderResult> ConnectAsync(VpnProfile profile,VpnSecrets secrets,CancellationToken cancellationToken=default)
    {
        Register(secrets);if(Guard(profile,true) is {} rejected)return rejected;
        if(Executable is not {} executable||WgExecutable is null)return Fail("Install the official WireGuard client and wg.exe first.");
        await gate.WaitAsync(cancellationToken);string? directory=null;
        try
        {
            var config=BuildConfig(profile,secrets);var errors=ValidateConfig(config);if(errors.Count>0)return Fail(string.Join("; ",errors.Select(x=>x.Message)));
            foreach(var line in config.Split('\n')){var pair=line.Split('=',2);if(pair.Length==2&&(pair[0].Trim().Equals("PrivateKey",StringComparison.OrdinalIgnoreCase)||pair[0].Trim().Equals("PresharedKey",StringComparison.OrdinalIgnoreCase)))Redactor.RegisterSecret(pair[1].Trim());}
            var current=await GetStatusAsync(profile,cancellationToken);if(current.State is VpnState.Connected or VpnState.Connecting)return Fail("This app-owned tunnel is already installed. Disconnect before connecting again.");
            directory=PrivateRuntimeFiles.CreateDirectory();var name=TunnelName(profile);var path=Path.Combine(directory,name+".conf.dpapi");var clear=Encoding.UTF8.GetBytes(config);
            try{await File.WriteAllBytesAsync(path,PrivateRuntimeFiles.ProtectForMachine(clear,name),cancellationToken);}finally{CryptographicOperations.ZeroMemory(clear);}
            var result=await SafeProcess.RunAsync(executable,["/installtunnelservice",path],cancellationToken:cancellationToken);
            if(result.ExitCode!=0){PrivateRuntimeFiles.Delete(directory);directory=null;return Fail("WireGuard service installation failed: "+result.StandardError+result.StandardOutput);}
            runtimeDirectories[profile.Id]=directory;directory=null;
            Log(VpnStage.Tunnel,"App-owned WireGuard tunnel service installed. Installation does not prove a handshake or working routes.");
            await StartLogStreamAsync(profile,cancellationToken);var status=await GetStatusAsync(profile,cancellationToken);
            return new(true,"Tunnel service installed. "+status.Message,status);
        }
        catch(Exception ex)when(ex is not OperationCanceledException){return Fail(ex.Message);}
        finally{if(directory is not null)PrivateRuntimeFiles.Delete(directory);gate.Release();}
    }
    public override async Task<ProviderResult> DisconnectAsync(VpnProfile profile,CancellationToken cancellationToken=default)
    {
        if(!OperatingSystem.IsWindows()||!ProviderEnvironment.IsAdministrator)return Fail("WireGuard removal requires Windows administrator rights.");
        if(Executable is not {} executable)return Fail("WireGuard executable is missing.");
        await gate.WaitAsync(cancellationToken);
        try
        {
            var result=await SafeProcess.RunAsync(executable,["/uninstalltunnelservice",TunnelName(profile)],cancellationToken:cancellationToken);
            if(result.ExitCode!=0)return Fail("Could not remove WireGuard tunnel service: "+result.StandardError+result.StandardOutput,VpnStage.Disconnect);
            await StopLogStreamAsync(cancellationToken);
            if(runtimeDirectories.Remove(profile.Id,out var directory))PrivateRuntimeFiles.Delete(directory);
            var status=await GetStatusAsync(profile,cancellationToken);if(status.State!=VpnState.Disconnected)return Fail("WireGuard removal requested but service removal could not be confirmed.",VpnStage.Disconnect);
            return new(true,"WireGuard tunnel removed.",status);
        }
        catch(Exception ex)when(ex is not OperationCanceledException){return Fail(ex.Message,VpnStage.Disconnect);}
        finally{gate.Release();}
    }
    public override async Task<VpnStatus> GetStatusAsync(VpnProfile profile,CancellationToken cancellationToken=default)
    {
        if(!OperatingSystem.IsWindows()||WgExecutable is not {} wg)return new(VpnState.Unknown,"WireGuard for Windows is unavailable.");
        try
        {
            var name=TunnelName(profile);var service=await SafeProcess.RunAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"sc.exe"),["query","WireGuardTunnel$"+name],cancellationToken:cancellationToken);
            if(service.ExitCode==1060)return new(VpnState.Disconnected,"App-owned WireGuard tunnel service is not installed.");
            if(service.ExitCode!=0)return new(VpnState.Unknown,"Unable to query WireGuard service: "+Redactor.Redact(service.StandardError));
            var handshakes=await SafeProcess.RunAsync(wg,["show",name,"latest-handshakes"],cancellationToken:cancellationToken);
            if(handshakes.ExitCode!=0)return new(VpnState.Unknown,"WireGuard service exists, but tunnel state cannot be read. Check administrator rights and engine logs.",InterfaceName:name);
            var times=handshakes.StandardOutput.Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Split('\t').Last()).Select(x=>long.TryParse(x,out var n)?n:0).ToArray();
            var last=times.DefaultIfEmpty(0).Max();var age=DateTimeOffset.UtcNow.ToUnixTimeSeconds()-last;
            var state=last==0?VpnState.Connecting:age is >=0 and <=180?VpnState.Connected:VpnState.Unknown;
            var summary=last==0?"No handshake observed yet. Generate permitted traffic, then inspect peer/firewall settings.":age is >=0 and <=180?"Recent WireGuard handshake observed; verify required routes and traffic separately.":"Last handshake is stale; an idle peer is not proof of failure. Generate traffic to recheck.";
            var display=await SafeProcess.RunAsync(wg,["show",name],cancellationToken:cancellationToken); // Human-readable output hides private and preshared keys; never request 'dump'.
            var ip=NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(x=>x.Name==name)?.GetIPProperties().UnicastAddresses.FirstOrDefault()?.Address.ToString();
            return new(state,summary,ip,name,Details:new Dictionary<string,string>{{"Latest handshake",last>0?DateTimeOffset.FromUnixTimeSeconds(last).ToString("O"):"Never"},{"WireGuard",Redactor.Redact(display.StandardOutput)}});
        }
        catch(Exception ex)when(ex is not OperationCanceledException){return new(VpnState.Unknown,Redactor.Redact(ex.Message));}
    }
    public override async Task StartLogStreamAsync(VpnProfile profile,CancellationToken cancellationToken=default)
    {
        await StopLogStreamAsync(cancellationToken);if(Executable is not {} executable)return;
        logsCancellation=new();var token=logsCancellation.Token;var start=new ProcessStartInfo(executable){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};start.ArgumentList.Add("/dumplog");start.ArgumentList.Add("/tail");
        logProcess=Process.Start(start);if(logProcess is null)return;var process=logProcess;
        _=Task.Run(async()=>{try{await Task.WhenAll(Pump(process.StandardOutput),Pump(process.StandardError));}catch(OperationCanceledException){}catch(Exception ex){Log(VpnStage.Tunnel,"WireGuard log stream ended: "+ex.Message,LogSeverity.Warning);}
            async Task Pump(StreamReader reader){while(await reader.ReadLineAsync(token) is {} line)if(line.Contains(TunnelName(profile),StringComparison.OrdinalIgnoreCase))Log(VpnStage.Tunnel,line);}});
    }
    public override Task StopLogStreamAsync(CancellationToken cancellationToken=default)
    {logsCancellation?.Cancel();logsCancellation?.Dispose();logsCancellation=null;if(logProcess is {} process){try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}process.Dispose();logProcess=null;}return Task.CompletedTask;}
}
