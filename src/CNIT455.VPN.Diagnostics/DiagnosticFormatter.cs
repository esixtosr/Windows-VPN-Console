using System.Text;
using CNIT455.VPN.Core;
namespace CNIT455.VPN.Diagnostics;
public sealed class DiagnosticFormatter(SecretRedactor redactor)
{
    public string Format(DiagnosticSnapshot d)
    {
        var text=new StringBuilder();
        text.AppendLine("==================================================\nCNIT455 VPN DIAGNOSTIC\n==================================================");
        text.AppendLine($"Application: CNIT455 VPN Console {d.AppVersion}\nCaptured: {d.CapturedAt:O}\nWindows/OS: {d.OsVersion}\nAdministrator: {d.IsAdministrator}\nVPN: {d.Profile.Protocol}\nProvider: {d.Profile.ProviderId}\nGateway: {d.Profile.Gateway}\nExpected networks: {string.Join(", ",d.Profile.PermittedNetworks)}\nPolicy: {d.Profile.TunnelMode} / {d.Profile.LabPolicy}\nAuthentication workflow: {d.Profile.AuthBackend}");
        if(d.Profile.ProviderId=="mock")text.AppendLine("SIMULATED PROVIDER — test data is not evidence of a real VPN.");
        text.AppendLine($"\nCONNECTIVITY\nGateway ICMP response: {d.GatewayReachable.ToString().ToUpperInvariant()} (no reply is UNKNOWN)\n\nNEGOTIATION\nTunnel: {d.Status.State}\nProvider observation: {d.Status.Message}\nAssigned address: {d.Status.TunnelIp??"UNKNOWN"}\nTunnel interface: {d.Status.InterfaceName??"UNKNOWN"}\nIKE / PSK / XAUTH / Phase 2: UNKNOWN unless explicitly reported in the logs below.");
        text.AppendLine($"\nROUTING\n{d.Routing.Result}: {d.Routing.Summary}");foreach(var line in d.Routing.Evidence)text.AppendLine(line);
        text.AppendLine("\nACTUAL ROUTES");foreach(var r in d.Routes)text.AppendLine(RouteAnalyzer.Describe(r));
        text.AppendLine("\nINTERFACES");text.AppendLine(FormatInterfaces(d));
        text.AppendLine("\nLIKELY FAILURE STAGE");foreach(var f in d.Findings)text.AppendLine($"{f.Category}: {f.Result} — {f.Summary}\nNext checks: {f.NextChecks}");
        text.AppendLine("\nRELEVANT LOGS");foreach(var log in d.Logs)text.AppendLine($"{log.Timestamp:O} [{log.Severity}] {log.Provider} / {log.Stage}: {log.Message}");
        text.AppendLine("\nWINDOWS RAS EVENTS\n"+d.SystemEvents);
        text.AppendLine("\nSANITIZED SERVER OUTPUT\n"+d.ServerOutput);
        text.AppendLine("\nCOLLECTION NOTES\n"+d.CollectionNotes);
        text.AppendLine("\nVERSIONS");foreach(var dep in d.Dependencies)text.AppendLine($"{dep.Name}: {(dep.Installed?dep.Version??"Found":"Missing")} {dep.ExecutablePath}");
        text.AppendLine("\nSECRETS AUTOMATICALLY REDACTED\nPSK: [REDACTED]\nPassword: [REDACTED]\nPrivate key: [REDACTED]\nNo traffic encryption claim is inferred from UDP or route presence.");
        return redactor.Redact(text.ToString());
    }
    public string FormatInterfaces(DiagnosticSnapshot d)=>redactor.Redact(string.Join("\n",d.Adapters.Select(a=>$"{a.Index}: {a.Name} ({a.Status})\n  IPv4/IPv6: {string.Join(", ",a.Addresses)}\n  DNS: {string.Join(", ",a.DnsServers)}\n  Gateway: {string.Join(", ",a.Gateways)}")));
}
