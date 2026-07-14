using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaudeCosts.App.Mvvm;
using ClaudeCosts.App.ViewModels;
using H.NotifyIcon;

namespace ClaudeCosts.App.Tray;

/// <summary>
/// Owns the system-tray icon: draws the current-period cost as a light rounded
/// tile with a dark, auto-fitted number, wires the context menu to the
/// view-model, and refreshes on <see cref="MainViewModel.TrayChanged"/>.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    // Rendered on a 64px canvas; Windows scales it down to the tray size.
    private const double Canvas = 64;
    private const double Inset = 3;      // keep the tile off the very edge
    private const double Radius = 12;    // rounded corners
    private const double PadFactor = 0.10;

    private static readonly Brush CardBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xF7, 0xF5, 0xF2)));
    private static readonly Brush TextBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x1C, 0x19, 0x17)));
    private static readonly Pen CardPen = FrozenPen(new Pen(new SolidColorBrush(Color.FromRgb(0xD8, 0xD4, 0xCF)), 2));
    private static readonly Typeface Face =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private readonly TaskbarIcon _icon;
    private readonly MainViewModel _vm;

    public TrayIconController(MainViewModel vm)
    {
        _vm = vm;
        _icon = new TaskbarIcon
        {
            ToolTipText = vm.TrayTooltip,
            LeftClickCommand = vm.ToggleWindowCommand,
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

    // H.NotifyIcon's IconSource pipeline expects ICO-formatted bytes, so we bypass it and
    // hand it a System.Drawing.Icon directly. The Icon property auto-disposes the previous
    // icon on replace and re-sends on an Explorer/taskbar restart.
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    private void UpdateIcon()
    {
        try
        {
            _icon.Icon = CreateIcon(RenderIcon(_vm.TrayText));
        }
        catch
        {
            // Keep the previous icon if a render cycle fails.
        }
    }

    /// <summary>Converts the rendered WPF bitmap into a System.Drawing.Icon that owns its handle.</summary>
    private static System.Drawing.Icon CreateIcon(RenderTargetBitmap rendered)
    {
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rendered));
        encoder.Save(stream);
        stream.Position = 0;

        using var gdiBitmap = new System.Drawing.Bitmap(stream);
        IntPtr hIcon = gdiBitmap.GetHicon();
        try
        {
            using var fromHandle = System.Drawing.Icon.FromHandle(hIcon);
            return (System.Drawing.Icon)fromHandle.Clone(); // Clone owns its handle → safe to dispose later
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    /// <summary>Draws a light rounded tile with the dark cost text auto-sized to fit.</summary>
    private static RenderTargetBitmap RenderIcon(string text)
    {
        var tile = new Rect(Inset, Inset, Canvas - 2 * Inset, Canvas - 2 * Inset);
        double pad = Canvas * PadFactor;
        double maxWidth = tile.Width - 2 * pad;
        double maxHeight = tile.Height - 2 * pad;

        // One-pass fit: FormattedText metrics scale linearly with the em size.
        const double probeSize = 40;
        var probe = MakeText(text, probeSize);
        double widthScale = probe.Width > 0 ? maxWidth / probe.Width : 1;
        double heightScale = probe.Height > 0 ? maxHeight / probe.Height : 1;
        double fontSize = Math.Max(8, probeSize * Math.Min(widthScale, heightScale));

        var ft = MakeText(text, fontSize);
        double x = tile.X + (tile.Width - ft.Width) / 2;
        double y = tile.Y + (tile.Height - ft.Height) / 2;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle(CardBrush, CardPen, tile, Radius, Radius);
            dc.DrawText(ft, new Point(x, y));
        }

        var bitmap = new RenderTargetBitmap((int)Canvas, (int)Canvas, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static FormattedText MakeText(string text, double size) => new(
        text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        Face,
        size,
        TextBrush,
        pixelsPerDip: 1.0);

    private static Brush Frozen(Brush b) { b.Freeze(); return b; }

    private static Pen FrozenPen(Pen p) { p.Freeze(); return p; }

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
        menu.Items.Add(Item("Einstellungen …", vm.OpenSettingsCommand));

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
