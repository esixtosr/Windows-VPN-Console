using System.Text.Json;
using System.Text.Json.Serialization;
namespace CNIT455.VPN.Core;
public sealed class ProfileStore(string? root = null)
{
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CNIT455-VPN-Console");
    public static JsonSerializerOptions JsonOptions { get; } = new() { WriteIndented=true, Converters={new JsonStringEnumConverter()}, PropertyNameCaseInsensitive=true, UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow };
    private readonly string directory = root ?? Path.Combine(DataDirectory,"Profiles");
    public List<string> LoadWarnings { get; } = [];
    public async Task<IReadOnlyList<VpnProfile>> LoadAllAsync()
    {
        Directory.CreateDirectory(directory);
        var result=new List<VpnProfile>();
        LoadWarnings.Clear();
        foreach(var file in Directory.EnumerateFiles(directory,"*.json"))
        {
            try { result.Add(await ReadAsync(file)); }
            catch(Exception e) when(e is JsonException or InvalidDataException or IOException or UnauthorizedAccessException) { LoadWarnings.Add($"Skipped unreadable profile {Path.GetFileName(file)}. Original preserved. {e.GetType().Name}"); }
        }
        return result;
    }
    public async Task SaveAsync(VpnProfile profile)
    {
        Directory.CreateDirectory(directory);
        var file=Path.Combine(directory,$"{profile.Id}.json");
        var temp=file+".tmp";
        await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(profile,JsonOptions));
        File.Move(temp,file,true);
    }
    public Task DeleteAsync(Guid profileId) { File.Delete(Path.Combine(directory,$"{profileId}.json"));return Task.CompletedTask; }
    public async Task<VpnProfile> ImportAsync(string path)
    {
        var profile=(await ReadAsync(path)) with { Id=Guid.NewGuid() };
        await SaveAsync(profile);return profile;
    }
    public Task ExportAsync(VpnProfile profile,string path) => File.WriteAllTextAsync(path,JsonSerializer.Serialize(profile with { ImportedConfigPath="" },JsonOptions));
    private static async Task<VpnProfile> ReadAsync(string path)
    {
        if(new FileInfo(path).Length>1024*1024) throw new InvalidDataException("Profile exceeds 1 MiB.");
        var p=JsonSerializer.Deserialize<VpnProfile>(await File.ReadAllTextAsync(path),JsonOptions)??throw new InvalidDataException("Empty profile.");
        if(p.Id==Guid.Empty || p.PermittedNetworks is null || p.ForbiddenNetworks is null || p.PermittedNetworks.Any(x=>x is null) || p.ForbiddenNetworks.Any(x=>x is null) || typeof(VpnProfile).GetProperties().Where(x=>x.PropertyType==typeof(string)).Any(x=>x.GetValue(p) is null)) throw new InvalidDataException("Missing required profile fields.");
        if(!Enum.IsDefined(p.Protocol) || !Enum.IsDefined(p.Authentication) || !Enum.IsDefined(p.TunnelMode) || !Enum.IsDefined(p.AuthBackend) || !Enum.IsDefined(p.LabPolicy) || !Enum.IsDefined(p.MockFailure)) throw new InvalidDataException("Unknown profile enum value.");
        return p;
    }
}
public sealed class AppSettings
{
    public bool DeveloperMode {get;set;}
    public bool LabMode {get;set;}
    public bool AdvancedMode {get;set;}
    public int LogRetentionDays {get;set;}=14;
    public int GroupNumber {get;set;}=33;
    public LabTopology? Topology {get;set;}
    public static async Task<AppSettings> LoadAsync()
    {
        var path=Path.Combine(ProfileStore.DataDirectory,"settings.json");
        return File.Exists(path)?JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(path),ProfileStore.JsonOptions)??new():new();
    }
    public async Task SaveAsync()
    {
        if(LogRetentionDays is <1 or >365) throw new InvalidDataException("Retention must be 1–365 days.");
        Directory.CreateDirectory(ProfileStore.DataDirectory);
        var path=Path.Combine(ProfileStore.DataDirectory,"settings.json");
        await File.WriteAllTextAsync(path+".tmp",JsonSerializer.Serialize(this,ProfileStore.JsonOptions));File.Move(path+".tmp",path,true);
    }
}
