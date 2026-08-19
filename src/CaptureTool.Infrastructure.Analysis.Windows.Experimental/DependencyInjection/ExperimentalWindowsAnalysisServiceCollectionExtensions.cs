using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Infrastructure.Analysis.Windows.Analyzers;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Analysis.Windows.Experimental.DependencyInjection;

public static class ExperimentalWindowsAnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddExperimentalWindowsAiAnalysis(
        this IServiceCollection services)
    {
        services.AddSingleton<IWindowsAiSpeechRecognitionService,
            WindowsAiSpeechRecognitionService>();
        services.AddSingleton<ICaptureAnalyzer, WindowsAiSpeechTranscriptAnalyzer>();
        return services;
    }
}
