using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeCosts.Core.Pricing;

/// <summary>
/// Resolves a model id to its <see cref="ModelPricing"/> via prefix matching,
/// and can be persisted to / loaded from an editable JSON file so new models
/// can be priced without recompiling.
/// </summary>
public sealed class PricingTable
{
    private readonly List<PricingRule> _rules;

    public PricingTable(IEnumerable<PricingRule> rules)
    {
        // Longest prefix wins: order by descending prefix length.
        _rules = rules.OrderByDescending(r => r.Prefix.Length).ToList();
    }

    public IReadOnlyList<PricingRule> Rules => _rules;

    /// <summary>
    /// Returns the pricing for <paramref name="model"/>, or <c>null</c> when no
    /// rule matches (unknown model → treated as cost 0 by the calculator).
    /// </summary>
    public ModelPricing? Resolve(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return null;
        var m = model.Trim();

        foreach (var rule in _rules)
        {
            if (string.Equals(rule.Prefix, m, StringComparison.OrdinalIgnoreCase))
                return rule.Pricing;
        }
        foreach (var rule in _rules) // already longest-first
        {
            if (m.StartsWith(rule.Prefix, StringComparison.OrdinalIgnoreCase))
                return rule.Pricing;
        }
        return null;
    }

    /// <summary>Built-in prices (USD per 1M tokens) for the current + legacy Claude families.</summary>
    public static PricingTable Defaults() => new(new[]
    {
        // --- current families ---
        new PricingRule("claude-opus-4",     new ModelPricing { InputPerMTok = 5,    OutputPerMTok = 25 }),   // 4-8 / 4-7 / 4-6 / 4-5 / 4-1 / 4-0
        new PricingRule("claude-sonnet-5",   new ModelPricing { InputPerMTok = 3,    OutputPerMTok = 15 }),
        new PricingRule("claude-sonnet-4",   new ModelPricing { InputPerMTok = 3,    OutputPerMTok = 15 }),   // 4-6 / 4-5 / 4-0
        new PricingRule("claude-haiku-4",    new ModelPricing { InputPerMTok = 1,    OutputPerMTok = 5 }),    // 4-5
        new PricingRule("claude-fable-5",    new ModelPricing { InputPerMTok = 10,   OutputPerMTok = 50 }),
        new PricingRule("claude-mythos-5",   new ModelPricing { InputPerMTok = 10,   OutputPerMTok = 50 }),
        // --- legacy (3.x) families ---
        new PricingRule("claude-3-opus",     new ModelPricing { InputPerMTok = 15,   OutputPerMTok = 75 }),
        new PricingRule("claude-3-5-sonnet", new ModelPricing { InputPerMTok = 3,    OutputPerMTok = 15 }),
        new PricingRule("claude-3-7-sonnet", new ModelPricing { InputPerMTok = 3,    OutputPerMTok = 15 }),
        new PricingRule("claude-3-sonnet",   new ModelPricing { InputPerMTok = 3,    OutputPerMTok = 15 }),
        new PricingRule("claude-3-5-haiku",  new ModelPricing { InputPerMTok = 0.80, OutputPerMTok = 4 }),
        new PricingRule("claude-3-haiku",    new ModelPricing { InputPerMTok = 0.25, OutputPerMTok = 1.25 }),
    });

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Loads the table from <paramref name="path"/>. If the file is missing or
    /// unreadable, the built-in defaults are written there and returned.
    /// </summary>
    public static PricingTable LoadOrCreate(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var dto = JsonSerializer.Deserialize<PricingFile>(File.ReadAllText(path), JsonOptions);
                if (dto?.Rules is { Count: > 0 })
                    return FromDto(dto);
            }
        }
        catch
        {
            // Corrupt/locked file → fall through to defaults (do not crash the app).
        }

        var defaults = Defaults();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            defaults.Save(path);
        }
        catch
        {
            // Non-fatal: run with in-memory defaults even if we cannot persist.
        }
        return defaults;
    }

    public void Save(string path)
    {
        var dto = new PricingFile
        {
            Rules = _rules.Select(r => new PricingRuleDto
            {
                Prefix = r.Prefix,
                InputPerMTok = r.Pricing.InputPerMTok,
                OutputPerMTok = r.Pricing.OutputPerMTok,
                CacheWrite5mMultiplier = r.Pricing.CacheWrite5mMultiplier,
                CacheWrite1hMultiplier = r.Pricing.CacheWrite1hMultiplier,
                CacheReadMultiplier = r.Pricing.CacheReadMultiplier,
            }).ToList(),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOptions));
    }

    private static PricingTable FromDto(PricingFile dto) => new(dto.Rules!.Select(d => new PricingRule(
        d.Prefix,
        new ModelPricing
        {
            InputPerMTok = d.InputPerMTok,
            OutputPerMTok = d.OutputPerMTok,
            CacheWrite5mMultiplier = d.CacheWrite5mMultiplier ?? 1.25,
            CacheWrite1hMultiplier = d.CacheWrite1hMultiplier ?? 2.0,
            CacheReadMultiplier = d.CacheReadMultiplier ?? 0.10,
        })));

    private sealed class PricingFile
    {
        public List<PricingRuleDto>? Rules { get; set; }
    }

    private sealed class PricingRuleDto
    {
        public string Prefix { get; set; } = "";
        public double InputPerMTok { get; set; }
        public double OutputPerMTok { get; set; }
        public double? CacheWrite5mMultiplier { get; set; }
        public double? CacheWrite1hMultiplier { get; set; }
        public double? CacheReadMultiplier { get; set; }
    }
}
