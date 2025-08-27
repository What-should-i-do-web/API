using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace WhatShouldIDo.Infrastructure.Interceptors
{
    public class QueryPerformanceInterceptor : DbCommandInterceptor
    {
        private readonly ILogger<QueryPerformanceInterceptor> _logger;
        private const int SlowQueryThresholdMs = 1000; // 1 second threshold for slow queries
        
        public QueryPerformanceInterceptor(ILogger<QueryPerformanceInterceptor> logger)
        {
            _logger = logger;
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            LogQueryPerformance(command, eventData);
            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }

        public override DbDataReader ReaderExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result)
        {
            LogQueryPerformance(command, eventData);
            return base.ReaderExecuted(command, eventData, result);
        }

        public override ValueTask<object?> ScalarExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            object? result,
            CancellationToken cancellationToken = default)
        {
            LogQueryPerformance(command, eventData);
            return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
        }

        public override object? ScalarExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            object? result)
        {
            LogQueryPerformance(command, eventData);
            return base.ScalarExecuted(command, eventData, result);
        }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            LogQueryPerformance(command, eventData);
            return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
        }

        public override int NonQueryExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result)
        {
            LogQueryPerformance(command, eventData);
            return base.NonQueryExecuted(command, eventData, result);
        }

        private void LogQueryPerformance(DbCommand command, CommandExecutedEventData eventData)
        {
            var executionTime = eventData.Duration.TotalMilliseconds;
            
            // Always track performance metrics
            QueryPerformanceMetrics.RecordQuery(executionTime, command.CommandText);
            
            // Log slow queries
            if (executionTime > SlowQueryThresholdMs)
            {
                _logger.LogWarning(
                    "Slow query detected: {ExecutionTime}ms - {CommandText}",
                    executionTime,
                    TruncateQuery(command.CommandText));
            }
            else if (executionTime > 500) // Log moderately slow queries at debug level
            {
                _logger.LogDebug(
                    "Query executed in {ExecutionTime}ms - {CommandText}",
                    executionTime,
                    TruncateQuery(command.CommandText));
            }
        }

        private static string TruncateQuery(string query)
        {
            const int maxLength = 200;
            if (query.Length <= maxLength)
                return query;
            
            return query.Substring(0, maxLength) + "...";
        }
    }

    public static class QueryPerformanceMetrics
    {
        private static readonly object _lock = new object();
        private static readonly List<QueryMetric> _recentQueries = new List<QueryMetric>();
        private static readonly Dictionary<string, QueryStats> _queryStats = new Dictionary<string, QueryStats>();
        private const int MaxRecentQueries = 100;

        public static void RecordQuery(double executionTimeMs, string query)
        {
            var metric = new QueryMetric
            {
                ExecutionTime = executionTimeMs,
                Query = TruncateQueryForStats(query),
                Timestamp = DateTime.UtcNow
            };

            lock (_lock)
            {
                // Add to recent queries
                _recentQueries.Add(metric);
                if (_recentQueries.Count > MaxRecentQueries)
                {
                    _recentQueries.RemoveAt(0);
                }

                // Update aggregated stats
                var queryPattern = ExtractQueryPattern(query);
                if (!_queryStats.TryGetValue(queryPattern, out var stats))
                {
                    stats = new QueryStats { QueryPattern = queryPattern };
                    _queryStats[queryPattern] = stats;
                }

                stats.Count++;
                stats.TotalExecutionTime += executionTimeMs;
                stats.MaxExecutionTime = Math.Max(stats.MaxExecutionTime, executionTimeMs);
                stats.MinExecutionTime = stats.MinExecutionTime == 0 ? executionTimeMs : Math.Min(stats.MinExecutionTime, executionTimeMs);
                stats.LastExecuted = DateTime.UtcNow;
            }
        }

        public static PerformanceReport GetPerformanceReport()
        {
            lock (_lock)
            {
                var totalQueries = _queryStats.Values.Sum(s => s.Count);
                var avgExecutionTime = totalQueries > 0 
                    ? _queryStats.Values.Sum(s => s.TotalExecutionTime) / totalQueries 
                    : 0;

                return new PerformanceReport
                {
                    TotalQueries = totalQueries,
                    AverageExecutionTime = avgExecutionTime,
                    SlowQueriesCount = _queryStats.Values.Sum(s => s.Count * (s.MaxExecutionTime > 1000 ? 1 : 0)),
                    RecentQueries = _recentQueries.TakeLast(20).ToList(),
                    TopSlowQueries = _queryStats.Values
                        .Where(s => s.MaxExecutionTime > 100)
                        .OrderByDescending(s => s.MaxExecutionTime)
                        .Take(10)
                        .ToList(),
                    GeneratedAt = DateTime.UtcNow
                };
            }
        }

        private static string TruncateQueryForStats(string query)
        {
            const int maxLength = 100;
            return query.Length <= maxLength ? query : query.Substring(0, maxLength) + "...";
        }

        private static string ExtractQueryPattern(string query)
        {
            // Simple pattern extraction - replace parameter values with placeholders
            var pattern = query.ToUpperInvariant();
            
            // Remove common parameter patterns
            pattern = System.Text.RegularExpressions.Regex.Replace(pattern, @"@\w+", "@PARAM");
            pattern = System.Text.RegularExpressions.Regex.Replace(pattern, @"'\d+'", "'NUMBER'");
            pattern = System.Text.RegularExpressions.Regex.Replace(pattern, @"'[^']*'", "'STRING'");
            pattern = System.Text.RegularExpressions.Regex.Replace(pattern, @"\b\d+\b", "NUMBER");
            
            return pattern.Length > 100 ? pattern.Substring(0, 100) + "..." : pattern;
        }
    }

    public class QueryMetric
    {
        public double ExecutionTime { get; set; }
        public string Query { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class QueryStats
    {
        public string QueryPattern { get; set; } = string.Empty;
        public int Count { get; set; }
        public double TotalExecutionTime { get; set; }
        public double MaxExecutionTime { get; set; }
        public double MinExecutionTime { get; set; }
        public DateTime LastExecuted { get; set; }
        
        public double AverageExecutionTime => Count > 0 ? TotalExecutionTime / Count : 0;
    }

    public class PerformanceReport
    {
        public int TotalQueries { get; set; }
        public double AverageExecutionTime { get; set; }
        public int SlowQueriesCount { get; set; }
        public List<QueryMetric> RecentQueries { get; set; } = new();
        public List<QueryStats> TopSlowQueries { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }
}