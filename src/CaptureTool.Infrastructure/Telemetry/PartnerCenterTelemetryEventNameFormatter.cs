using CaptureTool.Application.Abstractions.Telemetry;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CaptureTool.Infrastructure.Telemetry;

public static class PartnerCenterTelemetryEventNameFormatter
{
    private const string SchemaPrefix = "ct1";
    private const int MaximumEventNameLength = 96;

    private static readonly string[] CaptureDimensions =
    [
        TelemetryProperties.MediaType,
        TelemetryProperties.CaptureType,
        TelemetryProperties.DesktopAudioEnabled,
        TelemetryProperties.AudioInputEnabled
    ];

    private static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedDimensionValues =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            [TelemetryProperties.FailureStage] = new(StringComparer.Ordinal)
            {
                TelemetryFailureStages.FirstFrame,
                TelemetryFailureStages.RecorderStart
            },
            [TelemetryProperties.FailureReason] = new(StringComparer.Ordinal)
            {
                TelemetryFailureReasons.AccessDenied,
                TelemetryFailureReasons.ComponentUnavailable,
                TelemetryFailureReasons.ConfigurationUnsupported,
                TelemetryFailureReasons.GraphicsUnsupported,
                TelemetryFailureReasons.InitializationFailed,
                TelemetryFailureReasons.InvalidConfiguration,
                TelemetryFailureReasons.OutputUnavailable,
                TelemetryFailureReasons.PlatformUnsupported,
                TelemetryFailureReasons.ResourceExhausted,
                TelemetryFailureReasons.StartTimeout,
                TelemetryFailureReasons.TargetUnavailable
            }
        };

    private static readonly IReadOnlyDictionary<string, string[]> DimensionsByEvent =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [TelemetryEvents.AppActivated] =
                [TelemetryProperties.ActivationKind, TelemetryProperties.Source],
            [TelemetryEvents.AppShutdownRequested] =
                [TelemetryProperties.Source],
            [TelemetryEvents.CaptureRequested] =
                [.. CaptureDimensions],
            [TelemetryEvents.CaptureStarted] =
                [.. CaptureDimensions],
            [TelemetryEvents.CaptureCompleted] =
                [.. CaptureDimensions, TelemetryProperties.Outcome],
            [TelemetryEvents.CaptureCanceled] =
                [.. CaptureDimensions, TelemetryProperties.Outcome],
            [TelemetryEvents.CaptureFailed] =
                [
                    .. CaptureDimensions,
                    TelemetryProperties.Outcome,
                    TelemetryProperties.FailureStage,
                    TelemetryProperties.FailureReason
                ],
            [TelemetryEvents.DiagnosticsAction] =
                [TelemetryProperties.Action, TelemetryProperties.Outcome],
            [TelemetryEvents.EditorOpened] =
                [TelemetryProperties.MediaType],
            [TelemetryEvents.EditToolInvoked] =
                [TelemetryProperties.MediaType, TelemetryProperties.Tool, TelemetryProperties.Outcome],
            [TelemetryEvents.FeedbackOpened] =
                [TelemetryProperties.Outcome],
            [TelemetryEvents.NavigationCompleted] =
                [TelemetryProperties.ToRoute],
            [TelemetryEvents.OutputCompleted] =
                [TelemetryProperties.MediaType, TelemetryProperties.Operation, TelemetryProperties.Outcome],
            [TelemetryEvents.SettingsChanged] =
                [TelemetryProperties.Setting, TelemetryProperties.Value],
            [TelemetryEvents.StoreOpened] =
                [TelemetryProperties.Operation, TelemetryProperties.Outcome],
            [TelemetryEvents.StorePurchaseStarted] =
                [TelemetryProperties.Product],
            [TelemetryEvents.StorePurchaseCompleted] =
                [TelemetryProperties.Product, TelemetryProperties.Status, TelemetryProperties.Outcome],
            [TelemetryEvents.UiCommandInvoked] =
                [TelemetryProperties.Surface, TelemetryProperties.Action],
            [TelemetryEvents.UiCommandCompleted] =
                [TelemetryProperties.Surface, TelemetryProperties.Action, TelemetryProperties.Outcome],
            [TelemetryEvents.UseCaseCompleted] =
                [TelemetryProperties.Action, TelemetryProperties.Outcome],
            [TelemetryEvents.UserAction] =
                [TelemetryProperties.Action, TelemetryProperties.Outcome]
        };

    public static string Format(
        string eventName,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        StringBuilder builder = new();
        AppendSegment(builder, SchemaPrefix);
        AppendSegment(builder, eventName);

        if (properties is not null &&
            DimensionsByEvent.TryGetValue(eventName, out string[]? dimensions))
        {
            foreach (string dimension in dimensions)
            {
                if (properties.TryGetValue(dimension, out object? value) && value is not null)
                {
                    string formattedValue = FormatValue(value);
                    if (IsAllowedDimensionValue(dimension, formattedValue))
                    {
                        AppendSegment(builder, formattedValue);
                    }
                }
            }
        }

        string formattedName = builder.ToString();
        if (formattedName.Length <= MaximumEventNameLength)
        {
            return formattedName;
        }

        string hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(formattedName)))
            .ToLowerInvariant()[..8];
        int prefixLength = MaximumEventNameLength - hash.Length - 1;
        return $"{formattedName[..prefixLength].TrimEnd('_')}_{hash}";
    }

    private static bool IsAllowedDimensionValue(string dimension, string value)
    {
        return !AllowedDimensionValues.TryGetValue(dimension, out HashSet<string>? allowedValues) ||
            allowedValues.Contains(value);
    }

    private static void AppendSegment(StringBuilder builder, string value)
    {
        string normalizedValue = Normalize(value);
        if (normalizedValue.Length == 0)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('_');
        }

        builder.Append(normalizedValue);
    }

    private static string FormatValue(object value)
    {
        return value switch
        {
            bool boolValue => boolValue ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string Normalize(string value)
    {
        StringBuilder builder = new(value.Length);
        bool previousWasSeparator = true;
        char previousInput = '\0';

        foreach (char character in value)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                if (char.IsAsciiLetterUpper(character) &&
                    builder.Length > 0 &&
                    !previousWasSeparator &&
                    (char.IsAsciiLetterLower(previousInput) || char.IsAsciiDigit(previousInput)))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('_');
                previousWasSeparator = true;
            }

            previousInput = character;
        }

        return builder.ToString().Trim('_');
    }
}
