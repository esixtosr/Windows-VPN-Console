using System.Text.RegularExpressions;
namespace CNIT455.VPN.Core;
public sealed class SecretRedactor
{
    private readonly HashSet<string> secrets = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private static readonly Regex Pem = new(@"-----BEGIN (?:[A-Z0-9 ]*PRIVATE KEY|CERTIFICATE)-----[\s\S]*?(?:-----END (?:[A-Z0-9 ]*PRIVATE KEY|CERTIFICATE)-----|\z)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    private static readonly Regex Inline = new(@"<(key|pkcs12|tls-auth|tls-crypt|tls-crypt-v2|secret|auth-user-pass)>[\s\S]*?(?:</\1>|\z)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    // Consume the remainder of a sensitive line rather than risking leaks through quotes, spaces or CLI switches.
    private static readonly Regex Named = new("(?im)([\"']?(?:password|passwd|pwd|secret|psk|pre[ -]?shared(?:[ -]?key)?|private[ _-]?key|radius[ _-]?(?:key|secret)|shared[ _-]?secret|auth[ _-]?token)[\"']?\\s*(?::|=|\\s)\\s*)[^\\r\\n]*",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    private static readonly Regex RadiusCommand = new(@"(?im)(\bradius\s+server\s+[^\r\n]+?\s+key\s+)[^\r\n]*",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    private static readonly Regex KeyLike = new(@"(?<![A-Za-z0-9+/])[A-Za-z0-9+/]{43}=(?![A-Za-z0-9+/])",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    public void RegisterSecret(string? value) { if(!string.IsNullOrEmpty(value)) lock(gate) secrets.Add(value); }
    public void Register(VpnSecrets value) { RegisterSecret(value.Password);RegisterSecret(value.Psk);RegisterSecret(value.PrivateKey);RegisterSecret(value.RadiusSecret); }
    public string Redact(string? input)
    {
        if(string.IsNullOrEmpty(input)) return "";
        try
        {
            var text=input;
            lock(gate) foreach(var secret in secrets.OrderByDescending(x=>x.Length)) text=text.Replace(secret,"[REDACTED]",StringComparison.Ordinal);
            text=Pem.Replace(text,"[REDACTED PRIVATE/CERTIFICATE MATERIAL]");
            text=Inline.Replace(text,"[REDACTED INLINE CREDENTIAL MATERIAL]");
            text=RadiusCommand.Replace(text,"$1[REDACTED]");
            text=Named.Replace(text,"$1[REDACTED]");
            return KeyLike.Replace(text,"[REDACTED KEY]");
        }
        catch(RegexMatchTimeoutException) { return "[REDACTED: input exceeded safe sanitization time]"; }
    }
}
