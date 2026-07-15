using ClaudeCosts.Core.Aggregation;

namespace ClaudeCosts.App.Services;

/// <summary>User-configurable settings, persisted as JSON. Autostart lives in the registry, not here.</summary>
public sealed class AppSettings
{
    /// <summary>Which period the tray value and the window headline reflect.</summary>
    public Granularity TrayPeriod { get; set; } = Granularity.Month;

    public bool WeekStartsMonday { get; set; } = true;

    /// <summary>When true, costs are shown as a number of Big Macs instead of US dollars.</summary>
    public bool DisplayInBigMacs { get; set; } = false;

    /// <summary>Safety-net full refresh interval, in seconds.</summary>
    public int RefreshIntervalSeconds { get; set; } = 60;
}
