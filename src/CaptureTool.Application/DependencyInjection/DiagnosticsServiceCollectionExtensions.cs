using CaptureTool.Application.Abstractions.Features.Diagnostics.ClearLogs;
using CaptureTool.Application.Abstractions.Features.Diagnostics.ExportLogs;
using CaptureTool.Application.Abstractions.Features.Diagnostics.GetCurrentLogs;
using CaptureTool.Application.Abstractions.Features.Diagnostics.GetIsLoggingEnabled;
using CaptureTool.Application.Abstractions.Features.Diagnostics.UpdateLoggingState;
using CaptureTool.Application.Features.Diagnostics.ClearLogs;
using CaptureTool.Application.Features.Diagnostics.ExportLogs;
using CaptureTool.Application.Features.Diagnostics.GetCurrentLogs;
using CaptureTool.Application.Features.Diagnostics.GetIsLoggingEnabled;
using CaptureTool.Application.Features.Diagnostics.UpdateLoggingState;
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
