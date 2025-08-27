using WhatShouldIDo.Infrastructure.Options;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace WhatShouldIDo.Infrastructure.Services;

public class CostGuard
{
    private readonly CostGuardOptions _options;
    private readonly ConcurrentDictionary<string, ProviderUsage> _usage = new();

    public CostGuard(IOptions<CostGuardOptions> options)
    {
        _options = options.Value;
    }

    public bool CanCall(string provider)
    {
        var usage = GetOrCreateUsage(provider);
        var limits = GetProviderLimits(provider);
        
        return usage.DailyCount < limits.DailyCap && 
               usage.GetRpm() < limits.RequestsPerMinute;
    }

    public bool ShouldDegrade(string provider)
    {
        var usage = GetOrCreateUsage(provider);
        var limits = GetProviderLimits(provider);
        
        var dailyPct = (double)usage.DailyCount / limits.DailyCap;
        var rpmPct = (double)usage.GetRpm() / limits.RequestsPerMinute;
        
        return Math.Max(dailyPct, rpmPct) >= _options.DegradeThresholdPct;
    }

    public int GetDegradedRadius(string provider, int originalRadius)
    {
        if (!ShouldDegrade(provider)) return originalRadius;
        return Math.Max(1000, originalRadius * 60 / 100);
    }

    public void NotifyCall(string provider)
    {
        var usage = GetOrCreateUsage(provider);
        usage.RecordCall();
    }

    private ProviderUsage GetOrCreateUsage(string provider) =>
        _usage.GetOrAdd(provider, _ => new ProviderUsage());

    private ProviderLimits GetProviderLimits(string provider) =>
        provider switch
        {
            "Google" => _options.Google,
            "OpenTripMap" => _options.OpenTripMap,
            _ => throw new ArgumentException($"Unknown provider: {provider}")
        };

    private class ProviderUsage
    {
        private readonly Queue<DateTime> _recentCalls = new();
        public int DailyCount { get; private set; }
        private DateTime _lastDayReset = DateTime.UtcNow.Date;

        public void RecordCall()
        {
            var now = DateTime.UtcNow;
            
            if (now.Date > _lastDayReset)
            {
                DailyCount = 0;
                _lastDayReset = now.Date;
            }
            
            DailyCount++;
            
            _recentCalls.Enqueue(now);
            while (_recentCalls.Count > 0 && _recentCalls.Peek() < now.AddMinutes(-1))
                _recentCalls.Dequeue();
        }

        public int GetRpm() => _recentCalls.Count;
    }
}