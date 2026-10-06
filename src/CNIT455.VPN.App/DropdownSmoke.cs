using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CNIT455.VPN.Core;
using CNIT455.VPN.ConfigGenerators;

namespace CNIT455.VPN.App;

public sealed partial class MainViewModel
{
    // Exercise the actual field presenters: enum selections and DisplayMemberPath
    // selections take different rendering paths in the Windows ComboBox template.
    private async Task CaptureDropdownSmokeAsync(MainWindow window, string directory)
    {
        var previousProfile = SelectedProfile;
        var previousPage = SelectedPage;
        var previousTemplate = ServerOptions.Template;
        var previousAuthentication = ServerOptions.Authentication;
        var previousFocus = Keyboard.FocusedElement;
        async Task Drain() => await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        ComboBox Field(string name) => VisualChildren<ComboBox>(window).Single(x => AutomationProperties.GetName(x) == name);

        try
        {
            SelectedProfile = Profiles.First(x => x.Protocol == VpnProtocol.L2tpIpsec && x.ProviderId == "native");
            SelectedPage = "Connections";
            await Drain();
            await window.CapturePageAsync(Path.Combine(directory, "dropdown-native-l2tp.png"));
            foreach (var (label, name) in new[]
            {
                ("Active connection", "profile"), ("VPN type", "vpn-type"),
                ("VPN engine", "engine"), ("Authentication", "authentication"), ("Tunnel policy", "tunnel-policy")
            })
                await CaptureDropdownAsync(Field(label), "dropdown-" + name, directory, Drain);

            var logFilter = VisualChildren<ComboBox>(window).Single(x => x.GetBindingExpression(Selector.SelectedItemProperty)?.ParentBinding.Path.Path == nameof(LogFilter));
            await CaptureDropdownAsync(logFilter, "dropdown-log-filter", directory, Drain);

            ServerOptions.Template = ServerTemplate.WireGuardSiteToSite;
            ServerOptions.Authentication = AuthBackend.Radius;
            SelectedPage = "Server Config";
            await Drain();
            await window.CapturePageAsync(Path.Combine(directory, "dropdown-server-config.png"));
            await CaptureDropdownAsync(Field("Configuration template"), "dropdown-server-template", directory, Drain);
            await CaptureDropdownAsync(Field("Authentication backend"), "dropdown-server-authentication", directory, Drain);
            await CaptureDropdownAsync(Field("Length"), "dropdown-psk-length", directory, Drain);
        }
        finally
        {
            ServerOptions.Template = previousTemplate;
            ServerOptions.Authentication = previousAuthentication;
            SelectedProfile = previousProfile;
            SelectedPage = previousPage;
            await Drain();
            if (previousFocus is UIElement { IsVisible: true, IsEnabled: true } focus) Keyboard.Focus(focus);
        }
    }

    private static async Task CaptureDropdownAsync(ComboBox combo, string name, string directory, Func<Task> drain)
    {
        var enabled = combo.IsEnabled;
        var expanded = combo.IsDropDownOpen;
        try
        {
            combo.BringIntoView();
            await drain();
            combo.IsDropDownOpen = false;
            Keyboard.ClearFocus();
            await drain();
            CaptureElement(combo, Path.Combine(directory, name + "-closed.png"));
            combo.Focus();
            Keyboard.Focus(combo);
            await drain();
            CaptureElement(combo, Path.Combine(directory, name + "-focused.png"));
            combo.SetCurrentValue(UIElement.IsEnabledProperty, false);
            await drain();
            CaptureElement(combo, Path.Combine(directory, name + "-disabled.png"));
            combo.SetCurrentValue(UIElement.IsEnabledProperty, enabled);
            combo.IsDropDownOpen = true;
            await drain();
            if (combo.Template.FindName("PART_Popup", combo) is not Popup { IsOpen: true, Child: FrameworkElement popup })
                throw new InvalidOperationException("Dropdown smoke could not open " + name);
            // Popup has its own native window, outside MainWindow.RenderTargetBitmap.
            CaptureElement(popup, Path.Combine(directory, name + "-open.png"));
        }
        finally
        {
            combo.IsDropDownOpen = expanded;
            combo.SetCurrentValue(UIElement.IsEnabledProperty, enabled);
            await drain();
        }
    }

    private static IEnumerable<T> VisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in VisualChildren<T>(child)) yield return descendant;
        }
    }

    private static void CaptureElement(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
            throw new InvalidOperationException("Dropdown smoke found an unmeasured control: " + Path.GetFileName(path));
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path);
        encoder.Save(output);
    }
}
