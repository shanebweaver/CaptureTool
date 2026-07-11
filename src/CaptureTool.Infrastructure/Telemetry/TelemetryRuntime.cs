using Azure.Monitor.OpenTelemetry.Exporter;
using CaptureTool.Application.Abstractions.Telemetry;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;

namespace CaptureTool.Infrastructure.Telemetry;

public sealed class TelemetryRuntime : IDisposable
{
    public const string ActivitySourceName = "CaptureTool";
    public const string MeterName = "CaptureTool";
    public const string TraceSamplingRatioEnvironmentVariable = "CAPTURETOOL_TELEMETRY_TRACE_SAMPLING_RATIO";

    private const double DefaultTraceSamplingRatio = 1.0;
    private const int FlushTimeoutMilliseconds = 5000;

    private readonly ConcurrentDictionary<string, Histogram<double>> _histograms = new(StringComparer.Ordinal);
    private readonly ITelemetryProviderLifecycle _tracerProvider;
    private readonly ITelemetryProviderLifecycle _meterProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private bool _disposed;

    public TelemetryRuntime()
    {
        ActivitySource = new ActivitySource(ActivitySourceName);
        Meter = new Meter(MeterName);

        string? connectionString = GetConnectionString();
        HasAzureMonitorExporter = !string.IsNullOrWhiteSpace(connectionString);
        TraceSamplingRatio = GetTraceSamplingRatio();
        ResourceBuilder resourceBuilder = CreateResourceBuilder();

        TracerProviderBuilder tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(TraceSamplingRatio)))
            .AddSource(ActivitySourceName);

        MeterProviderBuilder meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(MeterName);

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(logging =>
            {
                logging.IncludeScopes = true;
                logging.SetResourceBuilder(resourceBuilder);
                if (HasAzureMonitorExporter)
                {
                    logging.AddAzureMonitorLogExporter(options => ConfigureAzureMonitor(options, connectionString!));
                }
            });
        });

        if (HasAzureMonitorExporter)
        {
            tracerProviderBuilder.AddAzureMonitorTraceExporter(options => ConfigureAzureMonitor(options, connectionString!));
            meterProviderBuilder.AddAzureMonitorMetricExporter(options => ConfigureAzureMonitor(options, connectionString!));
        }

        _tracerProvider = new TracerProviderLifecycle(tracerProviderBuilder.Build());
        _meterProvider = new MeterProviderLifecycle(meterProviderBuilder.Build());
        _logger = _loggerFactory.CreateLogger("CaptureTool.Telemetry");
    }

    internal TelemetryRuntime(
        ActivitySource activitySource,
        Meter meter,
        ITelemetryProviderLifecycle tracerProvider,
        ITelemetryProviderLifecycle meterProvider,
        ILoggerFactory loggerFactory,
        ILogger logger,
        bool hasAzureMonitorExporter,
        double traceSamplingRatio)
    {
        ActivitySource = activitySource;
        Meter = meter;
        _tracerProvider = tracerProvider;
        _meterProvider = meterProvider;
        _loggerFactory = loggerFactory;
        _logger = logger;
        HasAzureMonitorExporter = hasAzureMonitorExporter;
        TraceSamplingRatio = traceSamplingRatio;
    }

    public ActivitySource ActivitySource { get; }
    public Meter Meter { get; }
    public bool HasAzureMonitorExporter { get; }
    public double TraceSamplingRatio { get; }

    public Activity? StartActivity(string name, IReadOnlyDictionary<string, object?> attributes)
    {
        Activity? activity = ActivitySource.StartActivity(name, ActivityKind.Internal);
        if (activity is null)
        {
            return null;
        }

        foreach ((string key, object? value) in attributes)
        {
            activity.SetTag(key, value);
        }

        return activity;
    }

    public void TrackEvent(string eventName, IReadOnlyDictionary<string, object?> attributes)
    {
        List<KeyValuePair<string, object?>> state = CreateLogState(eventName, attributes);
        _logger.Log(
            LogLevel.Information,
            new EventId(0, eventName),
            state,
            exception: null,
            static (state, _) => $"Telemetry event: {GetEventName(state)}");
    }

    public void TrackException(Exception exception, IReadOnlyDictionary<string, object?> attributes)
    {
        Dictionary<string, object?> exceptionAttributes = new(attributes, StringComparer.Ordinal);
        exceptionAttributes.TryAdd(TelemetryAttributes.ExceptionType, exception.GetType().Name);
        List<KeyValuePair<string, object?>> state = CreateLogState(TelemetryEvents.ExceptionCaptured, exceptionAttributes);
        Exception safeException = CreateSanitizedExceptionForExport(exception);
        _logger.Log(
            LogLevel.Error,
            new EventId(0, TelemetryEvents.ExceptionCaptured),
            state,
            safeException,
            static (state, ex) => $"Telemetry exception: {GetStateValue(state, TelemetryAttributes.ExceptionType) ?? ex?.GetType().Name ?? GetEventName(state)}");
    }

    internal static Exception CreateSanitizedExceptionForExport(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new SanitizedTelemetryException();
    }

    public void TrackMetric(string metricName, double value, IReadOnlyDictionary<string, object?> attributes)
    {
        Histogram<double> histogram = _histograms.GetOrAdd(metricName, name => Meter.CreateHistogram<double>(name));
        histogram.Record(value, [.. attributes.Select(attribute => new KeyValuePair<string, object?>(attribute.Key, attribute.Value))]);
    }

    private static List<KeyValuePair<string, object?>> CreateLogState(
        string eventName,
        IReadOnlyDictionary<string, object?> attributes)
    {
        List<KeyValuePair<string, object?>> state =
        [
            new(TelemetryAttributes.EventName, eventName)
        ];

        state.AddRange(attributes.Select(attribute => new KeyValuePair<string, object?>(attribute.Key, attribute.Value)));
        return state;
    }

    private static string GetEventName(IReadOnlyList<KeyValuePair<string, object?>> state)
    {
        return GetStateValue(state, TelemetryAttributes.EventName) ?? "unknown";
    }

    private static string? GetStateValue(IReadOnlyList<KeyValuePair<string, object?>> state, string key)
    {
        return state.FirstOrDefault(entry => entry.Key == key).Value?.ToString();
    }

    private static ResourceBuilder CreateResourceBuilder()
    {
        return ResourceBuilder.CreateDefault().AddAttributes(
            new Dictionary<string, object>
            {
                ["service.name"] = "CaptureTool",
                ["service.namespace"] = "CaptureTool.Desktop",
                ["service.version"] = typeof(TelemetryRuntime).Assembly.GetName().Version?.ToString() ?? "unknown"
            });
    }

    private static string? GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("CAPTURETOOL_APPLICATIONINSIGHTS_CONNECTION_STRING") ??
            Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
    }

    private static double GetTraceSamplingRatio()
    {
        string? configuredRatio = Environment.GetEnvironmentVariable(TraceSamplingRatioEnvironmentVariable);
        return double.TryParse(configuredRatio, NumberStyles.Float, CultureInfo.InvariantCulture, out double ratio) &&
            ratio is >= 0.0 and <= 1.0
            ? ratio
            : DefaultTraceSamplingRatio;
    }

    private static void ConfigureAzureMonitor(AzureMonitorExporterOptions options, string connectionString)
    {
        options.ConnectionString = connectionString;
        options.DisableOfflineStorage = true;
    }

    private sealed class SanitizedTelemetryException : Exception
    {
        public SanitizedTelemetryException()
            : base("Exception message omitted by CaptureTool telemetry privacy policy.")
        {
        }
    }

    private sealed class TracerProviderLifecycle(TracerProvider provider) : ITelemetryProviderLifecycle
    {
        public bool ForceFlush(int timeoutMilliseconds)
        {
            return provider.ForceFlush(timeoutMilliseconds);
        }

        public void Dispose()
        {
            provider.Dispose();
        }
    }

    private sealed class MeterProviderLifecycle(MeterProvider provider) : ITelemetryProviderLifecycle
    {
        public bool ForceFlush(int timeoutMilliseconds)
        {
            return provider.ForceFlush(timeoutMilliseconds);
        }

        public void Dispose()
        {
            provider.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _tracerProvider.ForceFlush(FlushTimeoutMilliseconds);
        _meterProvider.ForceFlush(FlushTimeoutMilliseconds);
        _tracerProvider.Dispose();
        _meterProvider.Dispose();
        _loggerFactory.Dispose();
        ActivitySource.Dispose();
        Meter.Dispose();
        _disposed = true;
    }
}

internal interface ITelemetryProviderLifecycle : IDisposable
{
    bool ForceFlush(int timeoutMilliseconds);
}
