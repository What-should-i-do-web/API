namespace WhatShouldIDo.Infrastructure.Options;

public class CostGuardOptions
{
    public ProviderLimits Google { get; set; } = new();
    public ProviderLimits OpenTripMap { get; set; } = new();
    public double DegradeThresholdPct { get; set; } = 0.85;
}

public class ProviderLimits
{
    public int DailyCap { get; set; }
    public int RequestsPerMinute { get; set; }
    public decimal CostPerCallUsd { get; set; }
}