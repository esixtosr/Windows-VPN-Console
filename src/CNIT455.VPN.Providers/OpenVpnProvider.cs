using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using CNIT455.VPN.Core;

namespace CNIT455.VPN.Providers;

/// <summary>Runs only app-owned OpenVPN processes with a validated self-contained configuration.</summary>
public sealed class OpenVpnProvider(SecretRedactor? redactor=null) : VpnProviderBase(redactor)
{
    private sealed class Session
    {
        internal required Process Process; internal required string Directory; internal required string Username; internal required string Password;
        internal TcpClient? Client; internal StreamWriter? Writer; internal CancellationTokenSource Lifetime=new();internal SemaphoreSlim Writes=new(1,1);
        internal VpnStatus Status=new(VpnState.Connecting,"OpenVPN starting");internal Task? ReaderTask;
    }
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid,Session> sessions=new();
    private readonly SemaphoreSlim gate=new(1,1);
    public override string Id=>"openvpn";
    private static string? Executable=>ProviderEnvironment.FindProgram(@"OpenVPN\bin\openvpn.exe");
    public override Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken=default)=>Task.FromResult(new ProviderCapabilities(Id,"OpenVPN Community",[VpnProtocol.OpenVpn],[AuthenticationMode.Certificate,AuthenticationMode.CertificateAndUsername,AuthenticationMode.UsernamePassword,AuthenticationMode.ProviderDefault],true,true,true,true,"Requires OpenVPN Community 2.x and a self-contained .ovpn export. Unknown directives, scripts, plugins, external files, static keys and encrypted private keys are rejected. Uses authenticated loopback management with process-owner verification. Imported profile/server defines routes; Diagnostics validates the requested split/full policy."));
    public override Task<DependencyInfo> DetectInstallation(CancellationToken cancellationToken=default)=>Task.FromResult(ProviderEnvironment.Dependency(Id,"OpenVPN Community",Executable,"Uses openvpn.exe (Community 2.x), not OpenVPN Connect. TAP/Wintun/DCO driver must be installed by the official package.","https://openvpn.net/community-downloads/","winget install --id OpenVPNTechnologies.OpenVPN --exact"));
    public override IReadOnlyList<ValidationIssue> ValidateProfile(VpnProfile profile)
    {
        var issues=base.ValidateProfile(profile).ToList();
        if(profile.Protocol!=VpnProtocol.OpenVpn)issues.Add(new("Protocol","Select OpenVPN protocol."));
        if(profile.Authentication is not (AuthenticationMode.Certificate or AuthenticationMode.CertificateAndUsername or AuthenticationMode.UsernamePassword or AuthenticationMode.ProviderDefault))issues.Add(new("Authentication","OpenVPN TLS requires certificate, username/password, or provider-default authentication."));
        if(!File.Exists(profile.ImportedConfigPath))issues.Add(new("ImportedConfigPath","Import a self-contained .ovpn profile first."));
        else if(new FileInfo(profile.ImportedConfigPath).Length>4*1024*1024)issues.Add(new("ImportedConfigPath","OpenVPN profile exceeds 4 MiB."));
        else
        {
            var config=File.ReadAllText(profile.ImportedConfigPath);issues.AddRange(ValidateConfig(config));
            if(profile.IsLab)
            {
                var directives=Directives(config).ToArray();
                if(!directives.Any(x=>x.Name=="auth-user-pass"))issues.Add(new("Authentication","Lab OpenVPN requires username authentication through pfSense LDAP; the imported profile needs auth-user-pass."));
                if(directives.Any(x=>(x.Name=="proto"&&x.Argument.StartsWith("tcp",StringComparison.OrdinalIgnoreCase)) || (x.Name=="remote"&&x.Argument.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Skip(2).Any(v=>v.StartsWith("tcp",StringComparison.OrdinalIgnoreCase)))))issues.Add(new("Transport","Lab OpenVPN requires UDP. Import a UDP client profile."));
            }
        }
        return issues;
    }
    private static IEnumerable<(string Name,string Argument)> Directives(string config)
    {
        bool block=false;
        foreach(var raw in config.Split('\n'))
        {
            var line=raw.Trim().TrimStart('\ufeff');if(line.Length==0||line.StartsWith('#')||line.StartsWith(';'))continue;
            if(line.StartsWith("</")){block=false;continue;}if(line.StartsWith('<')){block=true;continue;}if(block)continue;
            var pair=line.Split((char[]?)null,2,StringSplitOptions.RemoveEmptyEntries);yield return (pair[0].TrimStart('-').ToLowerInvariant(),pair.Length>1?pair[1]:"");
        }
    }
    private static readonly HashSet<string> AllowedDirectives=new(StringComparer.OrdinalIgnoreCase)
    {
        "client","tls-client","dev","dev-type","proto","remote","remote-random","resolv-retry","nobind","persist-key","persist-tun","auth-user-pass","auth-nocache",
        "remote-cert-tls","verify-x509-name","peer-fingerprint","cipher","data-ciphers","data-ciphers-fallback","auth","tls-cipher","tls-ciphersuites","tls-version-min","tls-version-max","key-direction",
        "route","route-ipv6","route-gateway","route-metric","route-delay","route-nopull","redirect-gateway","dhcp-option","pull","pull-filter","ping","ping-restart","keepalive","connect-retry","connect-retry-max","connect-timeout","server-poll-timeout",
        "verb","mute","reneg-sec","explicit-exit-notify","mssfix","tun-mtu","sndbuf","rcvbuf","fast-io","float","allow-pull-fqdn","block-outside-dns","block-ipv6","register-dns","windows-driver","disable-dco"
    };
    private static readonly HashSet<string> InlineBlocks=new(["ca","cert","key","tls-auth","tls-crypt","tls-crypt-v2","extra-certs"],StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyList<ValidationIssue> ValidateConfig(string config)
    {
        var issues=new List<ValidationIssue>();string? block=null;var remote=false;var client=false;var ca=false;
        foreach(var raw in config.Split('\n'))
        {
            var line=raw.Trim().TrimStart('\ufeff');if(line.Length==0||line.StartsWith('#')||line.StartsWith(';'))continue;
            if(block is not null)
            {if(line.Equals("</"+block+">",StringComparison.OrdinalIgnoreCase))block=null;else if(line.Contains("ENCRYPTED",StringComparison.OrdinalIgnoreCase))issues.Add(new("Config","Encrypted private keys require a separate provider credential UI; use the vendor client for this profile."));continue;}
            if(line.StartsWith('<'))
            {var tag=line.Trim('<','>',' ').ToLowerInvariant();if(!InlineBlocks.Contains(tag))issues.Add(new("Config","Unsupported inline OpenVPN block."));else {block=tag;if(tag=="ca")ca=true;}continue;}
            var pair=line.Split((char[]?)null,2,StringSplitOptions.RemoveEmptyEntries);var directive=pair[0].TrimStart('-');var argument=pair.Length>1?pair[1]:"";
            if(new[]{"cipher","data-ciphers","data-ciphers-fallback"}.Contains(directive,StringComparer.OrdinalIgnoreCase) && argument.Split(':',' ','\t','"','\'').Any(x=>x.Equals("none",StringComparison.OrdinalIgnoreCase)))issues.Add(new("Config","Unencrypted OpenVPN cipher selection is not supported."));
            if(!AllowedDirectives.Contains(directive))issues.Add(new("Config","Unsafe or unsupported OpenVPN directive: "+directive));
            if(directive.Equals("auth-user-pass",StringComparison.OrdinalIgnoreCase)&&argument.Length>0)issues.Add(new("Config","auth-user-pass must not reference a plaintext credential file."));
            if(directive.Equals("dev",StringComparison.OrdinalIgnoreCase)&&!argument.Equals("tun",StringComparison.OrdinalIgnoreCase))issues.Add(new("Config","Only a routed tun device is supported."));
            if(directive.Equals("remote",StringComparison.OrdinalIgnoreCase))remote=true;
            if(directive.Equals("client",StringComparison.OrdinalIgnoreCase)||directive.Equals("tls-client",StringComparison.OrdinalIgnoreCase))client=true;
            if(directive.Equals("peer-fingerprint",StringComparison.OrdinalIgnoreCase))ca=true;
            if(line.Contains('\0')||line.EndsWith('\\'))issues.Add(new("Config","NUL bytes and line continuations are unsupported."));
        }
        if(block is not null)issues.Add(new("Config","Unclosed inline OpenVPN block."));
        if(!remote||!client||!ca)issues.Add(new("Config","A TLS client profile needs remote, client/tls-client and an inline CA or peer fingerprint."));
        return issues;
    }
    private static void RegisterInlineSecrets(string config,SecretRedactor redactor)
    {
        string? block=null;var secret=new StringBuilder();
        foreach(var raw in config.Split('\n'))
        {
            var line=raw.Trim();
            if(block is null){if(line.ToLowerInvariant() is "<key>" or "<tls-auth>" or "<tls-crypt>" or "<tls-crypt-v2>"){block=line[1..^1];secret.Clear();}}
            else if(line.Equals("</"+block+">",StringComparison.OrdinalIgnoreCase)){redactor.RegisterSecret(secret.ToString().Trim());block=null;}
            else{secret.AppendLine(raw);if(line.Length>12&&!line.StartsWith('-'))redactor.RegisterSecret(line);}
        }
    }
    public override async Task<ProviderResult> ConnectAsync(VpnProfile profile,VpnSecrets secrets,CancellationToken cancellationToken=default)
    {
        Register(secrets);if(Guard(profile,true) is {} rejected)return rejected;
        if(Executable is not {} executable)return Fail("Install OpenVPN Community 2.x first.");
        if(profile.Username.IndexOfAny(['\r','\n','\0'])>=0||secrets.Password.IndexOfAny(['\r','\n','\0'])>=0)return Fail("OpenVPN management credentials cannot contain line breaks or NUL bytes.");
        await gate.WaitAsync(cancellationToken);Session? session=null;string? directory=null;
        try
        {
            if(sessions.TryGetValue(profile.Id,out var active)&&!active.Process.HasExited)return Fail("This profile already has an app-owned OpenVPN process.");
            if(active is not null){await CleanupAsync(active);sessions.TryRemove(profile.Id,out _);}
            var config=await File.ReadAllTextAsync(profile.ImportedConfigPath,cancellationToken);var errors=ValidateConfig(config);if(errors.Count>0)return Fail(string.Join("; ",errors.Select(x=>x.Message)));
            RegisterInlineSecrets(config,Redactor);directory=PrivateRuntimeFiles.CreateDirectory();
            var configPath=Path.Combine(directory,"client.ovpn");await File.WriteAllTextAsync(configPath,config,new UTF8Encoding(false),cancellationToken);
            var managementPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));Redactor.RegisterSecret(managementPassword);
            var passwordPath=Path.Combine(directory,"management-password");await File.WriteAllTextAsync(passwordPath,managementPassword+"\n",new UTF8Encoding(false),cancellationToken);
            var reservation=new TcpListener(IPAddress.Loopback,0);reservation.Start();var port=((IPEndPoint)reservation.LocalEndpoint).Port;reservation.Stop();
            var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=directory};
            foreach(var arg in new[]{"--config",configPath,"--management","127.0.0.1",port.ToString(),passwordPath,"--management-query-passwords","--management-hold","--management-forget-disconnect","--management-signal","--script-security","1","--auth-nocache","--remote-cert-tls","server","--tls-version-min","1.2","--verb","3"})start.ArgumentList.Add(arg);
            var process=Process.Start(start)??throw new InvalidOperationException("OpenVPN did not start.");
            session=new(){Process=process,Directory=directory,Username=profile.Username,Password=secrets.Password};sessions[profile.Id]=session;directory=null;
            var owned=session;
            _=PumpOutputAsync(process.StandardOutput,owned);_=PumpOutputAsync(process.StandardError,owned);
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromSeconds(20));
            TcpClient? client=null;
            while(client is null)
            {
                deadline.Token.ThrowIfCancellationRequested();if(process.HasExited)throw new InvalidOperationException("OpenVPN exited before opening its management channel. Inspect engine logs and the imported profile.");
                var attempt=new TcpClient();
                try{await attempt.ConnectAsync(IPAddress.Loopback,port,deadline.Token);if(!TcpOwner.IsOwnedLoopbackPort(port,process.Id))throw new InvalidOperationException("Management listener does not belong to the launched OpenVPN process; credentials were not sent.");client=attempt;}
                catch(SocketException){attempt.Dispose();await Task.Delay(150,deadline.Token);}
                catch{attempt.Dispose();throw;}
            }
            owned.Client=client;var stream=client.GetStream();var reader=new StreamReader(stream,new UTF8Encoding(false),false,4096,true);owned.Writer=new StreamWriter(stream,new UTF8Encoding(false),4096,true){AutoFlush=true,NewLine="\n"};
            // OpenVPN prompts without a newline. Read exactly through the prompt before sending the random channel password.
            var prompt=new StringBuilder();var one=new char[1];
            while(!prompt.ToString().EndsWith("ENTER PASSWORD:",StringComparison.Ordinal))
            {if(prompt.Length>4096||await reader.ReadAsync(one.AsMemory(),deadline.Token)==0)throw new InvalidOperationException("Unexpected OpenVPN management authentication prompt.");prompt.Append(one[0]);}
            await SendAsync(owned,managementPassword,deadline.Token);var authenticated=false;
            while(await reader.ReadLineAsync(deadline.Token) is {} line)
            {if(line.Contains("SUCCESS: password is correct",StringComparison.OrdinalIgnoreCase)){authenticated=true;break;}if(line.Contains("ERROR",StringComparison.OrdinalIgnoreCase))break;}
            if(!authenticated)throw new InvalidOperationException("OpenVPN management authentication failed.");
            File.Delete(passwordPath);owned.ReaderTask=ReadManagementAsync(owned,reader);
            await SendAsync(owned,"state on",cancellationToken);await SendAsync(owned,"log on all",cancellationToken);await SendAsync(owned,"hold release",cancellationToken);
            Log(VpnStage.Initialization,"Authenticated local OpenVPN management channel established; TLS negotiation is starting.");
            return new(true,"OpenVPN started. Wait for an observed CONNECTED state, then validate routes.",owned.Status);
        }
        catch(Exception ex)
        {
            if(session is not null){await CleanupAsync(session);sessions.TryRemove(profile.Id,out _);}if(directory is not null)PrivateRuntimeFiles.Delete(directory);
            if(ex is OperationCanceledException&&cancellationToken.IsCancellationRequested)throw;
            return Fail("OpenVPN startup failed: "+ex.Message);
        }
        finally{gate.Release();}
    }
    private async Task PumpOutputAsync(StreamReader reader,Session session)
    {try{while(await reader.ReadLineAsync(session.Lifetime.Token) is {} line)Log(VpnStage.Tunnel,line);}catch(OperationCanceledException){}catch(ObjectDisposedException){}catch(IOException ex){Log(VpnStage.Tunnel,ex.Message,LogSeverity.Warning);}}
    private async Task ReadManagementAsync(Session session,StreamReader reader)
    {
        try
        {
            while(await reader.ReadLineAsync(session.Lifetime.Token) is {} line)
            {
                if(line.StartsWith(">PASSWORD:Need 'Auth'",StringComparison.Ordinal))
                {
                    if(string.IsNullOrWhiteSpace(session.Username)||string.IsNullOrEmpty(session.Password)){session.Status=new(VpnState.Failed,"OpenVPN requested username/password. Enter credentials and reconnect.");await SendAsync(session,"signal SIGTERM",session.Lifetime.Token);continue;}
                    await SendAsync(session,"username \"Auth\" "+Quote(session.Username),session.Lifetime.Token);await SendAsync(session,"password \"Auth\" "+Quote(session.Password),session.Lifetime.Token);
                }
                else if(line.StartsWith(">PASSWORD:",StringComparison.Ordinal))
                {session.Status=new(VpnState.Failed,"OpenVPN authentication failed or needs an unsupported challenge. Check credentials or use the vendor client for challenge/MFA/key prompts.");Log(VpnStage.Authentication,session.Status.Message,LogSeverity.Error);await SendAsync(session,"signal SIGTERM",session.Lifetime.Token);}
                else if(line.StartsWith(">HOLD:",StringComparison.Ordinal))
                { await SendAsync(session,"hold release",session.Lifetime.Token); }
                else if(line.StartsWith(">STATE:",StringComparison.Ordinal))
                {
                    var fields=line[7..].Split(',');if(fields.Length<3)continue;var state=fields[1];
                    if(state=="EXITING" && session.Status.State==VpnState.Failed)continue;
                    session.Status=state switch {"CONNECTED" when fields[2]=="SUCCESS"=>new(VpnState.Connected,"OpenVPN reports CONNECTED,SUCCESS; verify routing separately.",fields.ElementAtOrDefault(3),"OpenVPN",DateTimeOffset.Now),"EXITING"=>new(VpnState.Disconnected,"OpenVPN exiting: "+Redactor.Redact(fields[2])),"RECONNECTING"=>new(VpnState.Connecting,"OpenVPN reconnecting: "+Redactor.Redact(fields[2])),_=>new(VpnState.Connecting,"OpenVPN state: "+Redactor.Redact(state))};
                    Log(state=="CONNECTED"?VpnStage.Tunnel:VpnStage.Authentication,session.Status.Message);
                }
                else if(line.StartsWith(">FATAL:",StringComparison.Ordinal)){session.Status=new(VpnState.Failed,Redactor.Redact(line[7..]));Log(VpnStage.Tunnel,line,LogSeverity.Error);}
                else if(line.StartsWith(">LOG:",StringComparison.Ordinal))Log(VpnStage.Tunnel,line);
            }
            if(session.Status.State is VpnState.Connected or VpnState.Connecting)session.Status=new(VpnState.Unknown,"Management channel closed; tunnel state is unconfirmed.");
        }
        catch(OperationCanceledException){}
        catch(Exception ex){session.Status=new(VpnState.Unknown,"Management channel failed: "+Redactor.Redact(ex.Message));Log(VpnStage.Tunnel,session.Status.Message,LogSeverity.Warning);}
        finally{reader.Dispose();}
    }
    private static string Quote(string value)=>"\""+value.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";
    private static async Task SendAsync(Session session,string command,CancellationToken token)
    {await session.Writes.WaitAsync(token);try{await session.Writer!.WriteLineAsync(command.AsMemory(),token);await session.Writer.FlushAsync(token);}finally{session.Writes.Release();}}
    public override async Task<ProviderResult> DisconnectAsync(VpnProfile profile,CancellationToken cancellationToken=default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if(!sessions.TryGetValue(profile.Id,out var session))return new(false,"No OpenVPN process owned by this app session exists. Use the owning OpenVPN client to disconnect.",new(VpnState.Unknown,"No app-owned session"));
            if(!session.Process.HasExited&&session.Writer is not null){try{await SendAsync(session,"signal SIGTERM",cancellationToken);await session.Process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(10),cancellationToken);}catch(TimeoutException){Log(VpnStage.Disconnect,"Graceful shutdown timed out; terminating only this app-owned OpenVPN process.",LogSeverity.Warning);}catch(IOException){}}
            await CleanupAsync(session);sessions.TryRemove(profile.Id,out _);Log(VpnStage.Disconnect,"App-owned OpenVPN process stopped; transient profile files removed.");
            return new(true,"Disconnected.",new(VpnState.Disconnected,"OpenVPN stopped"));
        }
        catch(Exception ex)when(ex is not OperationCanceledException){return Fail("OpenVPN disconnect failed: "+ex.Message,VpnStage.Disconnect);}
        finally{gate.Release();}
    }
    private static async Task CleanupAsync(Session session)
    {
        if(!session.Process.HasExited){session.Process.Kill();await session.Process.WaitForExitAsync();}
        session.Lifetime.Cancel();session.Client?.Dispose();if(session.ReaderTask is not null)await session.ReaderTask;
        session.Writer?.Dispose();session.Process.Dispose();session.Username=session.Password="";session.Lifetime.Dispose();PrivateRuntimeFiles.Delete(session.Directory);
    }
    /// <summary>Stop only engines launched by this instance and remove their transient credentials.</summary>
    public async Task ShutdownAsync(CancellationToken cancellationToken=default)
    {
        foreach(var id in sessions.Keys.ToArray())
            await DisconnectAsync(new VpnProfile { Id=id },cancellationToken);
    }
    public override Task<VpnStatus> GetStatusAsync(VpnProfile profile,CancellationToken cancellationToken=default)
    {
        if(!sessions.TryGetValue(profile.Id,out var session))return Task.FromResult(new VpnStatus(VpnState.Unknown,"No app-owned OpenVPN session. Other clients are not controlled or inferred."));
        try { if(session.Process.HasExited)return Task.FromResult(new VpnStatus(session.Process.ExitCode==0?VpnState.Disconnected:VpnState.Failed,$"OpenVPN process exited with code {session.Process.ExitCode}. "+session.Status.Message));
        return Task.FromResult(session.Status); } catch(InvalidOperationException) { return Task.FromResult(new VpnStatus(VpnState.Unknown,"OpenVPN session is closing.")); }
    }
    public override Task StopLogStreamAsync(CancellationToken cancellationToken=default)=>Task.CompletedTask; // Management reader is also required for authentication/state for the entire connection.
}

internal static class TcpOwner
{
    internal static bool IsOwnedLoopbackPort(int port,int processId)
    {
        var size=0;var error=GetExtendedTcpTable(IntPtr.Zero,ref size,false,2,5,0);if(error!=122)return false;
        var buffer=Marshal.AllocHGlobal(size);
        try
        {
            if(GetExtendedTcpTable(buffer,ref size,false,2,5,0)!=0)return false;
            var count=Marshal.ReadInt32(buffer);for(var i=0;i<count;i++)
            {var row=IntPtr.Add(buffer,4+i*24);var address=(uint)Marshal.ReadInt32(row,4);var encoded=(uint)Marshal.ReadInt32(row,8);var localPort=(int)(((encoded&255)<<8)|((encoded>>8)&255));var pid=Marshal.ReadInt32(row,20);if(localPort==port&&pid==processId&&address==0x0100007f)return true;}
            return false;
        }
        finally{Marshal.FreeHGlobal(buffer);}
    }
    [DllImport("iphlpapi.dll")] private static extern uint GetExtendedTcpTable(IntPtr table,ref int size,[MarshalAs(UnmanagedType.Bool)]bool order,uint family,uint tableClass,uint reserved);
}
