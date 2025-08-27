namespace WhatShouldIDo.Infrastructure.Options
{
    public class DatabaseOptions
    {
        public int MaxRetryCount { get; set; } = 3;
        public int MaxRetryDelay { get; set; } = 5; // seconds
        public int CommandTimeout { get; set; } = 30; // seconds
        public string QueryTrackingBehavior { get; set; } = "NoTracking";
        public bool EnableSensitiveDataLogging { get; set; } = false;
        public bool EnableDetailedErrors { get; set; } = false;
    }
}