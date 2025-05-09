using System.Globalization;

namespace CaptureTool.ViewModels;

public sealed partial class AppLanguageViewModel : ViewModelBase
{
    private string? _language;
    public string? Language
    {
        get => _language;
        set
        {
            Set(ref _language, value);
            UpdateDisplayName();
        }
    }

    private string? _displayName;
    public string? DisplayName
    {
        get => _displayName;
        set => Set(ref _displayName, value);
    }

    private void UpdateDisplayName()
    {
        if (Language != null)
        {
            CultureInfo langInfo = new(Language);
            DisplayName = langInfo.NativeName;
        }
    }
}
