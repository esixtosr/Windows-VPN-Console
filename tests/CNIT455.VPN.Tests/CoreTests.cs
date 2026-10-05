using System.IO.Compression;
using System.Text.Json;
using CNIT455.VPN.Core;
using CNIT455.VPN.Diagnostics;
using Xunit;
namespace CNIT455.VPN.Tests;
public sealed class CoreTests
{
    [Theory][InlineData("192.168.6.12/24","192.168.6.0/24")][InlineData("0.0.0.0/0","0.0.0.0/0")][InlineData("255.255.255.255/32","255.255.255.255/32")]
    public void CidrNormalizes(string input,string expected){Assert.True(CidrNetwork.TryParse(input,out var n));Assert.Equal(expected,n.ToString());}
    [Theory][InlineData("192.168.1/24")][InlineData("256.1.1.1/24")][InlineData("1.2.3.4/33")][InlineData("1.2.3.4/-1")][InlineData("01.2.3.4/24")][InlineData("127.1/24")][InlineData("2001:db8::/32")][InlineData("1.2.3.4/24;whoami")]
    public void CidrRejectsInvalid(string input)=>Assert.False(CidrNetwork.TryParse(input,out _));
    [Theory][InlineData(0)][InlineData(7)][InlineData(33)][InlineData(255)]
    public void TopologyDerivesEveryGroupAddress(int group){var t=LabTopology.Create(group);Assert.Equal($"44.104.{group}.4",t.VyosExternal);Assert.Equal($"172.18.{group}.0/24",t.VyosDmz);Assert.Equal("192.168.4.0/24",t.PfSenseRemote);Assert.All(LabPresets.CreateClientProfiles(t),p=>Assert.Equal(group,p.GroupNumber));}
    [Fact]public void StrictExtendedAndCustomStayDistinct(){var t=LabTopology.Create(42);var p=LabPresets.CreateClientProfiles(t)[0];Assert.Equal(new[]{t.VyosRemote},p.PermittedNetworks);LabPresets.ApplyPolicy(p,t,LabPolicy.Extended);Assert.Contains(t.VyosHq,p.PermittedNetworks);Assert.DoesNotContain(t.VyosHq,p.ForbiddenNetworks);p.PermittedNetworks=["10.3.0.0/16"];LabPresets.ApplyPolicy(p,t,LabPolicy.Custom);Assert.Equal("10.3.0.0/16",p.PermittedNetworks[0]);}
    [Fact]public void ReservedSubnetIsWarningAndDuplicateNormalizedRouteIsError(){var p=LabPresets.CreateClientProfiles(LabTopology.Create())[0];p.PermittedNetworks=["192.168.5.0/24","192.168.5.1/24"];var v=ProfileValidator.Validate(p);Assert.Contains(v,x=>!x.IsError&&x.Message.Contains("reserved"));Assert.Contains(v,x=>x.IsError&&x.Message.Contains("Duplicate"));}
    [Fact]public void PoolOverlapIsRejected()=>Assert.NotEmpty(ProfileValidator.ValidatePool("192.168.6.128/25",["192.168.6.0/24"]));
    [Theory][InlineData("password=Test123","Test123")][InlineData("psk=mysecret","mysecret")][InlineData("privatekey=PRIVATEVALUE","PRIVATEVALUE")][InlineData("radius-secret=RadiusValue","RadiusValue")][InlineData("Pre-Shared Key: Multi Word Secret","Multi Word Secret")][InlineData("WireGuard PrivateKey = PrivateKeyValue","PrivateKeyValue")][InlineData("set x authentication password 'secret phrase'","secret phrase")][InlineData("{\"password\":\"JsonSecret\"}","JsonSecret")][InlineData("shared secret: AnotherSecret","AnotherSecret")][InlineData("set vpn l2tp remote-access authentication radius server 10.0.0.2 key 'RadiusHidden'","RadiusHidden")]
    public void RedactionRemovesSensitiveValues(string input,string secret){var redacted=new SecretRedactor().Redact(input);Assert.DoesNotContain(secret,redacted);Assert.Contains("REDACTED",redacted);}
    [Fact]public void RedactionHandlesMultilinePrivateMaterial(){var source="before\n-----BEGIN PRIVATE KEY-----\nTopSecretMaterial\n-----END PRIVATE KEY-----\nafter\n<key>OpenVpnSecret</key>";var output=new SecretRedactor().Redact(source);Assert.DoesNotContain("TopSecretMaterial",output);Assert.DoesNotContain("OpenVpnSecret",output);Assert.Contains("before",output);Assert.Contains("after",output);}
    [Fact]public void RegisteredSecretsAreRemovedWithoutLabels(){var r=new SecretRedactor();r.RegisterSecret("KnownSecret9!");Assert.Equal("failure: [REDACTED]",r.Redact("failure: KnownSecret9!"));}
    [Fact]public void UnlabelledWireGuardKeysAreConservativelyRedacted(){var key=Convert.ToBase64String(new byte[32]);Assert.DoesNotContain(key,new SecretRedactor().Redact($"peer: {key}"));}
    [Fact]public async Task ProfileRoundTripAndExportExcludeSecretFields()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
        try{var store=new ProfileStore(dir);var p=LabPresets.CreateClientProfiles(LabTopology.Create(19))[0];p.ImportedConfigPath="C:\\sensitive\\local.vpn";await store.SaveAsync(p);var loaded=Assert.Single(await store.LoadAllAsync());Assert.Equal(p.Id,loaded.Id);var export=Path.Combine(dir,"export.json");await store.ExportAsync(p,export);var json=await File.ReadAllTextAsync(export);Assert.DoesNotContain("sensitive",json);using var d=JsonDocument.Parse(json);Assert.False(d.RootElement.TryGetProperty("Password",out _));Assert.False(d.RootElement.TryGetProperty("Psk",out _));Assert.False(d.RootElement.TryGetProperty("PrivateKey",out _));Assert.Equal("{}",JsonSerializer.Serialize(new VpnSecrets{Password="secret",Psk="psk"}));}
        finally{Directory.Delete(dir,true);}
    }
    [Fact]public async Task ProfileImportRejectsSecretFields(){var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);try{var file=Path.Combine(dir,"bad.json");await File.WriteAllTextAsync(file,"{\"Password\":\"secret\"}");await Assert.ThrowsAsync<JsonException>(()=>new ProfileStore(dir).ImportAsync(file));}finally{Directory.Delete(dir,true);}}
    [Fact]public async Task DamagedProfileDoesNotPreventValidProfilesLoading()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
        try { var store=new ProfileStore(dir);await store.SaveAsync(new VpnProfile{Name="Valid"});await File.WriteAllTextAsync(Path.Combine(dir,"broken.json"),"{invalid");Assert.Single(await store.LoadAllAsync());Assert.Single(store.LoadWarnings);Assert.True(File.Exists(Path.Combine(dir,"broken.json"))); } finally { Directory.Delete(dir,true); }
    }
    [Fact]public async Task EvidenceRedactsEveryMemberAndKeepsValidJson()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".zip");var r=new SecretRedactor();r.RegisterSecret("KnownSecret9!");var snapshot=new DiagnosticSnapshot{ServerOutput="psk=ServerSecret",Logs=[new(DateTimeOffset.Now,LogSeverity.Error,"native",VpnStage.Authentication,"password=Test123")],Status=new(VpnState.Failed,"KnownSecret9!")};
        try{await new EvidenceExporter(r).ExportAsync(path,snapshot,[new("test","password=TextSecret",ResultState.Unknown,"radius-secret=RadiusSecret")]);using var zip=ZipFile.OpenRead(path);Assert.Equal(8,zip.Entries.Count);foreach(var e in zip.Entries){using var reader=new StreamReader(e.Open());var content=await reader.ReadToEndAsync();foreach(var secret in new[]{"ServerSecret","Test123","KnownSecret9!","TextSecret","RadiusSecret"})Assert.DoesNotContain(secret,content);if(e.Name.EndsWith(".json")){using var json=JsonDocument.Parse(content);Assert.Equal(JsonValueKind.Array,json.RootElement.ValueKind);}}}finally{File.Delete(path);}
    }
}
