using System.Security.Cryptography;
using System.Text.Json;

namespace CaptureTool.Analysis.Evaluation;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await RunAsync(args, DateTimeOffset.UtcNow).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is
            ArgumentException or InvalidDataException or IOException or JsonException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count == 0)
        {
            PrintUsage();
            return 1;
        }

        return args[0] switch
        {
            "evaluate" => await EvaluateAsync(args, nowUtc, cancellationToken).ConfigureAwait(false),
            "prune" => await PruneAsync(args, nowUtc, cancellationToken).ConfigureAwait(false),
            _ => PrintUnknownCommand(args[0]),
        };
    }

    private static async Task<int> EvaluateAsync(
        IReadOnlyList<string> args,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        string corpusPath = GetRequiredOption(args, "--corpus");
        string runPath = GetRequiredOption(args, "--run");
        string outputPath = GetRequiredOption(args, "--output");
        int retentionDays = GetOptionalPositiveInt(args, "--retention-days", 30, maximum: 365);

        byte[] corpusBytes = await File.ReadAllBytesAsync(corpusPath, cancellationToken)
            .ConfigureAwait(false);
        byte[] runBytes = await File.ReadAllBytesAsync(runPath, cancellationToken)
            .ConfigureAwait(false);
        EvaluationCorpus corpus = JsonSerializer.Deserialize(
            corpusBytes,
            EvaluationJsonContext.Default.EvaluationCorpus) ??
            throw new InvalidDataException("The corpus document is empty.");
        ProviderEvaluationRun run = JsonSerializer.Deserialize(
            runBytes,
            EvaluationJsonContext.Default.ProviderEvaluationRun) ??
            throw new InvalidDataException("The provider run document is empty.");

        EvaluationReport report = EvaluationEngine.Evaluate(
            corpus,
            run,
            Convert.ToHexString(SHA256.HashData(corpusBytes)),
            Convert.ToHexString(SHA256.HashData(runBytes)),
            nowUtc,
            nowUtc.AddDays(retentionDays));
        var store = new EvaluationRunStore(outputPath);
        _ = await store.PruneExpiredAsync(nowUtc, cancellationToken).ConfigureAwait(false);
        string reportPath = await store.WriteAsync(report, cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"Evaluation report: {reportPath}");
        foreach (EvaluationGate gate in report.Gates)
        {
            Console.WriteLine($"{(gate.Passed ? "PASS" : "FAIL")} {gate.GateId}: {gate.Observed:G6} ({gate.Requirement})");
        }

        return report.Passed ? 0 : 2;
    }

    private static async Task<int> PruneAsync(
        IReadOnlyList<string> args,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        string outputPath = GetRequiredOption(args, "--output");
        var store = new EvaluationRunStore(outputPath);
        int removed = await store.PruneExpiredAsync(nowUtc, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Removed {removed} expired evaluation run(s).");
        return 0;
    }

    private static string GetRequiredOption(IReadOnlyList<string> args, string option)
    {
        for (int index = 1; index < args.Count - 1; index++)
        {
            if (args[index] == option)
            {
                string value = args[index + 1];
                if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("--", StringComparison.Ordinal))
                {
                    return value;
                }
            }
        }

        throw new ArgumentException($"Required option '{option}' was not supplied.");
    }

    private static int GetOptionalPositiveInt(
        IReadOnlyList<string> args,
        string option,
        int defaultValue,
        int maximum)
    {
        for (int index = 1; index < args.Count; index++)
        {
            if (args[index] != option)
            {
                continue;
            }

            if (index == args.Count - 1 ||
                !int.TryParse(args[index + 1], out int value) ||
                value <= 0 ||
                value > maximum)
            {
                throw new ArgumentException($"Option '{option}' must be between 1 and {maximum}.");
            }

            return value;
        }

        return defaultValue;
    }

    private static int PrintUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine(
            "Usage: evaluate --corpus <file> --run <file> --output <directory> [--retention-days 30]");
        Console.Error.WriteLine("       prune --output <directory>");
    }
}
