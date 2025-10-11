using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatShouldIDo.Infrastructure.Options;

namespace WhatShouldIDo.Infrastructure.Services;

/// <summary>
/// Validates critical configuration at startup to fail fast with clear error messages
/// </summary>
public class StartupValidationService
{
    private readonly ILogger<StartupValidationService> _logger;
    private readonly OpenTripMapOptions _otmOptions;
    private readonly HybridOptions _hybridOptions;

    public StartupValidationService(
        ILogger<StartupValidationService> logger,
        IOptions<OpenTripMapOptions> otmOptions,
        IOptions<HybridOptions> hybridOptions)
    {
        _logger = logger;
        _otmOptions = otmOptions.Value;
        _hybridOptions = hybridOptions.Value;
    }

    /// <summary>
    /// Validate all critical configuration. Throws exception with actionable message if invalid.
    /// </summary>
    public void ValidateConfiguration()
    {
        _logger.LogInformation("🔍 Starting configuration validation...");

        var errors = new List<string>();

        // Validate OpenTripMap configuration
        if (_hybridOptions.Enabled)
        {
            if (string.IsNullOrWhiteSpace(_otmOptions.ApiKey))
            {
                errors.Add("OpenTripMap API key is missing. Set environment variable OPENTRIPMAP_API_KEY.");
            }
            else if (_otmOptions.ApiKey.StartsWith("${"))
            {
                errors.Add($"OpenTripMap API key not resolved: '{_otmOptions.ApiKey}'. Check environment variables.");
            }

            if (string.IsNullOrWhiteSpace(_otmOptions.BaseUrl))
            {
                errors.Add("OpenTripMap BaseUrl is not configured.");
            }

            if (_otmOptions.TimeoutMs <= 0 || _otmOptions.TimeoutMs > 30000)
            {
                _logger.LogWarning("⚠️ OpenTripMap timeout is unusual: {timeout}ms. Recommended: 3000-5000ms", _otmOptions.TimeoutMs);
            }
        }

        // Validate Hybrid options
        if (_hybridOptions.Enabled)
        {
            if (_hybridOptions.NearbyTtlMinutes <= 0)
            {
                errors.Add("HybridPlaces NearbyTtlMinutes must be > 0");
            }

            if (_hybridOptions.PromptTtlMinutes <= 0)
            {
                errors.Add("HybridPlaces PromptTtlMinutes must be > 0");
            }

            if (_hybridOptions.MinPrimaryResults < 0)
            {
                errors.Add("HybridPlaces MinPrimaryResults cannot be negative");
            }

            _logger.LogInformation(
                "✓ Hybrid Search Enabled | MinPrimaryResults: {min} | NearbyTTL: {nearbyTtl}min | PromptTTL: {promptTtl}min",
                _hybridOptions.MinPrimaryResults,
                _hybridOptions.NearbyTtlMinutes,
                _hybridOptions.PromptTtlMinutes);
        }
        else
        {
            _logger.LogInformation("ℹ️ Hybrid Search is disabled - using Google Places only");
        }

        if (errors.Count > 0)
        {
            var errorMessage = "❌ Configuration validation FAILED:\n" + string.Join("\n", errors.Select(e => $"  • {e}"));
            _logger.LogCritical(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogInformation("✅ Configuration validation passed");
    }

    /// <summary>
    /// Log configuration summary for diagnostics
    /// </summary>
    public void LogConfigurationSummary()
    {
        _logger.LogInformation("📋 Configuration Summary:");
        _logger.LogInformation("  Hybrid Search: {enabled}", _hybridOptions.Enabled ? "Enabled" : "Disabled");

        if (_hybridOptions.Enabled)
        {
            var apiKeyMasked = MaskApiKey(_otmOptions.ApiKey);
            _logger.LogInformation("  OpenTripMap API Key: {apiKey}", apiKeyMasked);
            _logger.LogInformation("  OpenTripMap BaseUrl: {baseUrl}", _otmOptions.BaseUrl);
            _logger.LogInformation("  OpenTripMap Timeout: {timeout}ms", _otmOptions.TimeoutMs);
            _logger.LogInformation("  Deduplication Distance: {meters}m", _hybridOptions.DedupMeters);
        }
    }

    private static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "<NOT SET>";

        if (apiKey.StartsWith("${"))
            return $"<NOT RESOLVED: {apiKey}>";

        if (apiKey.Length <= 8)
            return "***";

        return $"{apiKey.Substring(0, 4)}...{apiKey.Substring(apiKey.Length - 4)}";
    }
}
