namespace CaptureTool.Application.Abstractions.UseCases;

public sealed record UseCaseResponse<T>(UseCaseResult Result, T? Value = default)
{
    public static UseCaseResponse<T> Success(T value) => new(UseCaseResult.Succeeded, value);
    public static UseCaseResponse<T> Failure() => new(UseCaseResult.Failed);
    public static UseCaseResponse<T> Cancelled() => new(UseCaseResult.Cancelled);
}
