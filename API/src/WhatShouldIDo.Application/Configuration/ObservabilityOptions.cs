using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Application.Configuration
{
    /// <summary>
    /// Configuration options for observability (OpenTelemetry, logging, tracing).
    /// </summary>
    public class ObservabilityOptions
    {
        /// <summary>
        /// Gets or sets whether OpenTelemetry is enabled.
        /// Default: true
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the service name for telemetry.
        /// Default: "whatshouldido-api"
        /// </summary>
        [Required]
        [MinLength(1)]
        public string ServiceName { get; set; } = "whatshouldido-api";

        /// <summary>
        /// Gets or sets the service version for telemetry.
        /// Default: "1.0.0"
        /// </summary>
        [Required]
        public string ServiceVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the trace sampling ratio (0.0 to 1.0).
        /// 1.0 = 100% sampling, 0.05 = 5% sampling.
        /// Default: 0.05 (5% for production)
        /// </summary>
        [Range(0.0, 1.0)]
        public double TraceSamplingRatio { get; set; } = 0.05;

        /// <summary>
        /// Gets or sets whether to export metrics to Prometheus.
        /// Default: true
        /// </summary>
        public bool PrometheusEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the Prometheus scrape endpoint path.
        /// Default: "/metrics"
        /// </summary>
        [Required]
        public string PrometheusEndpoint { get; set; } = "/metrics";

        /// <summary>
        /// Gets or sets whether to export traces to OTLP (Tempo/Jaeger).
        /// Default: true
        /// </summary>
        public bool OtlpTracesEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the OTLP exporter endpoint for traces.
        /// Default: "http://tempo:4317"
        /// </summary>
        public string? OtlpTracesEndpoint { get; set; } = "http://tempo:4317";

        /// <summary>
        /// Gets or sets whether to export logs to OTLP (Loki).
        /// Default: true
        /// </summary>
        public bool OtlpLogsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the OTLP exporter endpoint for logs.
        /// Default: "http://loki:4317"
        /// </summary>
        public string? OtlpLogsEndpoint { get; set; } = "http://loki:4317";

        /// <summary>
        /// Gets or sets the log level for structured logging.
        /// Default: "Information"
        /// </summary>
        [Required]
        public string LogLevel { get; set; } = "Information";

        /// <summary>
        /// Gets or sets whether to include sensitive data in logs/traces (dev only).
        /// Default: false (MUST be false in production)
        /// </summary>
        public bool IncludeSensitiveData { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to add detailed exception stack traces to logs.
        /// Default: true
        /// </summary>
        public bool IncludeExceptionStackTrace { get; set; } = true;

        /// <summary>
        /// Gets or sets the correlation ID header name.
        /// Default: "X-Correlation-Id"
        /// </summary>
        [Required]
        public string CorrelationIdHeader { get; set; } = "X-Correlation-Id";
    }
}
