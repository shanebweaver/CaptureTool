namespace CaptureTool.Domain.Analysis;

internal static class AnalysisGuard
{
    public static string Identifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 200) throw new ArgumentException("Identifier is too long.", parameterName);
        return value;
    }

    public static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values) where T : class
    {
        ArgumentNullException.ThrowIfNull(values);
        T[] copy = values.ToArray();
        if (copy.Any(value => value == null)) throw new ArgumentException("Null entries are not allowed.", nameof(values));
        return Array.AsReadOnly(copy);
    }
}
