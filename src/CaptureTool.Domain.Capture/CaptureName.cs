using System.Globalization;

namespace CaptureTool.Domain.Capture;

/// <summary>A durable display name, independent of analysis results and physical file paths.</summary>
public sealed record CaptureName
{
    public string Text { get; }
    public bool IsAutomatic { get; }
    public CaptureName(string text, bool isAutomatic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        text = text.Trim();
        if (text.Length > 160 || text.Any(character => char.IsControl(character) ||
            char.GetUnicodeCategory(character) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator))
            throw new ArgumentException("Use a single-line capture name of at most 160 characters.", nameof(text));
        for (int index = 0; index < text.Length; index++)
        {
            if (!char.IsSurrogate(text[index])) continue;
            if (!char.IsHighSurrogate(text[index]) || index + 1 == text.Length || !char.IsLowSurrogate(text[++index]))
                throw new ArgumentException("Capture names must contain valid Unicode text.", nameof(text));
        }
        Text = text;
        IsAutomatic = isAutomatic;
    }

    /// <summary>A Windows-safe base name; the save picker owns the extension and destination.</summary>
    public string SuggestedFileName()
    {
        string name = new(Text.Select(character => "<>:\"/\\|?*".Contains(character) ||
            char.GetUnicodeCategory(character) == UnicodeCategory.Format ? '_' : character).ToArray());
        name = name.Trim().TrimEnd('.', ' ');
        if (name.Length == 0) name = "Capture";
        string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
            "123456789¹²³".Contains(stem[3])) name = "_" + name;
        // Leave room for extensions and user-selected paths; do not split a surrogate pair.
        int length = Math.Min(120, name.Length);
        if (char.IsHighSurrogate(name[length - 1])) length--;
        return name[..length].TrimEnd('.', ' ');
    }
}
