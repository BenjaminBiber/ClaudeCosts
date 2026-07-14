using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ClaudeCosts.App.Services;
using ClaudeCosts.App.Tray;
using ClaudeCosts.App.ViewModels;
using ClaudeCosts.Core.Pricing;

namespace ClaudeCosts.App;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private SettingsService _settingsService = null!;
    private AppSettings _settings = null!;
    private UsageService _usage = null!;
    private MainViewModel _vm = null!;
    private TrayIconController _tray = null!;
    private FileWatcherService _watcher = null!;
    private DispatcherTimer _timer = null!;
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance — a second launch just exits so we never get two tray icons.
        _instanceMutex = new Mutex(initiallyOwned: true, "ClaudeCosts.SingleInstance.9F3C", out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        _settingsService = new SettingsService();
        _settings = _settingsService.Load();

        var pricing = PricingTable.LoadOrCreate(SettingsService.PricingPath);
        _usage = new UsageService(pricing);

        _vm = new MainViewModel(
            _usage, _settings, _settingsService, new AutostartService(),
            showWindow: ShowMainWindow,
            exit: ExitApp);

        _tray = new TrayIconController(_vm);

        _watcher = new FileWatcherService();
        _watcher.Changed += OnUsageFilesChanged;
        _watcher.Start();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(15, _settings.RefreshIntervalSeconds)),
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        _ = _vm.ReloadAsync(); // initial background load

        // Optional: open the dashboard on launch (e.g. a Start-menu shortcut with --show).
        if (e.Args.Any(a => a.Equals("--show", StringComparison.OrdinalIgnoreCase)
                            || a.Equals("/show", StringComparison.OrdinalIgnoreCase)))
        {
            ShowMainWindow();
        }
    }

    private async void OnUsageFilesChanged(object? sender, EventArgs e) => await _vm.ReloadAsync();

    private async void OnTimerTick(object? sender, EventArgs e) => await _vm.ReloadAsync();

    private void ShowMainWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow { DataContext = _vm };
            ApplyPlacement(_window);
            _window.Closing += OnWindowClosing;
        }

        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        SavePlacement(_window);
        if (_window is not null)
            _window.Closing -= OnWindowClosing;
        _window = null; // recreate fresh next time (app stays alive in tray)
    }

    private void ExitApp()
    {
        SavePlacement(_window);
        _timer?.Stop();
        _watcher?.Dispose();
        _tray?.Dispose();
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        Shutdown();
    }

    private void ApplyPlacement(Window window)
    {
        if (_settings.WindowWidth is > 200 and { } w) window.Width = w;
        if (_settings.WindowHeight is > 150 and { } h) window.Height = h;

        if (_settings.WindowLeft is { } left && _settings.WindowTop is { } top &&
            IsOnScreen(left, top, window.Width, window.Height))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = left;
            window.Top = top;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    private void SavePlacement(Window? window)
    {
        if (window is null) return;

        var bounds = window.RestoreBounds; // normal-state bounds even if maximized/minimized
        if (bounds.Width < 200 || bounds.Height < 150) return;

        _settings.WindowLeft = bounds.Left;
        _settings.WindowTop = bounds.Top;
        _settings.WindowWidth = bounds.Width;
        _settings.WindowHeight = bounds.Height;
        _settingsService.Save(_settings);
    }

    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var virt = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        // require a chunk of the title bar to be visible
        return virt.IntersectsWith(new Rect(left, top, Math.Max(width, 1), 40));
    }
}
