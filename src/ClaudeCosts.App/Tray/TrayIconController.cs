using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ClaudeCosts.App.Mvvm;
using ClaudeCosts.App.ViewModels;
using H.NotifyIcon;

namespace ClaudeCosts.App.Tray;

/// <summary>
/// Owns the system-tray icon: renders the current-period cost as the icon image
/// via <see cref="GeneratedIconSource"/>, wires the context menu to the view-model,
/// and refreshes on <see cref="MainViewModel.TrayChanged"/>.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    // Claude-ish accent so short white text is legible on both light and dark taskbars.
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x57));
    private static readonly Brush TextBrush = new SolidColorBrush(Colors.White);

    private readonly TaskbarIcon _icon;
    private readonly MainViewModel _vm;

    public TrayIconController(MainViewModel vm)
    {
        _vm = vm;
        Accent.Freeze();
        TextBrush.Freeze();

        _icon = new TaskbarIcon
        {
            ToolTipText = vm.TrayTooltip,
            LeftClickCommand = vm.ShowWindowCommand,
            NoLeftClickDelay = true,
            ContextMenu = BuildMenu(vm),
        };

        UpdateIcon();
        _icon.ForceCreate();

        _vm.TrayChanged += OnTrayChanged;
    }

    private void OnTrayChanged(object? sender, EventArgs e)
    {
        UpdateIcon();
        _icon.ToolTipText = _vm.TrayTooltip;
    }

    private void UpdateIcon()
    {
        _icon.IconSource = new GeneratedIconSource
        {
            Text = _vm.TrayText,
            FontSize = _vm.TrayFontSize,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            Background = Accent,
            Size = 128,
        };
    }

    private static ContextMenu BuildMenu(MainViewModel vm)
    {
        var menu = new ContextMenu { DataContext = vm };

        menu.Items.Add(Item("Fenster öffnen", vm.ShowWindowCommand));
        menu.Items.Add(new Separator());

        var period = new MenuItem { Header = "Zeitraum" };
        period.Items.Add(GranularityItem(vm, "Tag", "Day"));
        period.Items.Add(GranularityItem(vm, "Woche", "Week"));
        period.Items.Add(GranularityItem(vm, "Monat", "Month"));
        period.Items.Add(GranularityItem(vm, "Gesamt", "All"));
        menu.Items.Add(period);

        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Aktualisieren", vm.ReloadCommand));

        var autostart = new MenuItem { Header = "Autostart mit Windows", IsCheckable = true };
        autostart.SetBinding(MenuItem.IsCheckedProperty,
            new Binding(nameof(MainViewModel.Autostart)) { Mode = BindingMode.TwoWay });
        menu.Items.Add(autostart);

        menu.Items.Add(Item("Preise bearbeiten …", vm.OpenPricingCommand));
        menu.Items.Add(Item("Datenordner öffnen …", vm.OpenFolderCommand));

        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Beenden", vm.ExitCommand));

        return menu;
    }

    private static MenuItem Item(string header, ICommand command) => new() { Header = header, Command = command };

    private static MenuItem GranularityItem(MainViewModel vm, string header, string value)
    {
        var item = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            Command = vm.SetGranularityCommand,
            CommandParameter = value,
        };
        item.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(MainViewModel.Granularity))
        {
            Converter = new EnumMatchToBooleanConverter(),
            ConverterParameter = value,
            Mode = BindingMode.OneWay,
        });
        return item;
    }

    public void Dispose()
    {
        _vm.TrayChanged -= OnTrayChanged;
        _icon.Dispose();
    }
}
