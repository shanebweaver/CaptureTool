using System;
using System.Text.Json.Serialization;
using Windows.Foundation.Collections;

namespace CaptureTool.UI.Windows.Activation;

public sealed class DiscoverResponseCapability
{
    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("methods")]
    public string[] Methods { get; set; }

    [JsonPropertyName("parameters")]
    public string[] Parameters { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonConstructor]
    public DiscoverResponseCapability(string path, string[] methods, string[] parameters, string description)
    {
        Path = path;
        Methods = methods;
        Parameters = parameters;
        Description = description;
    }

    public ValueSet ToValueSet()
    {
        return new() {
            { "path", Path },
            { "methods", Methods},
            { "parameters", Parameters},
            { "descrition", Description},
        };
    }
}