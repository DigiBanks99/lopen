using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Lopen.Otel;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers OpenTelemetry tracing, metrics, and log export for Lopen.
    /// Respects master toggle (otel:enabled), per-signal toggles, and environment variables.
    /// </summary>
    public static IServiceCollection AddLopenOtel(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (!configuration.GetValue("otel:enabled", defaultValue: true))
            return services;

        var serviceName = configuration["OTEL_SERVICE_NAME"]
            ?? configuration["otel:service_name"]
            ?? "lopen";

        var otel = services.AddOpenTelemetry();

        otel.ConfigureResource(r => r.AddService(serviceName));

        if (configuration.GetValue("otel:traces:enabled", defaultValue: true))
        {
            otel.WithTracing(tracing =>
            {
                foreach (var sourceName in LopenTelemetryDiagnostics.AllSourceNames)
                {
                    tracing.AddSource(sourceName);
                }

                tracing.AddHttpClientInstrumentation();
            });
        }

        if (configuration.GetValue("otel:metrics:enabled", defaultValue: true))
        {
            otel.WithMetrics(metrics => metrics
                .AddMeter(LopenTelemetryDiagnostics.Meter.Name)
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation());
        }

        if (configuration.GetValue("otel:logs:enabled", defaultValue: true))
        {
            services.AddLogging(logging =>
            {
                logging.Configure(options =>
                    options.ActivityTrackingOptions =
                        ActivityTrackingOptions.TraceId |
                        ActivityTrackingOptions.SpanId);
                logging.AddOpenTelemetry(otelLogging =>
                {
                    otelLogging.IncludeFormattedMessage = true;
                    otelLogging.IncludeScopes = true;
                });
            });
        }

        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? configuration["otel:export:endpoint"];

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            otel.UseOtlpExporter();
        }

        return services;
    }
}
