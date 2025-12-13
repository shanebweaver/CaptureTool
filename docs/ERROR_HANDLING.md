# Error Handling Best Practices

## Overview

This document outlines error handling patterns and best practices for the CaptureTool application. Consistent error handling improves reliability, maintainability, and user experience.

## Current Patterns

### 1. Action Pattern Error Handling

Actions that can fail should let exceptions bubble up to be handled by the caller (typically ViewModels or navigation handlers).

```csharp
public override void Execute()
{
    if (!Directory.Exists(_folderPath))
    {
        throw new DirectoryNotFoundException($"The folder path '{_folderPath}' does not exist.");
    }
    
    Process.Start("explorer.exe", $"/open, {_folderPath}");
}
```

**Benefits:**
- Clear error contracts
- Caller decides how to handle errors
- Easy to test error scenarios

### 2. ViewModel Error Handling

ViewModels wrap action execution with telemetry and error handling:

```csharp
private void OpenScreenshotsFolder()
{
    TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.OpenScreenshotsFolder, () =>
    {
        var action = _openScreenshotsFolderActionFactory.Create(ScreenshotsFolderPath);
        action.Execute(); // May throw exceptions
    });
}
```

The `TelemetryHelper` wraps exceptions for tracking but allows them to propagate.

### 3. Page/View Error Handling

`PageBase<VM>` catches exceptions during page load and routes to error page:

```csharp
catch (OperationCanceledException ex)
{
    _logService.LogException(ex, "Page load canceled.");
}
catch (Exception ex)
{
    _logService.LogException(ex, "Failed to load page.");
    _appNavigation.GoToError(ex);
}
```

**Benefits:**
- Global error handling for navigation failures
- User sees meaningful error page
- Errors are logged for diagnostics

### 4. Service Error Handling

Services that perform I/O or fallible operations typically:
- Return `Task<bool>` for success/failure
- Catch and log exceptions internally
- Return false to indicate failure

```csharp
public async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
{
    try
    {
        await _jsonStorageService.WriteAsync(GetSettingsFile(), settingsList, context);
        return true;
    }
    catch (Exception e)
    {
        _logService.LogException(e, "Unable to perform save operation.");
        return false;
    }
}
```

### 5. Cleanup Error Handling

When performing cleanup operations (like clearing temp files), swallow individual errors but allow the operation to continue:

```csharp
foreach (var entry in Directory.EnumerateFileSystemEntries(tempFolderPath))
{
    try
    {
        if (Directory.Exists(entry))
        {
            Directory.Delete(entry, recursive: true);
        }
        else
        {
            File.Delete(entry);
        }
    }
    catch
    {
        // Ignore errors - some files might be in use
    }
}
```

## Best Practices

### DO:

1. **Let exceptions bubble up** in Actions and low-level operations unless you can handle them meaningfully
2. **Use specific exception types** (e.g., `DirectoryNotFoundException` instead of `Exception`)
3. **Log exceptions** at the boundary where they're caught
4. **Provide context** in exception messages (include relevant data like paths, IDs)
5. **Use CancellationToken** for async operations to support cancellation
6. **Validate input** early in Actions and Services
7. **Use try-finally** or `using` statements for resource cleanup
8. **Handle OperationCanceledException** separately from other exceptions

### DON'T:

1. **Don't swallow exceptions** without logging (except in specific cleanup scenarios)
2. **Don't catch `Exception`** unless you're at a boundary (UI, service entry point)
3. **Don't throw exceptions** for control flow in normal scenarios
4. **Don't re-throw** exceptions without adding value (`throw ex` loses stack trace)
5. **Don't log and rethrow** (creates duplicate log entries)
6. **Don't use exceptions** for expected failures (consider Result pattern)

## Recommended Improvements

### 1. Result Pattern for Expected Failures

For operations with expected failure modes (like validation), consider a Result type:

```csharp
public record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    
    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(string error) => new() { IsSuccess = false, Error = error };
}

// Usage
public Result<IFolder> ValidateFolder(string path)
{
    if (string.IsNullOrWhiteSpace(path))
        return Result<IFolder>.Failure("Folder path cannot be empty");
        
    if (!Directory.Exists(path))
        return Result<IFolder>.Failure($"Folder '{path}' does not exist");
        
    return Result<IFolder>.Success(new Folder(path));
}
```

**Benefits:**
- Makes failure modes explicit
- No exception overhead for expected failures
- Forces caller to handle failures
- Better for flow control than exceptions

### 2. Validation Layer

Add a validation layer for Actions that take parameters:

```csharp
public abstract class ActionCommand<T> : ActionCommandBase, IActionCommand<T>
{
    protected virtual ValidationResult Validate(T parameter)
    {
        return ValidationResult.Success;
    }
    
    public override void Execute(object? parameter)
    {
        if (parameter is not T typedParameter)
        {
            throw new ArgumentException("Unexpected parameter type.");
        }
        
        var validation = Validate(typedParameter);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Error);
        }
        
        Execute(typedParameter);
    }
    
    public abstract void Execute(T parameter);
}
```

### 3. Async Exception Handling

For async operations, ensure exceptions are properly propagated:

