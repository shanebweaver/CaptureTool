namespace CaptureTool.Application.Abstractions.Settings;

public enum SettingsMutationStatus
{
    Unknown,
    Saved,
    PersistenceFailed,
    ServiceUnavailable,
}

public readonly record struct SettingsMutationResult(SettingsMutationStatus Status)
{
    public bool Succeeded => Status == SettingsMutationStatus.Saved;

    public static SettingsMutationResult Saved { get; } = new(SettingsMutationStatus.Saved);
    public static SettingsMutationResult PersistenceFailed { get; } = new(SettingsMutationStatus.PersistenceFailed);
    public static SettingsMutationResult ServiceUnavailable { get; } = new(SettingsMutationStatus.ServiceUnavailable);
}
