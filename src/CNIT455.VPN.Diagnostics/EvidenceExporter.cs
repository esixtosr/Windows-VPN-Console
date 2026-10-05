using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CNIT455.VPN.Core;
namespace CNIT455.VPN.Diagnostics;
public sealed class EvidenceExporter(SecretRedactor redactor)
{
    public async Task ExportAsync(string path,DiagnosticSnapshot snapshot,IEnumerable<CheckoffItem> checkoff)
    {
        if(!Path.GetExtension(path).Equals(".zip",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Evidence destination must be a ZIP.");
        var formatter=new DiagnosticFormatter(redactor);
        var entries=new Dictionary<string,string>
        {
            ["summary.txt"]=$"CNIT455 VPN Console {snapshot.AppVersion}\nCaptured {snapshot.CapturedAt:O}\nProfile: {snapshot.Profile.Name}\n{snapshot.Profile.Protocol} / {snapshot.Profile.ProviderId}\nTunnel: {snapshot.Status.State}\nRoute policy: {snapshot.Routing.Result}\nManual checkoff entries are user attestations, not automated measurements.\nCapture files are not automatically included because they can contain sensitive traffic.",
            ["diagnostics.txt"]=formatter.Format(snapshot),
            ["routes.txt"]=string.Join("\n",snapshot.Routes.Select(RouteAnalyzer.Describe)),
            ["interfaces.txt"]=formatter.FormatInterfaces(snapshot),
            ["vpn-status.txt"]=$"{snapshot.Status.State}\n{snapshot.Status.Message}\nTunnel IP: {snapshot.Status.TunnelIp}\nInterface: {snapshot.Status.InterfaceName}",
            ["redacted-log.txt"]=string.Join("\n",snapshot.Logs.Select(l=>$"{l.Timestamp:O} [{l.Severity}] {l.Provider} {l.Stage}: {l.Message}")),
            ["versions.txt"]=$"App {snapshot.AppVersion}\n{snapshot.OsVersion}\n"+string.Join("\n",snapshot.Dependencies.Select(d=>$"{d.Name}: {d.Version??"UNKNOWN"}"))
        };
        var items=checkoff.Select(c=>new { Id=redactor.Redact(c.Id),Text=redactor.Redact(c.Text),Result=c.Result.ToString(),Evidence=redactor.Redact(c.Evidence),Source="User attestation unless explicitly stated in evidence" }).ToArray();
        var absolute=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);var temp=absolute+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var zip=ZipFile.Open(temp,ZipArchiveMode.Create))
            {
                foreach(var entry in entries) { var item=zip.CreateEntry(entry.Key);await using var writer=new StreamWriter(item.Open(),Encoding.UTF8);await writer.WriteAsync(redactor.Redact(entry.Value)); }
                var jsonEntry=zip.CreateEntry("checkoff-results.json");await using var jsonWriter=new StreamWriter(jsonEntry.Open(),Encoding.UTF8);await jsonWriter.WriteAsync(JsonSerializer.Serialize(items,ProfileStore.JsonOptions));
            }
            File.Move(temp,absolute,true);
        }
        finally {if(File.Exists(temp))File.Delete(temp);}
    }
}
