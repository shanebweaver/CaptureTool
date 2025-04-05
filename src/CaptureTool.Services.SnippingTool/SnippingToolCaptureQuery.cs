using System;
using System.Collections.Generic;

namespace CaptureTool.Services.SnippingTool;

public class SnippingToolCaptureQuery : SnippingToolQuery
{
    public override SnippingToolHost Host => SnippingToolHost.Capture;
    public SnippingToolPath Path { get; }
    public SnippingToolDefaultMode? DefaultMode { get; }
    public SnippingToolEnabledMode[] EnabledModes { get; }
    public string? ApiVersion { get; }
    public string? UserAgent { get; }
    public string? RequestCorrelationId { get; }
    public bool? AutoSave { get; }

    public SnippingToolCaptureQuery(
        SnippingToolPath path, 
        SnippingToolDefaultMode? defaultMode,
        SnippingToolEnabledMode[] enabledModes,
        string redirectUri,
        string? apiVersion = null,
        string? userAgent = null,
        string? requestCorrelationId = null,
        bool? autoSave = null)
        : base(redirectUri)
    {
        Path = path;
        DefaultMode = defaultMode;
        EnabledModes = enabledModes;
        ApiVersion = apiVersion;
        UserAgent = userAgent;
        RequestCorrelationId = requestCorrelationId;
        AutoSave = autoSave;
    }

    public override string ToString()
    {
        List<string> queryParts = [];

        // Default mode
        if (DefaultMode != null)
        {
            string? defaultMode = Enum.GetName(typeof(SnippingToolDefaultMode), DefaultMode);
            ArgumentNullException.ThrowIfNull(defaultMode);
            queryParts.Add(defaultMode);
        }

        // API version
        if (ApiVersion != null)
        {
            queryParts.Add($"api-version=\"{ApiVersion}\"");
        }

        // User agent
        if (UserAgent != null)
        {
            queryParts.Add($"user-agent={UserAgent}");
        }

        // Request correlation id
        if (RequestCorrelationId != null)
        {
            queryParts.Add($"x-request-correlation-id={RequestCorrelationId}");
        }

        // Autosave
        if (AutoSave != null)
        {
            queryParts.Add($"auto-save={AutoSave}");
        }

        // Enabled modes
        List<string> enabledModes = [];
        foreach (var mode in EnabledModes)
        {
            string? modeName = Enum.GetName(mode);
            ArgumentNullException.ThrowIfNull(modeName);
            enabledModes.Add(modeName);
        }
        queryParts.Add($"enabledModes={string.Join(',', enabledModes)}");

        // Add the base
        queryParts.Add(base.ToString());

        // Join the parts
        string queryString = string.Join('&', queryParts);

        string? hostName = Enum.GetName(Host);
        ArgumentNullException.ThrowIfNull(hostName);

        string? pathName = Enum.GetName(Path);
        ArgumentNullException.ThrowIfNull(pathName);

        return $"{hostName}/{pathName}?{queryString}";
    }
}
