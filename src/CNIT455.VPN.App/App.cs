using System.IO;
using System.Text.Json;
using System.Windows;

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
            var smokePath = smokeIndex >= 0 && smokeIndex + 1 < args.Length ? Path.GetFullPath(args[smokeIndex + 1]) : null;
            MainViewModel? model = null; MainWindow? window = null;
            var uiErrors = new List<string>();
            app.DispatcherUnhandledException += (_, e) =>
            {
                model?.ReportError(e.Exception); uiErrors.Add(e.Exception.ToString()); e.Handled = true;
            };
            try
            {
                if (smokePath is not null) Directory.CreateDirectory(Path.GetDirectoryName(smokePath)!);
                model = new MainViewModel(smokePath is not null);
                window = new MainWindow(model); app.MainWindow = window; window.Show();
                await model.InitializeAsync();
                if (smokePath is not null)
                {
                    var smoke = await model.RunSmokeAsync(window, smokePath);
                    if (uiErrors.Count > 0) throw new InvalidOperationException("Unhandled UI exception during smoke test: " + string.Join("\n", uiErrors));
                    await model.ShutdownAsync();
                    await File.WriteAllTextAsync(smokePath, JsonSerializer.Serialize(smoke, new JsonSerializerOptions { WriteIndented = true }));
                    app.Shutdown(0);
                }
            }
            catch (Exception error)
            {
                model?.ReportError(error);
                if (smokePath is not null)
                {
                    try { window?.SaveScreenshot(Path.Combine(Path.GetDirectoryName(smokePath)!, "smoke-failure.png")); } catch { }
                    await File.WriteAllTextAsync(smokePath, JsonSerializer.Serialize(new { success = false, error = error.ToString(), uiErrors }));
                    if (model is not null) await model.ShutdownAsync();
                    app.Shutdown(1);
                }
                else if (window is null)
                {
                    MessageBox.Show("The VPN console could not start. " + error.Message, "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
                    app.Shutdown(1);
                }
            }
        };
        app.Run();
    }
}
