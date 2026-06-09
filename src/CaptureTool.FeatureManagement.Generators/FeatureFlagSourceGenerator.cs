using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using System.Text.RegularExpressions;

namespace CaptureTool.FeatureManagement.Generators;

[Generator]
public sealed class FeatureFlagSourceGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MissingAppSettings = new(
        "CTFM001",
        "Feature management appsettings file was not found",
        "Could not find appsettings.json as an additional file for feature generation",
        "CaptureTool.FeatureManagement",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidAppSettings = new(
        "CTFM002",
        "Feature management appsettings file could not be parsed",
        "{0}",
        "CaptureTool.FeatureManagement",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly Regex FeatureFlagsArrayRegex = new(
        "\"feature_flags\"\\s*:\\s*\\[(?<flags>.*)\\]",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex FeatureFlagObjectRegex = new(
        "\\{(?<featureFlag>.*?)\\}",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex FeatureIdRegex = new(
        "\"id\"\\s*:\\s*\"(?<id>(?:\\\\.|[^\"])*)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FeatureEnabledRegex = new(
        "\"enabled\"\\s*:\\s*(?<enabled>true|false)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<AdditionalText?> appSettingsProvider = context.AdditionalTextsProvider
            .Where(static file => string.Equals(System.IO.Path.GetFileName(file.Path), "appsettings.json", StringComparison.OrdinalIgnoreCase))
            .Collect()
            .Select(static (files, _) => files.FirstOrDefault());

        context.RegisterSourceOutput(appSettingsProvider, static (sourceProductionContext, appSettings) =>
        {
            if (appSettings is null)
            {
                sourceProductionContext.ReportDiagnostic(Diagnostic.Create(MissingAppSettings, Location.None));
                return;
            }

            SourceText? sourceText = appSettings.GetText(sourceProductionContext.CancellationToken);
            if (sourceText is null)
            {
                sourceProductionContext.ReportDiagnostic(Diagnostic.Create(InvalidAppSettings, Location.None, "Could not read appsettings.json."));
                return;
            }

            FeatureFlagParseResult result = ParseFeatureFlags(sourceText.ToString());
            foreach (string error in result.Errors)
            {
                sourceProductionContext.ReportDiagnostic(Diagnostic.Create(InvalidAppSettings, Location.None, error));
            }

            if (result.Errors.Count > 0)
            {
                return;
            }

            sourceProductionContext.AddSource("AppFeatures.g.cs", SourceText.From(GenerateSource(result.FeatureFlags), Encoding.UTF8));
        });
    }

    private static FeatureFlagParseResult ParseFeatureFlags(string appSettings)
    {
        var errors = new List<string>();
        var featureFlags = new List<FeatureFlagDefinition>();

        Match featureFlagsArrayMatch = FeatureFlagsArrayRegex.Match(appSettings);
        if (!featureFlagsArrayMatch.Success)
        {
            errors.Add("Could not find feature_management.feature_flags in appsettings.json.");
            return new FeatureFlagParseResult(featureFlags, errors);
        }

        string featureFlagsArray = featureFlagsArrayMatch.Groups["flags"].Value;
        foreach (Match featureFlagMatch in FeatureFlagObjectRegex.Matches(featureFlagsArray))
        {
            string featureFlag = featureFlagMatch.Groups["featureFlag"].Value;
            Match idMatch = FeatureIdRegex.Match(featureFlag);
            if (!idMatch.Success)
            {
                errors.Add("Feature flag definitions must include an id.");
                continue;
            }

            string featureId = Regex.Unescape(idMatch.Groups["id"].Value);
            if (string.IsNullOrWhiteSpace(featureId))
            {
                errors.Add("Feature flag ids cannot be blank.");
                continue;
            }

            Match enabledMatch = FeatureEnabledRegex.Match(featureFlag);
            if (!enabledMatch.Success)
            {
                errors.Add($"Feature flag id '{featureId}' must include a boolean enabled value.");
                continue;
            }

            bool isEnabled = bool.Parse(enabledMatch.Groups["enabled"].Value);
            featureFlags.Add(new FeatureFlagDefinition(featureId, isEnabled));
        }

        if (featureFlags.Count == 0)
        {
            errors.Add("No feature flag ids were found in appsettings.json.");
        }

        string[] duplicateIds = featureFlags
            .GroupBy(static featureFlag => featureFlag.Id, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        foreach (string duplicateId in duplicateIds)
        {
            errors.Add($"Feature flag id '{duplicateId}' is defined more than once.");
        }

        string[] duplicateMemberNames = featureFlags
            .GroupBy(static featureFlag => CreateMemberName(featureFlag.Id), StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        foreach (string duplicateMemberName in duplicateMemberNames)
        {
            errors.Add($"Multiple feature flag ids produce the generated member name '{duplicateMemberName}'.");
        }

        return new FeatureFlagParseResult(featureFlags, errors);
    }

    private static string GenerateSource(IReadOnlyList<FeatureFlagDefinition> featureFlags)
    {
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("// <auto-generated />");
        sourceBuilder.AppendLine("#nullable enable");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("namespace CaptureTool.FeatureManagement;");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"CaptureTool.FeatureManagement.Generators\", \"1.0.0\")]");
        sourceBuilder.AppendLine("public static partial class AppFeatures");
        sourceBuilder.AppendLine("{");

        foreach (FeatureFlagDefinition featureFlag in featureFlags.OrderBy(static featureFlag => featureFlag.Id, StringComparer.Ordinal))
        {
            sourceBuilder
                .Append("    public static readonly global::CaptureTool.FeatureManagement.FeatureFlag ")
                .Append(CreateMemberName(featureFlag.Id))
                .Append(" = new(")
                .Append(ToStringLiteral(featureFlag.Id))
                .Append(", ")
                .Append(featureFlag.IsEnabled ? "true" : "false")
                .AppendLine(");");
        }

        sourceBuilder.AppendLine("}");

        return sourceBuilder.ToString();
    }

    private static string CreateMemberName(string featureId)
    {
        var memberNameBuilder = new StringBuilder("Feature_");
        foreach (char character in featureId)
        {
            memberNameBuilder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        return memberNameBuilder.ToString();
    }

    private static string ToStringLiteral(string value)
    {
        return "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n") + "\"";
    }

    private sealed class FeatureFlagDefinition
    {
        public FeatureFlagDefinition(string id, bool isEnabled)
        {
            Id = id;
            IsEnabled = isEnabled;
        }

        public string Id { get; }

        public bool IsEnabled { get; }
    }

    private sealed class FeatureFlagParseResult
    {
        public FeatureFlagParseResult(IReadOnlyList<FeatureFlagDefinition> featureFlags, IReadOnlyList<string> errors)
        {
            FeatureFlags = featureFlags;
            Errors = errors;
        }

        public IReadOnlyList<FeatureFlagDefinition> FeatureFlags { get; }

        public IReadOnlyList<string> Errors { get; }
    }
}
