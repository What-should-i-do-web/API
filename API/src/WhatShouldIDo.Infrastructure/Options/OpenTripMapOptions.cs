namespace WhatShouldIDo.Infrastructure.Options;

public class OpenTripMapOptions
{
    public string BaseUrl { get; set; } = "https://api.opentripmap.com";
    public string ApiKey { get; set; } = "";
    public string[] Kinds { get; set; } = Array.Empty<string>();
    public int TimeoutMs { get; set; } = 5000;
}