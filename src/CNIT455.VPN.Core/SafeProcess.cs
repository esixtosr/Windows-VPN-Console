using System.Diagnostics;
namespace CNIT455.VPN.Core;
public sealed record ProcessResult(int ExitCode,string StandardOutput,string StandardError);
public static class SafeProcess
{
    public static async Task<ProcessResult> RunAsync(string executable,IEnumerable<string> args,string? standardInput=null,CancellationToken cancellationToken=default)
    {
        if(!Path.IsPathFullyQualified(executable) || !File.Exists(executable)) throw new FileNotFoundException("Expected an existing absolute executable path.",executable);
        var start=new ProcessStartInfo(executable) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true };
        foreach(var arg in args) start.ArgumentList.Add(arg);
        using var process=Process.Start(start)??throw new InvalidOperationException("Process could not start.");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);timeout.CancelAfter(TimeSpan.FromSeconds(45));
        var output=ReadBoundedAsync(process.StandardOutput,timeout.Token);
        var error=ReadBoundedAsync(process.StandardError,timeout.Token);
        try
        {
            if(standardInput is not null) await process.StandardInput.WriteAsync(standardInput.AsMemory(),timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            return new(process.ExitCode,await output,await error);
        }
        catch
        {
            try { if(!process.HasExited) process.Kill(entireProcessTree:true); } catch(InvalidOperationException) { }
            try { await Task.WhenAll(output,error); } catch { }
            throw;
        }
    }
    private static async Task<string> ReadBoundedAsync(StreamReader reader,CancellationToken token)
    {
        var text=new System.Text.StringBuilder();var buffer=new char[4096];int read;
        while((read=await reader.ReadAsync(buffer.AsMemory(),token))>0) { if(text.Length<2*1024*1024) text.Append(buffer,0,Math.Min(read,2*1024*1024-text.Length)); }
        return text.ToString();
    }
}
