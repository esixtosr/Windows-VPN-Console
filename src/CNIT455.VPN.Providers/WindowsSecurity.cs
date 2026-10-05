using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using CNIT455.VPN.Core;

namespace CNIT455.VPN.Providers;

/// <summary>Optional explicit opt-in storage using the Windows account's Credential Manager.</summary>
public sealed class WindowsSecretStore : ISecretStore
{
    private sealed record SecretPayload(string Password,string Psk,string PrivateKey,string RadiusSecret);
    private static string Target(Guid id) => "CNIT455-VPN-Console/" + id.ToString("N");
    public Task SaveAsync(Guid profileId,VpnSecrets secrets)
    {
        EnsureWindows();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new SecretPayload(secrets.Password,secrets.Psk,secrets.PrivateKey,secrets.RadiusSecret));
        if (bytes.Length > 2560) { CryptographicOperations.ZeroMemory(bytes); throw new ArgumentException("Combined credentials exceed the Windows Credential Manager limit."); }
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes,0,ptr,bytes.Length);
            var credential = new Credential { Type=1,TargetName=Target(profileId),CredentialBlobSize=(uint)bytes.Length,CredentialBlob=ptr,Persist=2,UserName=Environment.UserName,Comment="CNIT 455 VPN Console: explicitly remembered by this Windows account" };
            if (!CredWrite(ref credential,0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.Copy(new byte[bytes.Length],0,ptr,bytes.Length); Marshal.FreeHGlobal(ptr); CryptographicOperations.ZeroMemory(bytes); }
        return Task.CompletedTask;
    }
    public Task<VpnSecrets?> LoadAsync(Guid profileId)
    {
        EnsureWindows();
        if (!CredRead(Target(profileId),1,0,out var ptr))
        { var error=Marshal.GetLastWin32Error(); if(error==1168) return Task.FromResult<VpnSecrets?>(null); throw new Win32Exception(error); }
        try
        {
            var credential=Marshal.PtrToStructure<Credential>(ptr);
            var bytes=new byte[checked((int)credential.CredentialBlobSize)]; Marshal.Copy(credential.CredentialBlob,bytes,0,bytes.Length);
            try { var p=JsonSerializer.Deserialize<SecretPayload>(bytes) ?? throw new InvalidDataException("Invalid saved credential."); return Task.FromResult<VpnSecrets?>(new() {Password=p.Password,Psk=p.Psk,PrivateKey=p.PrivateKey,RadiusSecret=p.RadiusSecret}); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        finally { CredFree(ptr); }
    }
    public Task DeleteAsync(Guid profileId)
    { EnsureWindows(); if(!CredDelete(Target(profileId),1,0) && Marshal.GetLastWin32Error()!=1168) throw new Win32Exception(Marshal.GetLastWin32Error()); return Task.CompletedTask; }
    private static void EnsureWindows() { if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Credential storage requires Windows."); }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct Credential
    {
        public uint Flags,Type; public string TargetName; public string? Comment; public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize; public IntPtr CredentialBlob; public uint Persist,AttributeCount; public IntPtr Attributes; public string? TargetAlias; public string? UserName;
    }
    [DllImport("advapi32.dll",EntryPoint="CredWriteW",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CredWrite(ref Credential credential,uint flags);
    [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CredRead(string target,uint type,uint flags,out IntPtr credential);
    [DllImport("advapi32.dll",EntryPoint="CredDeleteW",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CredDelete(string target,uint type,uint flags);
    [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr buffer);
}

public sealed record CertificateInfo(string Subject,string Issuer,string Thumbprint,DateTime NotBefore,DateTime NotAfter,bool HasPrivateKey,bool IsValid,string StoreLocation)
{
    public string ValidityStatus => IsValid ? "Within date range; chain/revocation not verified" : "Expired or not yet valid";
}
public sealed class CertificateService
{
    public IReadOnlyList<CertificateInfo> ListCertificates()
    {
        if(!OperatingSystem.IsWindows()) return [];
        var result=new List<CertificateInfo>();
        foreach(var location in new[] {StoreLocation.CurrentUser,StoreLocation.LocalMachine})
        {
            using var store=new X509Store(StoreName.My,location); store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
            foreach(var cert in store.Certificates) using(cert) result.Add(Describe(cert,location));
        }
        return result.OrderBy(x=>x.Subject).ToList();
    }
    public CertificateInfo ImportCertificate(string path,string? password=null)
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows certificate store required.");
        var ext=Path.GetExtension(path).ToLowerInvariant();
        using var cert=ext is ".pfx" or ".p12"
            ? X509CertificateLoader.LoadPkcs12FromFile(path,password,X509KeyStorageFlags.PersistKeySet|X509KeyStorageFlags.UserKeySet)
            : X509CertificateLoader.LoadCertificateFromFile(path);
        // Import into Personal only. Never silently trust a CA or mark a private key exportable.
        using var store=new X509Store(StoreName.My,StoreLocation.CurrentUser); store.Open(OpenFlags.ReadWrite); store.Add(cert);
        return Describe(cert,StoreLocation.CurrentUser);
    }
    private static CertificateInfo Describe(X509Certificate2 cert,StoreLocation location) => new(cert.Subject,cert.Issuer,cert.Thumbprint,cert.NotBefore,cert.NotAfter,cert.HasPrivateKey,DateTime.Now>=cert.NotBefore && DateTime.Now<=cert.NotAfter,location.ToString());
}

internal static class PrivateRuntimeFiles
{
    internal static string CreateDirectory()
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows required.");
        var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CNIT455-VPN-Console","Runtime",Guid.NewGuid().ToString("N"));
        var directory=Directory.CreateDirectory(path);
        var acl=new DirectorySecurity(); acl.SetAccessRuleProtection(true,false);
        using var identity=WindowsIdentity.GetCurrent();
        foreach(var sid in new[] {identity.User!,new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null)})
            acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        directory.SetAccessControl(acl);
        return path;
    }
    internal static void Delete(string? directory)
    { if(directory is not null && Directory.Exists(directory)) Directory.Delete(directory,true); }

    internal static byte[] ProtectForMachine(byte[] data,string description)
    {
        var source=new DataBlob { Length=data.Length,Data=Marshal.AllocHGlobal(data.Length) };
        try
        {
            Marshal.Copy(data,0,source.Data,data.Length);
            if(!CryptProtectData(ref source,description,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,5,out var output)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try { var result=new byte[output.Length]; Marshal.Copy(output.Data,result,0,result.Length); return result; }
            finally { LocalFree(output.Data); }
        }
        finally { Marshal.Copy(new byte[data.Length],0,source.Data,data.Length); Marshal.FreeHGlobal(source.Data); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct DataBlob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref DataBlob data,string description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out DataBlob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
