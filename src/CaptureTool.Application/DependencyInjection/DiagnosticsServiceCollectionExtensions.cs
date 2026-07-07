using CaptureTool.Application.Abstractions.Diagnostics.ClearLogs;
using CaptureTool.Application.Abstractions.Diagnostics.ExportLogs;
using CaptureTool.Application.Abstractions.Diagnostics.GetCurrentLogs;
using CaptureTool.Application.Abstractions.Diagnostics.GetIsLoggingEnabled;
using CaptureTool.Application.Abstractions.Diagnostics.UpdateLoggingState;
using CaptureTool.Application.Diagnostics.ClearLogs;
using CaptureTool.Application.Diagnostics.ExportLogs;
using CaptureTool.Application.Diagnostics.GetCurrentLogs;
using CaptureTool.Application.Diagnostics.GetIsLoggingEnabled;
using CaptureTool.Application.Diagnostics.UpdateLoggingState;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class DiagnosticsServiceCollectionExtensions
{
    public static IServiceCollection AddDiagnosticsUseCases(this IServiceCollection services)
    {
        services.AddTransient<IClearLogsUseCase, ClearLogsUseCase>();
        services.AddTransient<IExportLogsUseCase, ExportLogsUseCase>();
        services.AddTransient<IGetCurrentLogsUseCase, GetCurrentLogsUseCase>();
        services.AddTransient<IGetIsLoggingEnabledUseCase, GetIsLoggingEnabledUseCase>();
        services.AddTransient<IUpdateLoggingStateUseCase, UpdateLoggingStateUseCase>();

        return services;
    }
}
