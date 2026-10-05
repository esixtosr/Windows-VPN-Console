using CNIT455.VPN.Core;
namespace CNIT455.VPN.Diagnostics;
public sealed class SessionLog(SecretRedactor redactor)
{
    private readonly object gate=new();
    private readonly Queue<VpnLogEvent> events=new();
    public IReadOnlyList<VpnLogEvent> Events {get {lock(gate)return events.ToArray();}}
    public void Add(VpnLogEvent e)
    {
        e=e with {Message=redactor.Redact(e.Message)};
        lock(gate)
        {
            events.Enqueue(e);while(events.Count>2000)events.Dequeue();
            var directory=Path.Combine(ProfileStore.DataDirectory,"Logs");Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory,$"{DateTime.Today:yyyy-MM-dd}.log"),$"{e.Timestamp:O} [{e.Severity}] {e.Provider}/{e.Stage}: {e.Message}\n");
        }
    }
    public void Clear(){lock(gate)events.Clear();}
    public static void Prune(int days=14)
    {
        if(days is <1 or >365)throw new ArgumentOutOfRangeException(nameof(days));
        var directory=Path.Combine(ProfileStore.DataDirectory,"Logs");if(!Directory.Exists(directory))return;
        foreach(var file in Directory.EnumerateFiles(directory,"*.log"))if(File.GetLastWriteTimeUtc(file)<DateTime.UtcNow.AddDays(-days))File.Delete(file);
    }
}
