namespace WhatShouldIDo.Infrastructure.Options;

public class HybridOptions
{
    public bool Enabled { get; set; } = true;
    public int PrimaryTake { get; set; } = 40;
    public int MinPrimaryResults { get; set; } = 25;
    public double DedupMeters { get; set; } = 70;
    public int NearbyTtlMinutes { get; set; } = 30;
    public int PromptTtlMinutes { get; set; } = 15;
    public bool ForceTourismKinds { get; set; } = false;
}