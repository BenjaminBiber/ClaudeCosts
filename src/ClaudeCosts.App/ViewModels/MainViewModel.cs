using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using ClaudeCosts.App.Formatting;
using ClaudeCosts.App.Mvvm;
using ClaudeCosts.App.Services;
using ClaudeCosts.Core.Aggregation;

namespace ClaudeCosts.App.ViewModels;

/// <summary>
/// Single source of UI-facing state for both the window and the tray icon.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly UsageService _usage;
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly AutostartService _autostart;
    private readonly Action _showWindow;
    private readonly Action _toggleWindow;
    private readonly Action _exit;

    private bool _autostartEnabled;
    private bool _showSettings;
    private bool _pinned;
    private bool _isLoading;
    private bool _hasNoData;
    private string _headline = "$0.00";
    private string _headlineCaption = "";
    private string _periodTypeName = "";
    private string _bucketsHeader = "";
    private string _statusMessage = "";
    private string _lastUpdatedText = "";
    private string _trayText = "$0";
    private string _trayTooltip = "Claude-Kosten";
    private IReadOnlyList<BucketRow> _buckets = Array.Empty<BucketRow>();
    private IReadOnlyList<ModelRow> _models = Array.Empty<ModelRow>();

    public MainViewModel(
        UsageService usage,
        AppSettings settings,
        SettingsService settingsService,
        AutostartService autostart,
        Action showWindow,
        Action toggleWindow,
        Action exit)
    {
        _usage = usage;
        _settings = settings;
        _settingsService = settingsService;
        _autostart = autostart;
        _showWindow = showWindow;
        _toggleWindow = toggleWindow;
        _exit = exit;
        _autostartEnabled = autostart.IsEnabled();

        ReloadCommand = new RelayCommand(() => _ = ReloadAsync());
        ShowWindowCommand = new RelayCommand(() => _showWindow());
        ToggleWindowCommand = new RelayCommand(() => _toggleWindow());
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        CloseSettingsCommand = new RelayCommand(() => ShowSettings = false);
        ExitCommand = new RelayCommand(() => _exit());
        SetGranularityCommand = new RelayCommand(SetGranularity);
        OpenPricingCommand = new RelayCommand(OpenPricingFile);
        OpenFolderCommand = new RelayCommand(OpenDataFolder);

        _usage.Updated += (_, _) => Refresh();
        Refresh();
    }

    // --- Commands ---
    public ICommand ReloadCommand { get; }
    public ICommand ShowWindowCommand { get; }
    public ICommand ToggleWindowCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand CloseSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand SetGranularityCommand { get; }
    public ICommand OpenPricingCommand { get; }
    public ICommand OpenFolderCommand { get; }

    /// <summary>Raised after a refresh so the tray icon can update its generated image/tooltip.</summary>
    public event EventHandler? TrayChanged;

    /// <summary>Raised when the refresh interval changes so the host can retune its timer.</summary>
    public event EventHandler? RefreshIntervalChanged;

    // --- Bindable state ---
    public Granularity Granularity
    {
        get => _settings.TrayPeriod;
        set
        {
            if (_settings.TrayPeriod == value) return;
            _settings.TrayPeriod = value;
            _settingsService.Save(_settings);
            OnPropertyChanged();
            Refresh();
        }
    }

    public bool Autostart
    {
        get => _autostartEnabled;
        set
        {
            _autostart.SetEnabled(value);
            _autostartEnabled = _autostart.IsEnabled();
            OnPropertyChanged();
        }
    }

    public bool WeekStartsMonday
    {
        get => _settings.WeekStartsMonday;
        set
        {
            if (_settings.WeekStartsMonday == value) return;
            _settings.WeekStartsMonday = value;
            _settingsService.Save(_settings);
            OnPropertyChanged();
            Refresh();
        }
    }

    public int RefreshIntervalSeconds
    {
        get => _settings.RefreshIntervalSeconds;
        set
        {
            if (_settings.RefreshIntervalSeconds == value) return;
            _settings.RefreshIntervalSeconds = value;
            _settingsService.Save(_settings);
            OnPropertyChanged();
            RefreshIntervalChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>When true, the window shows the in-window settings page instead of the tabs.</summary>
    public bool ShowSettings { get => _showSettings; set => SetProperty(ref _showSettings, value); }

    /// <summary>When true, the flyout stays open on focus loss instead of auto-hiding.</summary>
    public bool Pinned { get => _pinned; set => SetProperty(ref _pinned, value); }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool HasNoData { get => _hasNoData; private set => SetProperty(ref _hasNoData, value); }
    public string Headline { get => _headline; private set => SetProperty(ref _headline, value); }
    public string HeadlineCaption { get => _headlineCaption; private set => SetProperty(ref _headlineCaption, value); }
    public string PeriodTypeName { get => _periodTypeName; private set => SetProperty(ref _periodTypeName, value); }
    public string BucketsHeader { get => _bucketsHeader; private set => SetProperty(ref _bucketsHeader, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string LastUpdatedText { get => _lastUpdatedText; private set => SetProperty(ref _lastUpdatedText, value); }
    public IReadOnlyList<BucketRow> Buckets { get => _buckets; private set => SetProperty(ref _buckets, value); }
    public IReadOnlyList<ModelRow> Models { get => _models; private set => SetProperty(ref _models, value); }

    public string TrayText { get => _trayText; private set => SetProperty(ref _trayText, value); }
    public string TrayTooltip { get => _trayTooltip; private set => SetProperty(ref _trayTooltip, value); }

    public async Task ReloadAsync()
    {
        IsLoading = true;
        StatusMessage = "Lade Nutzungsdaten …";
        try
        {
            await _usage.ReloadAsync(); // fires Updated → Refresh()
        }
        catch (Exception ex)
        {
            StatusMessage = "Fehler beim Laden: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Shared by the header gear and the tray menu: make sure the window is up, then reveal settings.
    private void OpenSettings()
    {
        ShowSettings = true;
        _showWindow();
    }

    private void SetGranularity(object? parameter)
    {
        Granularity g = parameter switch
        {
            Granularity granularity => granularity,
            string s when Enum.TryParse<Granularity>(s, out var parsed) => parsed,
            _ => Granularity,
        };
        Granularity = g;
    }

    private void Refresh()
    {
        var g = _settings.TrayPeriod;
        bool weekMon = _settings.WeekStartsMonday;

        var current = _usage.CurrentPeriod(g, weekMon);
        var buckets = _usage.Aggregate(g, weekMon);

        Headline = Format.Money(current.Cost);
        HeadlineCaption = Format.PeriodLabel(current);
        PeriodTypeName = Format.PeriodTypeName(g);
        BucketsHeader = g switch
        {
            Granularity.Day => "Nach Tag",
            Granularity.Week => "Nach Woche",
            Granularity.Month => "Nach Monat",
            Granularity.All => "Gesamt",
            _ => "Verlauf",
        };

        double maxBucketCost = buckets.Count > 0 ? buckets.Max(b => b.Cost) : 0;
        var currentKey = current.PeriodStart ?? DateOnly.MinValue;
        Buckets = buckets.Take(60).Select(b => new BucketRow
        {
            Label = Format.PeriodLabel(b),
            CostText = Format.Money(b.Cost),
            TokensText = Format.Tokens(b.TotalTokens),
            Percent = maxBucketCost > 0 ? b.Cost / maxBucketCost * 100 : 0,
            IsCurrent = (b.PeriodStart ?? DateOnly.MinValue) == currentKey,
        }).ToList();

        var models = current.Models;
        double maxModelCost = models.Count > 0 ? models.Max(m => m.Cost) : 0;
        Models = models.Select(m => new ModelRow
        {
            Model = ShortModel(m.Model),
            CostText = Format.Money(m.Cost),
            TokensText = Format.Tokens(m.TotalTokens),
            Percent = maxModelCost > 0 ? m.Cost / maxModelCost * 100 : 0,
            Known = m.KnownPricing,
        }).ToList();

        HasNoData = !_usage.HasData;
        StatusMessage = HasNoData
            ? "Keine Nutzungsdaten gefunden. Sobald du Claude Code nutzt, erscheinen die Kosten hier."
            : "";

        LastUpdatedText = _usage.LastLoadedUtc is { } t
            ? "Stand: " + t.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss")
            : "";

        TrayText = Format.TrayText(current.Cost);
        TrayTooltip = BuildTooltip(current);

        TrayChanged?.Invoke(this, EventArgs.Empty);
    }

    private string BuildTooltip(UsageBucket current)
    {
        var lines = new List<string>
        {
            $"Claude-Kosten · {Format.PeriodTypeName(current.Granularity)}",
            $"{Format.PeriodLabel(current)}: {Format.Money(current.Cost)}",
        };

        if (current.Models.Count > 0)
        {
            var top = current.Models[0];
            lines.Add($"Top: {ShortModel(top.Model)} {Format.Money(top.Cost)}");
        }

        if (!string.IsNullOrEmpty(LastUpdatedText))
            lines.Add(LastUpdatedText);

        return string.Join(Environment.NewLine, lines);
    }

    private static string ShortModel(string model) =>
        model.StartsWith("claude-", StringComparison.OrdinalIgnoreCase) ? model["claude-".Length..] : model;

    private void OpenPricingFile()
    {
        try
        {
            // Ensure the file exists so the editor has something to open.
            if (!File.Exists(SettingsService.PricingPath))
                Core.Pricing.PricingTable.LoadOrCreate(SettingsService.PricingPath);

            Process.Start(new ProcessStartInfo(SettingsService.PricingPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = "Konnte Preisdatei nicht öffnen: " + ex.Message;
        }
    }

    private void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(SettingsService.AppDataDir);
            Process.Start(new ProcessStartInfo(SettingsService.AppDataDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = "Konnte Ordner nicht öffnen: " + ex.Message;
        }
    }
}
