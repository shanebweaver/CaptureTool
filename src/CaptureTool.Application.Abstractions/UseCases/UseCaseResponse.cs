namespace CaptureTool.Application.Abstractions.UseCases;

public class UseCaseResponse<T>
{
    public UseCaseResult Result { get; init; } = UseCaseResult.Succeeded;
    public T? Value { get; init; } = default;

    // Factory methods
    public static UseCaseResponse<T> Success(T value) => new() { Result = UseCaseResult.Succeeded, Value = value };
    public static UseCaseResponse<T> Failure() => new() { Result = UseCaseResult.Failed};
    public static UseCaseResponse<T> Cancelled() => new() { Result = UseCaseResult.Cancelled };

    // Async helpers
    public static Task<UseCaseResponse<T>> SuccessAsync(T value) => Task.FromResult(Success(value));
    public static Task<UseCaseResponse<T>> FailureAsync() => Task.FromResult(Failure());
    public static Task<UseCaseResponse<T>> CancelledAsync() => Task.FromResult(Cancelled());
}
