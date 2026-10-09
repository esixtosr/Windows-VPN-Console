using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CNIT455.VPN.Core;

namespace CNIT455.VPN.App;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Raise(name); return true;
    }
}

public sealed class AsyncCommand(Func<Task> execute, Action<Exception> onError) : ICommand
{
    private bool running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !running;
    public async void Execute(object? parameter)
    {
        if (running) return;
        running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute(); } catch (Exception error) { onError(error); }
        finally { running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
}

public sealed record ProviderChoice(string Id, string Name) { public override string ToString() => Name; }
public sealed record EngineRow(DependencyInfo Info)
{
    public string Name => Info.Name;
    public string Availability => Info.Installed ? "Detected" : "Not detected";
    public string Role => Info.Id switch
    {
        "native" => "Built into Windows",
        "wireguard" or "openvpn" => "Managed by this console",
        "ncp" => "Opens NCP · separate license",
        "shrew" => "Opens Shrew · legacy client",
        "mock" => "Simulation only", _ => "External engine"
    };
}
public sealed class CheckoffRow(CheckoffItem item) : ObservableObject
{
    private ResultState result = item.Result;
    private string evidence = item.Evidence;
    public string Id { get; } = item.Id;
    public string Text { get; } = item.Text;
    public ResultState Result { get => result; set => Set(ref result, value); }
    public string Evidence { get => evidence; set => Set(ref evidence, value); }
    public CheckoffItem ToItem() => new(Id, Text, Result, Evidence);
}

public sealed class UserSettings
{
    public bool DeveloperMode { get; set; }
    public bool LabMode { get; set; }
    public int GroupNumber { get; set; } = 33;
    public int LogRetentionDays { get; set; } = 14;
    public LabTopology? Topology { get; set; }
}
