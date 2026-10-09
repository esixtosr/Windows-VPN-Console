using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CNIT455.VPN.App;

public sealed class MainWindow : Window
{
    private readonly MainViewModel model;
    private readonly ContentControl page = new();
    private bool refreshQueued;
    public MainWindow(MainViewModel model)
    {
        this.model = model;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Windows-VPN;component/Theme.xaml", UriKind.Relative) });
        DataContext = model; Title = "Windows VPN Console";
        Width = Math.Min(1280, SystemParameters.WorkArea.Width); Height = Math.Min(880, SystemParameters.WorkArea.Height);
        MinWidth = Math.Min(980, SystemParameters.WorkArea.Width); MinHeight = Math.Min(640, SystemParameters.WorkArea.Height);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Windows-VPN;component/Assets/app.ico"));
        var root = new Grid(); root.ColumnDefinitions.Add(new() { Width = new GridLength(206) }); root.ColumnDefinitions.Add(new());
        var side = new DockPanel { Margin = new Thickness(16, 24, 16, 18) };
        var brand = new StackPanel { Margin = new Thickness(10, 0, 0, 28) };
        brand.Children.Add(new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Windows-VPN;component/Assets/app-icon.png")), Width = 44, Height = 44, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12) });
        brand.Children.Add(new TextBlock { Text = "VPN Console", FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = Ui.Brush("#F1F5F9") });
        brand.Children.Add(new TextBlock { Text = "FOR WINDOWS", FontSize = 10, Margin = new Thickness(1, 5, 0, 0), Foreground = Ui.Brush("#88A3BC") });
        DockPanel.SetDock(brand, Dock.Top); side.Children.Add(brand);
        var footer = new StackPanel { Margin = new Thickness(10, 12, 0, 0) };
        footer.Children.Add(Ui.Text("Your secrets stay local.", 11, "#A8BBCC"));
        footer.Children.Add(Ui.Text($"v{DisplayLabels.Version}", 10, "#8098AE")); DockPanel.SetDock(footer, Dock.Bottom); side.Children.Add(footer);
        var nav = new ListBox { ItemsSource = model.Navigation, ItemTemplate = Ui.NavigationTemplate() }; nav.SetBinding(IsEnabledProperty, Ui.Bind(nameof(model.CanEdit), false)); nav.SetBinding(ListBox.SelectedItemProperty, Ui.Bind(nameof(model.SelectedPage))); AutomationProperties.SetName(nav, "Primary navigation"); side.Children.Add(nav);
        var sideBorder = new Border { Background = Ui.Brush("#101C2B"), BorderBrush = Ui.Brush("#26364A"), BorderThickness = new Thickness(0, 0, 1, 0), Child = side }; root.Children.Add(sideBorder);
        var work = new Grid { Margin = new Thickness(26, 24, 22, 12) }; work.RowDefinitions.Add(new() { Height = GridLength.Auto }); work.RowDefinitions.Add(new()); work.RowDefinitions.Add(new() { Height = GridLength.Auto }); work.RowDefinitions.Add(new() { Height = GridLength.Auto }); Grid.SetColumn(work, 1); root.Children.Add(work);
        var header = new Grid { Margin = new Thickness(0, 0, 0, 18) }; header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var titles = new StackPanel(); titles.Children.Add(Ui.BoundText(nameof(model.ModeLabel), 10, "#5DE2C2")); titles.Children.Add(Ui.BoundText(nameof(model.PageTitle), 28, "#F1F5F9", FontWeights.SemiBold)); titles.Children.Add(Ui.BoundText(nameof(model.PageDescription), 12, "#A8BBCC")); header.Children.Add(titles);
        var state = new Border { Background = Ui.Brush("#203145"), CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 8, 14, 3), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(16, 4, 0, 0), MaxWidth = 200 };
        var statusLabel = Ui.BoundText(nameof(model.StatusLabel), 12, "#C9D8E4", FontWeights.SemiBold);
        statusLabel.SetBinding(TextBlock.ForegroundProperty, Ui.Bind(nameof(model.StatusColor), false));
        state.Child = statusLabel; state.SetBinding(ToolTipProperty, Ui.Bind(nameof(model.ConnectionMessage), false)); Grid.SetColumn(state, 1); header.Children.Add(state); work.Children.Add(header);
        Grid.SetRow(page, 1); page.SetBinding(IsEnabledProperty, Ui.Bind(nameof(model.CanEdit), false)); work.Children.Add(page);
        var notice = new Border { Background = Ui.Brush("#192C3D"), BorderBrush = Ui.Brush("#2C4358"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(12, 9, 12, 3), Margin = new Thickness(0, 8, 0, 6), MaxHeight = 90 };
        var noticeText = Ui.BoundText(nameof(model.StatusText), 12, "#D7E5F0"); AutomationProperties.SetLiveSetting(noticeText, AutomationLiveSetting.Polite); notice.Child = new ScrollViewer { Content = noticeText, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(notice, 2); work.Children.Add(notice);
        var log = new DockPanel { Height = 188 }; var logActions = new DockPanel(); DockPanel.SetDock(logActions, Dock.Top);
        var actions = Ui.Actions(Ui.Button("Pause", model.PauseLogsCommand), Ui.Button("Clear", model.ClearLogsCommand), Ui.Button("Copy", model.CopyLogsCommand), Ui.Button("Save", model.SaveLogsCommand)); ((Button)actions.Children[0]).SetBinding(ContentControl.ContentProperty, Ui.Bind(nameof(model.PauseLabel), false)); actions.HorizontalAlignment = HorizontalAlignment.Right; logActions.Children.Add(actions); log.Children.Add(logActions);
        var filterBar = Ui.Actions(Ui.Text("FILTER", 10, "#A8BBCC"), Ui.Select(nameof(model.LogFilter), new[] { "All", "Debug", "Information", "Warning", "Error" }), Ui.Input(nameof(model.LogSearch)));
        ((ComboBox)filterBar.Children[1]).Width = 135; ((TextBox)filterBar.Children[2]).Width = 230; ((TextBox)filterBar.Children[2]).Margin = new Thickness(10, 0, 0, 0); ((TextBox)filterBar.Children[2]).ToolTip = "Search this session's redacted log";
        DockPanel.SetDock(filterBar, Dock.Top); log.Children.Add(filterBar);
        var logBox = Ui.Output(nameof(model.LogText), 11); logBox.TextChanged += (_, _) => { if (!model.LogsPaused) logBox.ScrollToEnd(); }; log.Children.Add(logBox);
        var drawer = Ui.Disclosure("Activity log · redacted", log); drawer.Name = "ActivityDrawer"; drawer.Margin = new Thickness(0); Grid.SetRow(drawer, 3); work.Children.Add(drawer);
        Content = root; model.PropertyChanged += ModelChanged; RefreshPage();
        Closing += OnClosing;
    }
    private bool cleanupComplete;
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (cleanupComplete) return;
        e.Cancel = true;
        try { await model.ShutdownAsync(); } catch (Exception error) { model.ReportError(error); }
        cleanupComplete = true; Close();
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedPage) || (e.PropertyName == nameof(MainViewModel.EditorRevision) && model.SelectedPage == "Connections") || (e.PropertyName == nameof(MainViewModel.Topology) && model.SelectedPage == "Lab 2"))
        {
            if (refreshQueued) return; refreshQueued = true;
            Dispatcher.BeginInvoke(() => { refreshQueued = false; RefreshPage(); }, DispatcherPriority.DataBind);
        }
    }
    private void RefreshPage() => page.Content = new ScrollViewer { Content = ViewFactory.Create(model.SelectedPage, model), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 14, 0) };
    public async Task CapturePageAsync(string path)
    {
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout(); SaveScreenshot(path);
        if (page.Content is ScrollViewer scroll && scroll.ScrollableHeight > 40)
        {
            scroll.ScrollToBottom(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            SaveScreenshot(Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-bottom.png"));
            scroll.ScrollToTop();
        }
    }
    public void SaveScreenshot(string path)
    {
        UpdateLayout(); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
}

internal static class Ui
{
    internal static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    internal static Binding Bind(string path, bool write = true) => new(path) { Mode = write ? BindingMode.TwoWay : BindingMode.OneWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, ValidatesOnExceptions = true };
    internal static TextBlock Text(string text, double size = 13, string color = "#F1F5F9", FontWeight? weight = null) => new() { Text = text, FontSize = size, Foreground = Brush(color), FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 7) };
    internal static TextBlock BoundText(string path, double size = 13, string color = "#F1F5F9", FontWeight? weight = null) { var text = Text("", size, color, weight); text.SetBinding(TextBlock.TextProperty, Bind(path, false)); return text; }
    internal static Button Button(string text, System.Windows.Input.ICommand command, bool primary = false)
    {
        var b = new Button { Content = text, Command = command }; if (primary) { b.Background = Brush("#5DE2C2"); b.Foreground = Brush("#102A29"); b.BorderBrush = Brush("#5DE2C2"); b.FontWeight = FontWeights.SemiBold; } AutomationProperties.SetName(b, text); return b;
    }
    internal static WrapPanel Actions(params UIElement[] children) { var p = new WrapPanel { Margin = new Thickness(0, 5, 0, 3) }; foreach (var c in children) p.Children.Add(c); return p; }
    internal static StackPanel Stack(params UIElement[] children) { var p = new StackPanel(); foreach (var c in children) p.Children.Add(c); return p; }
    internal static Border Card(string title, params UIElement[] children)
    {
        var stack = new StackPanel(); if (title.Length > 0) stack.Children.Add(Text(title, 16, "#EDF5FA", FontWeights.SemiBold)); foreach (var child in children) stack.Children.Add(child);
        return new Border { Background = Brush("#162334"), BorderBrush = Brush("#2C3D50"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 14), Child = stack };
    }
    internal static FrameworkElement Two(UIElement left, UIElement right, double leftWeight = 1, double rightWeight = 1)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new GridLength(leftWeight, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(16) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(rightWeight, GridUnitType.Star) }); grid.Children.Add(left); Grid.SetColumn(right, 2); grid.Children.Add(right);
        var stacked = false;
        grid.SizeChanged += (_, _) =>
        {
            var compact = grid.ActualWidth < 620;
            if (compact == stacked) return;
            stacked = compact; grid.RowDefinitions.Clear();
            if (compact)
            {
                grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
                Grid.SetColumnSpan(left, 3); Grid.SetColumnSpan(right, 3); Grid.SetColumn(right, 0); Grid.SetRow(right, 1);
            }
            else { Grid.SetColumnSpan(left, 1); Grid.SetColumnSpan(right, 1); Grid.SetColumn(right, 2); Grid.SetRow(right, 0); }
        };
        return grid;
    }
    internal static TextBox Input(string path, bool multiline = false)
    {
        var input = new TextBox { AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden };
        if (multiline) input.MinHeight = 85; input.SetBinding(TextBox.TextProperty, Bind(path)); return input;
    }
    internal static TextBox Output(string path, double size = 12)
    {
        var output = new TextBox { IsReadOnly = true, AcceptsReturn = true, FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = size, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap };
        output.SetBinding(TextBox.TextProperty, Bind(path, false)); return output;
    }
    internal static FrameworkElement Field(string label, FrameworkElement input, string? tip = null)
    {
        var text = Text(label, 12, "#BDD0DD"); if (tip is not null) { text.ToolTip = tip; input.ToolTip = tip; }
        AutomationProperties.SetName(input, label); AutomationProperties.SetLabeledBy(input, text);
        var stack = Stack(text, input); stack.Margin = new Thickness(0, 3, 0, 13); return stack;
    }
    internal static ComboBox Select(string path, System.Collections.IEnumerable items, string? display = null, string? value = null)
    {
        var combo = new ComboBox { ItemsSource = items }; if (display is not null) combo.DisplayMemberPath = display;
        else
        {
            var label = new FrameworkElementFactory(typeof(TextBlock));
            label.SetBinding(TextBlock.TextProperty, new Binding { Converter = DisplayLabelConverter.Instance });
            label.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            label.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
            combo.ItemTemplate = new DataTemplate { VisualTree = label };
        }
        if (value is null) combo.SetBinding(ComboBox.SelectedItemProperty, Bind(path)); else { combo.SelectedValuePath = value; combo.SetBinding(ComboBox.SelectedValueProperty, Bind(path)); } return combo;
    }
    internal static CheckBox Check(string label, string path) { var check = new CheckBox { Content = label }; check.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Bind(path)); return check; }
    internal static Button BoundButton(string labelPath, System.Windows.Input.ICommand command, bool primary = false)
    {
        var button = Button("", command, primary); button.SetBinding(ContentControl.ContentProperty, Bind(labelPath, false));
        if (labelPath == "ConnectLabel") button.SetBinding(UIElement.IsEnabledProperty, Bind("CanStartConnection", false));
        button.SetBinding(AutomationProperties.NameProperty, Bind(labelPath, false)); return button;
    }
    internal static Expander Disclosure(string title, params UIElement[] children)
    {
        var expander = new Expander { Header = title, Content = Stack(children), Margin = new Thickness(0, 0, 0, 10) };
        AutomationProperties.SetName(expander, title); return expander;
    }
    internal static DataTemplate NavigationTemplate()
    {
        var row = new FrameworkElementFactory(typeof(StackPanel)); row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var icon = new FrameworkElementFactory(typeof(TextBlock)); icon.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
        icon.SetValue(TextBlock.FontSizeProperty, 15.0); icon.SetValue(FrameworkElement.WidthProperty, 28.0);
        icon.SetBinding(TextBlock.TextProperty, new Binding { Converter = new NavigationGlyphConverter() }); row.AppendChild(icon);
        var label = new FrameworkElementFactory(typeof(TextBlock)); label.SetBinding(TextBlock.TextProperty, new Binding { Converter = DisplayLabelConverter.Instance }); row.AppendChild(label);
        return new DataTemplate { VisualTree = row };
    }
    internal static DataGrid Table(string path, params (string title, string property, double width)[] columns)
    {
        var grid = new DataGrid { IsReadOnly = true, MaxHeight = 350 }; grid.SetBinding(ItemsControl.ItemsSourceProperty, Bind(path, false));
        foreach (var c in columns)
        {
            var style=new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));
            style.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.NoWrap));
            style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,Bind(c.property,false)));
            grid.Columns.Add(new DataGridTextColumn { Header = c.title, Binding = Bind(c.property, false), ElementStyle=style, MinWidth=75, Width = new DataGridLength(c.width, DataGridLengthUnitType.Star) });
        }
        return grid;
    }
    internal static FrameworkElement Note(string text) => new Border { Background = Brush("#203442"), BorderBrush = Brush("#35556B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(12), Margin = new Thickness(0, 4, 0, 14), Child = Text(text, 12, "#C2D9E8") };
}
