namespace CaptureTool.Application.Abstractions.UseCases;

public class UseCaseResponse<T>
{
    public UseCaseResult Result { get; init; } = UseCaseResult.Succeeded;
    public T? Value { get; init; } = default;
}
