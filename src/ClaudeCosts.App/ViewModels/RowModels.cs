namespace ClaudeCosts.App.ViewModels;

/// <summary>A single period row in the window's breakdown list.</summary>
public sealed class BucketRow
{
    public required string Label { get; init; }
    public required string CostText { get; init; }
    public required string TokensText { get; init; }
    public double Percent { get; init; }
    public bool IsCurrent { get; init; }
}

/// <summary>A single model row in the per-model breakdown.</summary>
public sealed class ModelRow
{
    public required string Model { get; init; }
    public required string CostText { get; init; }
    public required string TokensText { get; init; }
    public double Percent { get; init; }
    public bool Known { get; init; }
    public string PricingNote => Known ? "" : "Preis unbekannt";
}
