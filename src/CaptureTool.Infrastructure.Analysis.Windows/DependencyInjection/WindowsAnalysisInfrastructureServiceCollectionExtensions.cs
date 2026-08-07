using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Infrastructure.Analysis.Windows.Analyzers;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Analysis.Windows.DependencyInjection;

public static class WindowsAnalysisInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsAnalysisDomains(this IServiceCollection services)
    {
        services.AddSingleton<ICaptureAnalyzer, WindowsImageMediaPropertiesAnalyzer>();
        services.AddSingleton<ICaptureAnalyzer, WindowsOcrDocumentAnalyzer>();
        services.AddSingleton<ICaptureAnalyzer, WindowsImageDescriptionAnalyzer>();
        return services;
    }
}
