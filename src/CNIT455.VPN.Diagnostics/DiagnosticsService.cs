using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text.Json;
using CNIT455.VPN.Core;
namespace CNIT455.VPN.Diagnostics;
public sealed class DiagnosticsService(SecretRedactor redactor)
{
    public async Task<DiagnosticSnapshot> CollectAsync(VpnProfile profile,IVpnProvider provider,IEnumerable<VpnLogEvent> logs,string serverOutput="",CancellationToken cancellationToken=default)
    {
        var snapshot=new DiagnosticSnapshot { Profile=profile with {}, Logs=logs.TakeLast(1000).Select(l=>l with{Message=redactor.Redact(l.Message)}).ToList(),ServerOutput=redactor.Redact(serverOutput),IsAdministrator=IsAdministrator() };
        try{snapshot.Status=await provider.GetStatusAsync(profile,cancellationToken);snapshot.Dependencies.Add(await provider.DetectInstallation(cancellationToken));snapshot.ProviderDiagnostics=redactor.Redact(await provider.GetDiagnosticsAsync(profile,cancellationToken));}catch(Exception e) when(e is not OperationCanceledException){snapshot.CollectionNotes+="Provider query: "+redactor.Redact(e.Message)+"\n";}
        foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                var info=adapter.GetIPProperties();
                snapshot.Adapters.Add(new(info.GetIPv4Properties()?.Index??0,adapter.Name,adapter.Description,adapter.OperationalStatus.ToString(),info.UnicastAddresses.Select(x=>x.Address.ToString()).ToList(),info.DnsAddresses.Select(x=>x.ToString()).ToList(),info.GatewayAddresses.Select(x=>x.Address.ToString()).ToList()));
            }
            catch(NetworkInformationException){ }
        }
        if(OperatingSystem.IsWindows())
        {
            try
            {
                var script="$ErrorActionPreference='Stop'; Get-NetRoute -AddressFamily IPv4 | ForEach-Object { $r=$_; $i=Get-NetIPInterface -AddressFamily IPv4 -InterfaceIndex $r.InterfaceIndex -ErrorAction SilentlyContinue | Select-Object -First 1; [pscustomobject]@{ Destination=$r.DestinationPrefix; NextHop=$r.NextHop; InterfaceIndex=$r.InterfaceIndex; InterfaceAlias=$r.InterfaceAlias; RouteMetric=$r.RouteMetric; InterfaceMetric=$i.InterfaceMetric } } | ConvertTo-Json -Compress";
                var process=await RunPowerShell(script,cancellationToken);
                if(process.ExitCode==0&&!string.IsNullOrWhiteSpace(process.StandardOutput))
                {
                    using var json=JsonDocument.Parse(process.StandardOutput);
                    var rows=json.RootElement.ValueKind==JsonValueKind.Array?json.RootElement.EnumerateArray().ToArray():[json.RootElement];
                    foreach(var row in rows) snapshot.Routes.Add(new(row.GetProperty("Destination").GetString()??"",row.GetProperty("NextHop").GetString()??"",row.GetProperty("InterfaceIndex").GetInt32(),row.GetProperty("InterfaceAlias").GetString()??"",row.GetProperty("RouteMetric").GetInt32(),row.TryGetProperty("InterfaceMetric",out var metric)&&metric.ValueKind==JsonValueKind.Number?metric.GetInt32():0));
                }
                else snapshot.CollectionNotes+="Route collection: "+redactor.Redact(process.StandardError)+"\n";
                var events=await RunPowerShell("Get-WinEvent -FilterHashtable @{LogName='Application';ProviderName='RasClient';StartTime=(Get-Date).AddHours(-2)} -MaxEvents 20 -ErrorAction SilentlyContinue | Select-Object TimeCreated,Id,LevelDisplayName,Message | Format-List | Out-String -Width 180",cancellationToken);
                snapshot.SystemEvents=redactor.Redact(events.StandardOutput);
                var ipv6=await RunPowerShell("Get-NetRoute -AddressFamily IPv6 -ErrorAction SilentlyContinue | Select-Object DestinationPrefix,NextHop,InterfaceIndex,InterfaceAlias,RouteMetric | Format-Table -AutoSize | Out-String -Width 180",cancellationToken);
                snapshot.Ipv6Routes=redactor.Redact(ipv6.StandardOutput);
            }
            catch(Exception e) when(e is not OperationCanceledException){snapshot.CollectionNotes+="Windows diagnostics: "+redactor.Redact(e.Message)+"\n";}
        }
        else snapshot.CollectionNotes+="Windows route/event collection is unavailable on this operating system.\n";
        var tunnel=snapshot.Adapters.FirstOrDefault(a=> (!string.IsNullOrWhiteSpace(snapshot.Status.InterfaceName)&&a.Name.Equals(snapshot.Status.InterfaceName,StringComparison.OrdinalIgnoreCase))||(!string.IsNullOrWhiteSpace(snapshot.Status.TunnelIp)&&a.Addresses.Contains(snapshot.Status.TunnelIp)));
        // Provider IDs and adapter names alone are never treated as tunnel evidence.
        int? index=snapshot.Status.State==VpnState.Connected?tunnel?.Index:null;
        if(index.HasValue)snapshot.Routes=snapshot.Routes.Select(r=>r with{IsVpn=r.InterfaceIndex==index}).ToList();
        snapshot.Routing=RouteAnalyzer.Analyze(profile,snapshot.Routes,index);
        if(ProfileValidator.IsHost(profile.Gateway))
        {
            try {using var ping=new Ping();var reply=await ping.SendPingAsync(profile.Gateway,TimeSpan.FromSeconds(2),cancellationToken:cancellationToken);snapshot.GatewayReachable=reply.Status==IPStatus.Success?ResultState.Pass:ResultState.Unknown;}
            catch(PingException){snapshot.GatewayReachable=ResultState.Unknown;}
        }
        snapshot.Findings=TroubleshootingAnalyzer.Analyze(snapshot).ToList();
        return snapshot;
    }
    public static bool IsAdministrator()
    {
        if(!OperatingSystem.IsWindows())return false;
        using var identity=WindowsIdentity.GetCurrent();return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
    private static Task<ProcessResult> RunPowerShell(string script,CancellationToken ct)=>SafeProcess.RunAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"),["-NoLogo","-NoProfile","-NonInteractive","-Command",script],cancellationToken:ct);
}
