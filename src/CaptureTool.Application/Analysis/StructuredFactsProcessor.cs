using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CaptureTool.Application.Analysis;

/// <summary>Literal extraction only. Values are never resolved, executed, or sent to a model.</summary>
public sealed partial class StructuredFactsProcessor : IMetadataProcessor
{
    private readonly TimeProvider _time;
    private readonly StructuredFactsOptions _options;
    public MetadataProcessorDescriptor Descriptor { get; }

    public StructuredFactsProcessor(StructuredFactsOptions? options = null)
        : this(options, TimeProvider.System) { }

    internal StructuredFactsProcessor(StructuredFactsOptions? options, TimeProvider time)
    {
        _options = options ?? MetadataEnrichmentConfiguration.StructuredFacts;
        Descriptor = MetadataEnrichmentConfiguration.CreateStructuredFacts(_options.Limits);
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    public Task<AnalyzerOutcome> ProcessAsync(MetadataProcessorInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Descriptor.Matches(input.Descriptor)) return Task.FromResult(AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "metadata-contract-mismatch"));
        if (input.Derivation == null) return Task.FromResult(AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "metadata-input-missing"));
        return Task.FromResult(Extract(input, cancellationToken));
    }

    private AnalyzerOutcome Extract(MetadataProcessorInput input, CancellationToken cancellationToken)
    {
        long started = _time.GetTimestamp();
        MetadataProcessingLimits limits = Descriptor.Limits;
        MetadataProcessingLimit limited = MetadataProcessingLimit.None;
        Dictionary<(StructuredFactKind Kind, string Value), List<AnalysisEvidence>> facts = [];
        List<(StructuredFactKind Kind, string Value)> order = [];
        try
        {
            foreach (MetadataTextEntry entry in input.Entries)
            {
                CheckBudget();
                if (!IsValidText(entry.Text)) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.InvalidSource, "metadata-text-invalid");
                for (Match match = CandidateRegex().Match(entry.Text); match.Success; match = match.NextMatch())
                {
                    CheckBudget();
                    (StructuredFactKind Kind, Group Group)? candidate = Select(match);
                    if (candidate == null) continue;
                    (StructuredFactKind kind, Group group) = candidate.Value;
                    string value = group.Value;
                    if (kind == StructuredFactKind.Url) value = TrimUrlPunctuation(value);
                    if (kind == StructuredFactKind.EmailAddress && value.EndsWith('.')) value = value[..^1];
                    if (value.Length > StructuredFact.MaximumValueLength)
                    {
                        limited |= MetadataProcessingLimit.FactValue;
                        continue;
                    }
                    if (!IsValid(kind, value)) continue;
                    var key = (kind, value);
                    if (!facts.TryGetValue(key, out List<AnalysisEvidence>? evidence))
                    {
                        if (facts.Count == _options.MaxFacts)
                        {
                            limited |= MetadataProcessingLimit.Facts;
                            continue; // Still collect later evidence for retained facts.
                        }
                        evidence = [];
                        facts.Add(key, evidence);
                        order.Add(key);
                    }
                    if (evidence.Count == _options.MaxEvidencePerFact)
                    {
                        limited |= MetadataProcessingLimit.Evidence;
                        continue;
                    }
                    evidence.Add(new(entry.ResultId, entry.EntryIndex, group.Index, value.Length));
                }
            }
            CheckBudget();
            var payload = new StructuredFactsMetadata(order.Select(key => new StructuredFact(key.Kind, key.Value, facts[key])),
                input.Coverage.WithOutputLimits(limited));
            cancellationToken.ThrowIfCancellationRequested();
            return AnalyzerOutcome.Success(payload, new(Descriptor.Id, "local-rules", "structured-facts-rules", Descriptor.Version));
        }
        catch (Exception exception) when (exception is RegexMatchTimeoutException or TimeoutException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "metadata-timeout");
        }

        void CheckBudget()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_time.GetElapsedTime(started) >= limits.ExecutionTimeout) throw new TimeoutException();
        }

        bool IsValidText(string text)
        {
            for (int index = 0; index < text.Length; index++)
            {
                if (index % 256 == 0) CheckBudget();
                char value = text[index];
                if (value == '\0') return false;
                if (!char.IsSurrogate(value)) continue;
                if (!char.IsHighSurrogate(value) || index + 1 == text.Length || !char.IsLowSurrogate(text[++index])) return false;
            }
            return true;
        }
    }

    private static (StructuredFactKind, Group)? Select(Match match)
    {
        if (match.Groups["url"] is { Success: true } url) return (StructuredFactKind.Url, url);
        if (match.Groups["email"] is { Success: true } email) return (StructuredFactKind.EmailAddress, email);
        if (match.Groups["date"] is { Success: true } date) return (StructuredFactKind.Date, date);
        if (match.Groups["amount"] is { Success: true } amount) return (StructuredFactKind.CurrencyAmount, amount);
        if (match.Groups["error"] is { Success: true } error) return (StructuredFactKind.ErrorCode, error);
        if (match.Groups["reference"] is { Success: true } reference) return (StructuredFactKind.ReferenceCode, reference);
        return null;
    }

    private static bool IsValid(StructuredFactKind kind, string value) => kind switch
    {
        StructuredFactKind.Url => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && uri.IsWellFormedOriginalString() &&
            uri.HostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6 && uri.UserInfo.Length == 0,
        StructuredFactKind.EmailAddress => IsEmail(value),
        StructuredFactKind.Date => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        StructuredFactKind.CurrencyAmount => true, // Entire token and supported code are constrained by the pattern.
        StructuredFactKind.ErrorCode => IsErrorCode(value),
        StructuredFactKind.ReferenceCode => value.Any(char.IsAsciiLetterUpper) && value.Any(char.IsAsciiDigit) &&
            value.All(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character) || character is '-' or '_'),
        _ => false,
    };

    private static bool IsEmail(string value)
    {
        int at = value.IndexOf('@');
        string local = value[..at];
        if (local.StartsWith('.') || local.EndsWith('.') || local.Contains("..", StringComparison.Ordinal)) return false;
        string[] labels = value[(at + 1)..].Split('.');
        return value.Length <= 254 && labels.Length >= 2 && labels.All(label => label.Length is >= 1 and <= 63 &&
            char.IsAsciiLetterOrDigit(label[0]) && char.IsAsciiLetterOrDigit(label[^1]) &&
            label.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')) &&
            labels[^1].Length >= 2 && labels[^1].All(char.IsAsciiLetter);
    }

    private static bool IsErrorCode(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return value.Length is >= 4 and <= 18 && value[2..].All(char.IsAsciiHexDigit);
        if (value.All(char.IsAsciiDigit)) return value.Length is >= 3 and <= 8;
        if (!char.IsAsciiLetterUpper(value[0]) || !value.All(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character) || character is '_' or '-')) return false;
        return value.Any(char.IsAsciiDigit) || value.StartsWith("ERR_", StringComparison.Ordinal) && value.Length > 4 ||
            value.StartsWith("E_", StringComparison.Ordinal) && value.Length > 2;
    }

    private static string TrimUrlPunctuation(string value)
    {
        int length = value.Length;
        int parentheses = 0, brackets = 0, braces = 0;
        foreach (char character in value)
        {
            if (character == '(') parentheses++;
            else if (character == ')') parentheses--;
            else if (character == '[') brackets++;
            else if (character == ']') brackets--;
            else if (character == '{') braces++;
            else if (character == '}') braces--;
        }
        while (length > 0)
        {
            char last = value[length - 1];
            if (".,;!，。；！".Contains(last)) { length--; continue; }
            if (last == ')' && parentheses < 0) parentheses++;
            else if (last == ']' && brackets < 0) brackets++;
            else if (last == '}' && braces < 0) braces++;
            else break;
            length--;
        }
        return value[..length];
    }

    // Deliberately narrow v1 currency vocabulary; no locale-dependent parsing or currency conversion.
    private const string Currency = "(?:USD|EUR|GBP|JPY|CAD|AUD|CHF|CNY|INR|KRW|NZD|SEK|NOK|DKK|SGD|HKD|BRL|MXN|ZAR)";
    private const string Amount = "-?(?:0|[1-9][0-9]{0,14})(?:\\.[0-9]{1,2})?";
    private const string Pattern =
        "(?<![\\p{L}\\p{N}_@.,/+\\-])(?:" +
        "(?<url>(?i:https?)://[^\\s<>\"'“”‘’]+)|(?:" +
        "(?<email>[A-Za-z0-9!#$%&'*+/=?^_`{|}~.\\-]{1,64}@[A-Za-z0-9.\\-]{1,253})|" +
        "(?<date>[0-9]{4}-[0-9]{2}-[0-9]{2})|" +
        "(?<amount>" + Currency + "[ \\t]+" + Amount + "|" + Amount + "[ \\t]+" + Currency + ")|" +
        "(?i:error(?:[ \\t]+code)?)[ \\t]*[:=][ \\t]*(?<error>[A-Za-z0-9][A-Za-z0-9_\\-]{1,63})|" +
        "(?i:reference(?:[ \\t]+(?:code|id))?|ref|ticket(?:[ \\t]+id)?)[ \\t]*[:=#][ \\t]*(?<reference>[A-Za-z0-9][A-Za-z0-9_\\-]{1,63})" +
        ")(?![\\p{L}\\p{N}_@/+\\-]|[.,][0-9]))";

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant, 100)]
    private static partial Regex CandidateRegex();
}
