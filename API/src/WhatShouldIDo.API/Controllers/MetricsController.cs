using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;

namespace WhatShouldIDo.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MetricsController : ControllerBase
    {
        [HttpGet]
        public ActionResult GetMetrics()
        {
            // This is a simplified implementation for demonstration
            // In production, you would use a proper Prometheus exporter library
            // like prometheus-net.AspNetCore or OpenTelemetry
            
            var metrics = new StringBuilder();
            
            // Add some basic metrics in Prometheus format (use \n only for Unix line endings)
            metrics.Append("# HELP whatshouldido_info Application information\n");
            metrics.Append("# TYPE whatshouldido_info gauge\n");
            metrics.Append("whatshouldido_info{version=\"2.0.0\",environment=\"development\"} 1\n");
            
            metrics.Append("# HELP whatshouldido_uptime_seconds Total uptime in seconds\n");
            metrics.Append("# TYPE whatshouldido_uptime_seconds counter\n");
            var uptime = DateTimeOffset.UtcNow.Subtract(Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
            metrics.Append($"whatshouldido_uptime_seconds {uptime:F0}\n");

            return Content(metrics.ToString(), "text/plain; version=0.0.4; charset=utf-8");
        }
    }
}