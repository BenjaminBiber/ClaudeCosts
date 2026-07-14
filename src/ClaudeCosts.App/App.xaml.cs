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
    private bool _exiting;
    private DateTime _autoHiddenAt;

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
            toggleWindow: ToggleMainWindow,
            exit: ExitApp);
        _vm.RefreshIntervalChanged += OnRefreshIntervalChanged;

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

    private void OnRefreshIntervalChanged(object? sender, EventArgs e) =>
        _timer.Interval = TimeSpan.FromSeconds(Math.Max(15, _settings.RefreshIntervalSeconds));

    // Left-click on the tray icon toggles the flyout open/closed.
    private void ToggleMainWindow()
    {
        if (_window is { IsVisible: true })
        {
            _window.Hide();
            return;
        }

        // The tray click first deactivated (and auto-hid) the window; don't immediately reopen it.
        if (DateTime.UtcNow - _autoHiddenAt < TimeSpan.FromMilliseconds(300))
            return;

        ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow { DataContext = _vm };
            _window.Closing += OnWindowClosing;
            _window.Deactivated += OnWindowDeactivated;
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyPlacement(_window); // re-anchor to the bottom-right corner on every open
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
        // Bring to front. When pinned, keep it Topmost so it stays visible over the
        // window the user works in; otherwise drop Topmost again (plain flyout).
        _window.Topmost = true;
        if (!_vm.Pinned)
            _window.Topmost = false;
    }

    // Pinning must make the flyout stay visibly on top; unpinning releases that.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Pinned) && _window is { IsVisible: true })
            _window.Topmost = _vm.Pinned;
    }

    // Closing just hides the flyout; the app keeps living in the tray.
    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting) return;
        e.Cancel = true;
        _window?.Hide();
    }

    // Clicking elsewhere hides the flyout — unless the user pinned it open.
    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (_exiting || _vm.Pinned) return;
        if (_window is { IsVisible: true })
        {
            _autoHiddenAt = DateTime.UtcNow;
            _window.Hide();
        }
    }

    private void ExitApp()
    {
        _exiting = true;
        _timer?.Stop();
        _watcher?.Dispose();
        _tray?.Dispose();
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        Shutdown();
    }

    // Anchor the window to the bottom-right of the primary work area (above the taskbar),
    // like a tray flyout. Size is the fixed compact default from XAML; position is not persisted.
    private const double EdgeMargin = 12;

    private void ApplyPlacement(Window window)
    {
        var area = SystemParameters.WorkArea; // DIPs, matches WPF Left/Top
        double width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        double height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = Math.Max(area.Left, area.Right - width - EdgeMargin);
        window.Top = Math.Max(area.Top, area.Bottom - height - EdgeMargin);
    }
}
