using System.IO;
using System.Text.Json;
using System.Windows;
using CNIT455.VPN.Core;

namespace CNIT455.VPN.App;

public sealed class App : Application
{
    [STAThread]
    public static void Main(string[] args)
    {
        var app = new App { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.Startup += async (_, _) =>
        {
            var smokeIndex = Array.IndexOf(args, "--smoke-test");
            var smokePath = smokeIndex >= 0 && smokeIndex + 1 < args.Length ? args[smokeIndex + 1] : null;
            var model = new MainViewModel(smokePath is not null);
            app.DispatcherUnhandledException += (_, e) => { model.ReportError(e.Exception); e.Handled = true; };
            var window = new MainWindow(model);
            app.MainWindow = window;
            window.Show();
            try
            {
                await model.InitializeAsync();
                if (smokePath is not null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(smokePath))!);
                    var smoke = await model.RunSmokeAsync(window, smokePath);
                    await File.WriteAllTextAsync(smokePath, JsonSerializer.Serialize(smoke, new JsonSerializerOptions { WriteIndented = true }));
                    await model.ShutdownAsync();
                    app.Shutdown(0);
                }
            }
            catch (Exception error)
            {
                model.ReportError(error);
                if (smokePath is not null)
                {
                    await File.WriteAllTextAsync(smokePath, JsonSerializer.Serialize(new { success = false, error = error.ToString() }));
                    app.Shutdown(1);
                }
            }
        };
        app.Run();
    }
}