```csharp
// GOOD: Exception propagates naturally
public async Task ExecuteAsync(CancellationToken ct)
{
    await _service.PerformOperationAsync(ct);
}

// BAD: Exception is lost
public async Task ExecuteAsync(CancellationToken ct)
{
    _service.PerformOperationAsync(ct); // Fire and forget - exceptions disappear
}
```

### 4. Telemetry Integration

All user actions should be wrapped with telemetry:

```csharp
private void OnUserAction()
{
    TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.UserAction, () =>
    {
        // Action logic
        // Exceptions are tracked and re-thrown
    });
}
```

This pattern:
- Tracks action duration
- Logs exceptions with context
- Preserves stack traces

## Exception Types

### Application-Specific Exceptions

Consider creating domain-specific exceptions for clarity:

```csharp
public class CaptureFailedException : Exception
{
    public CaptureMode Mode { get; }
    
    public CaptureFailedException(CaptureMode mode, string message) 
        : base(message)
    {
        Mode = mode;
    }
}

public class SettingsCorruptedException : Exception
{
    public string SettingsPath { get; }
    
    public SettingsCorruptedException(string path, string message, Exception? inner = null)
        : base(message, inner)
    {
        SettingsPath = path;
    }
}
```

### Standard Exception Usage

- **ArgumentException**: Invalid method arguments
- **ArgumentNullException**: Null argument when not allowed
- **InvalidOperationException**: Operation not valid in current state
- **NotSupportedException**: Unsupported operation
- **FileNotFoundException**: File not found
- **DirectoryNotFoundException**: Directory not found
- **UnauthorizedAccessException**: Permission denied
- **OperationCanceledException**: Operation was cancelled

## Error Recovery Strategies

### 1. Retry with Exponential Backoff

For transient failures (network, file locks):

```csharp
public async Task<T> ExecuteWithRetryAsync<T>(
    Func<Task<T>> operation,
    int maxAttempts = 3,
    int delayMs = 1000)
{
    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (IsTransientError(ex) && attempt < maxAttempts)
        {
            await Task.Delay(delayMs * attempt);
        }
    }
    
    throw new InvalidOperationException("Max retry attempts exceeded");
}

private bool IsTransientError(Exception ex)
{
    return ex is IOException or TimeoutException;
}
```

### 2. Fallback Values

For settings and configuration:

```csharp
public T Get<T>(ISettingDefinitionWithValue<T> settingDefinition)
{
    try
    {
        if (_settings.TryGetValue(settingDefinition.Key, out var storedSetting))
        {
            return ((SettingDefinition<T>)storedSetting).Value;
        }
    }
    catch (Exception ex)
    {
        _logService.LogException(ex, $"Failed to read setting {settingDefinition.Key}");
    }
    
    // Fallback to default
    return settingDefinition.Value;
}
```

### 3. Graceful Degradation

For non-critical features:

```csharp
public void InitializeOptionalFeature()
{
    try
    {
        // Try to enable advanced feature
        EnableAdvancedCapture();
    }
    catch (Exception ex)
    {
        _logService.LogWarning($"Advanced capture unavailable: {ex.Message}");
        // Continue with basic functionality
    }
}
```

## Testing Error Scenarios

### Unit Tests for Error Handling

```csharp
[TestMethod]
public void Execute_WhenFolderDoesNotExist_ThrowsDirectoryNotFoundException()
{
    // Arrange
    var action = new SettingsOpenTempFolderAction("/nonexistent/path");
    
    // Act & Assert
    Assert.ThrowsException<DirectoryNotFoundException>(() => action.Execute());
}

[TestMethod]
public async Task TrySaveAsync_WhenIOException_ReturnsFalse()
{
    // Arrange
    var mockStorage = new Mock<IJsonStorageService>();
    mockStorage
        .Setup(x => x.WriteAsync(It.IsAny<IFile>(), It.IsAny<object>(), It.IsAny<object>()))
        .ThrowsAsync(new IOException("Disk full"));
    
    var service = new LocalSettingsService(Mock.Of<ILogService>(), mockStorage.Object);
    await service.InitializeAsync("test.json", CancellationToken.None);
    
    // Act
    bool result = await service.TrySaveAsync(CancellationToken.None);
    
    // Assert
    Assert.IsFalse(result);
}
```

## Logging Guidelines

### Log Levels

- **Exception**: Unexpected errors requiring attention
- **Warning**: Expected errors or degraded functionality
- **Info**: Significant application events
- **Debug**: Detailed diagnostic information

### Exception Logging

Always include context:

```csharp
_logService.LogException(
    ex,
    $"Failed to save settings to {filePath}. User: {userId}, AttemptCount: {attemptCount}"
);
```

### Performance Considerations

- Logging is synchronous - keep messages concise
- Don't log in tight loops
- Use structured logging for complex data
- Avoid logging sensitive data (passwords, tokens)

## Summary

Good error handling:
1. Makes failure modes explicit and understandable
2. Provides enough context for debugging
3. Fails fast when appropriate
4. Degrades gracefully when possible
5. Never loses errors silently
6. Maintains consistent patterns across the codebase

By following these practices, we create a more reliable, maintainable, and user-friendly application.
