using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Options;

namespace WhatShouldIDo.Infrastructure.Services.AI
{
    /// <summary>
    /// Hybrid AI orchestrator that manages multiple providers with health checks,
    /// priority-based selection, and automatic failover.
    /// </summary>
    public class HybridAIOrchestrator
    {
        private readonly IEnumerable<IAIProvider> _providers;
        private readonly AIOptions _options;
        private readonly ILogger<HybridAIOrchestrator> _logger;
        private readonly IMetricsService? _metrics;
        private readonly Dictionary<string, DateTime> _providerHealthStatus;
        private readonly SemaphoreSlim _healthCheckLock = new(1, 1);

        public HybridAIOrchestrator(
            IEnumerable<IAIProvider> providers,
            IOptions<AIOptions> options,
            ILogger<HybridAIOrchestrator> logger,
            IMetricsService? metrics = null)
        {
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics;
            _providerHealthStatus = new Dictionary<string, DateTime>();
        }

        /// <summary>
        /// Selects the best available provider for chat operations based on priority and health.
        /// </summary>
        public async Task<IAIProvider> SelectProviderForChatAsync(CancellationToken cancellationToken = default)
        {
            return await SelectProviderAsync("Chat", _options.ProviderPriority?.Chat, cancellationToken);
        }

        /// <summary>
        /// Selects the best available provider for embedding operations based on priority and health.
        /// </summary>
        public async Task<IAIProvider> SelectProviderForEmbeddingAsync(CancellationToken cancellationToken = default)
        {
            return await SelectProviderAsync("Embedding", _options.ProviderPriority?.Embedding, cancellationToken);
        }

        /// <summary>
        /// Executes an operation with automatic provider selection and failover.
        /// </summary>
        public async Task<T> ExecuteWithFailoverAsync<T>(
            Func<IAIProvider, Task<T>> operation,
            string capability,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            var priorityList = capability.ToLowerInvariant() switch
            {
                "chat" => _options.ProviderPriority?.Chat,
                "embedding" => _options.ProviderPriority?.Embedding,
                _ => _options.ProviderPriority?.Chat
            };

            var orderedProviders = GetOrderedProviders(priorityList);
            Exception? lastException = null;

            foreach (var provider in orderedProviders)
            {
                // Skip unhealthy providers
                if (!await IsProviderHealthyAsync(provider, cancellationToken))
                {
                    _logger.LogWarning("Skipping unhealthy provider: {Provider}", provider.Name);
                    continue;
                }

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    _logger.LogInformation("Executing {Operation} with provider: {Provider}",
                        operationName, provider.Name);

                    var result = await operation(provider);

                    stopwatch.Stop();

                    // Record success metrics
                    _metrics?.RecordAIProviderSelected(provider.Name);
                    _metrics?.RecordAICallLatency(provider.Name, operationName, stopwatch.Elapsed.TotalSeconds);
                    _metrics?.IncrementAICallSuccess(provider.Name);

                    _logger.LogInformation("Successfully executed {Operation} with {Provider} in {ElapsedMs}ms",
                        operationName, provider.Name, stopwatch.ElapsedMilliseconds);

                    return result;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    lastException = ex;

                    // Record failure metrics
                    _metrics?.IncrementAICallFailure(provider.Name, ex.GetType().Name);

                    _logger.LogWarning(ex, "Provider {Provider} failed for {Operation}, trying next provider",
                        provider.Name, operationName);

                    // Mark provider as potentially unhealthy
                    await MarkProviderUnhealthyAsync(provider.Name);
                }
            }

            // All providers failed
            _logger.LogError(lastException, "All providers failed for {Operation}", operationName);
            throw new InvalidOperationException(
                $"All AI providers failed for operation: {operationName}",
                lastException);
        }

        /// <summary>
        /// Gets health status of all registered providers.
        /// </summary>
        public async Task<Dictionary<string, bool>> GetProvidersHealthAsync(CancellationToken cancellationToken = default)
        {
            var healthStatus = new Dictionary<string, bool>();

            foreach (var provider in _providers)
            {
                try
                {
                    var isHealthy = await provider.IsHealthyAsync(cancellationToken);
                    healthStatus[provider.Name] = isHealthy;

                    if (isHealthy)
                    {
                        await MarkProviderHealthyAsync(provider.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Health check failed for provider: {Provider}", provider.Name);
                    healthStatus[provider.Name] = false;
                    await MarkProviderUnhealthyAsync(provider.Name);
                }
            }

            return healthStatus;
        }

        private async Task<IAIProvider> SelectProviderAsync(
            string capability,
            List<string>? priorityList,
            CancellationToken cancellationToken)
        {
            var orderedProviders = GetOrderedProviders(priorityList);

            foreach (var provider in orderedProviders)
            {
                if (await IsProviderHealthyAsync(provider, cancellationToken))
                {
                    _logger.LogDebug("Selected provider {Provider} for {Capability}",
                        provider.Name, capability);
                    _metrics?.RecordAIProviderSelected(provider.Name);
                    return provider;
                }
            }

            // Fallback to first available provider
            var fallbackProvider = orderedProviders.FirstOrDefault();
            if (fallbackProvider != null)
            {
                _logger.LogWarning("No healthy provider found for {Capability}, using fallback: {Provider}",
                    capability, fallbackProvider.Name);
                return fallbackProvider;
            }

            throw new InvalidOperationException($"No AI providers available for capability: {capability}");
        }

        private List<IAIProvider> GetOrderedProviders(List<string>? priorityList)
        {
            if (priorityList == null || priorityList.Count == 0)
            {
                return _providers.ToList();
            }

            var providerDict = _providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var ordered = new List<IAIProvider>();

            foreach (var providerName in priorityList)
            {
                if (providerDict.TryGetValue(providerName, out var provider))
                {
                    ordered.Add(provider);
                    providerDict.Remove(providerName);
                }
            }

            // Add remaining providers at the end
            ordered.AddRange(providerDict.Values);

            return ordered;
        }

        private async Task<bool> IsProviderHealthyAsync(IAIProvider provider, CancellationToken cancellationToken)
        {
            await _healthCheckLock.WaitAsync(cancellationToken);
            try
            {
                // Check last known status
                if (_providerHealthStatus.TryGetValue(provider.Name, out var lastUnhealthyTime))
                {
                    // If provider was marked unhealthy less than 5 minutes ago, skip
                    if (DateTime.UtcNow - lastUnhealthyTime < TimeSpan.FromMinutes(5))
                    {
                        return false;
                    }

                    // Remove old status, will recheck
                    _providerHealthStatus.Remove(provider.Name);
                }

                // Perform health check
                return await provider.IsHealthyAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check exception for provider: {Provider}", provider.Name);
                return false;
            }
            finally
            {
                _healthCheckLock.Release();
            }
        }

        private async Task MarkProviderUnhealthyAsync(string providerName)
        {
            await _healthCheckLock.WaitAsync();
            try
            {
                _providerHealthStatus[providerName] = DateTime.UtcNow;
                _logger.LogWarning("Provider {Provider} marked as unhealthy", providerName);
            }
            finally
            {
                _healthCheckLock.Release();
            }
        }

        private async Task MarkProviderHealthyAsync(string providerName)
        {
            await _healthCheckLock.WaitAsync();
            try
            {
                if (_providerHealthStatus.ContainsKey(providerName))
                {
                    _providerHealthStatus.Remove(providerName);
                    _logger.LogInformation("Provider {Provider} restored to healthy status", providerName);
                }
            }
            finally
            {
                _healthCheckLock.Release();
            }
        }
    }
}
