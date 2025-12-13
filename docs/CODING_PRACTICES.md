# Coding Best Practices

## Overview

This document outlines coding standards and best practices for the CaptureTool codebase to ensure consistency, maintainability, and quality.

## General Principles

### 1. SOLID Principles

- **Single Responsibility**: Each class should have one reason to change
- **Open/Closed**: Open for extension, closed for modification
- **Liskov Substitution**: Derived classes must be substitutable for their base classes
- **Interface Segregation**: Many specific interfaces better than one general interface
- **Dependency Inversion**: Depend on abstractions, not concretions

### 2. Don't Repeat Yourself (DRY)

Avoid code duplication. Extract common logic into:
- Base classes (e.g., `ViewModelBase`, `ActionCommand`)
- Services (e.g., `ISettingsService`)
- Helper methods (e.g., `TelemetryHelper`)
- Shared actions

### 3. You Aren't Gonna Need It (YAGNI)

Don't add functionality until it's needed. Avoid:
- Premature abstractions
- Unused parameters
- Overly generic solutions

### 4. Keep It Simple (KISS)

Simple solutions are easier to understand, test, and maintain.

## Code Organization

### Project Structure

```
src/
├── CaptureTool.Common/              # Shared base classes and utilities
├── CaptureTool.Core.Interfaces/     # Core business logic interfaces
├── CaptureTool.Core.Implementations/ # Core business logic implementations
├── CaptureTool.Services.Interfaces/  # Service abstractions
├── CaptureTool.Services.Implementations/ # Platform-agnostic services
├── CaptureTool.Services.Implementations.Windows/ # Windows-specific services
├── CaptureTool.Domains.*.Interfaces/ # Domain-specific interfaces
├── CaptureTool.Domains.*.Implementations.*/ # Domain implementations
├── CaptureTool.ViewModels/          # Presentation logic
├── CaptureTool.UI.Windows/          # WinUI 3 UI layer
└── *.Tests/                         # Unit tests
```

### Namespace Conventions

- Match folder structure
- Use PascalCase
- Avoid deeply nested namespaces (max 4 levels recommended)

### File Organization

One public type per file, named after the type:
- `HomePageViewModel.cs` contains `HomePageViewModel`
- `ISettingsService.cs` contains `ISettingsService`

## Naming Conventions

### Classes and Interfaces

```csharp
// Interfaces start with 'I'
public interface ISettingsService { }

// Implementations describe what they are
public class LocalSettingsService : ISettingsService { }

// ViewModels end with 'ViewModel'
public class HomePageViewModel : ViewModelBase { }

// Actions end with 'Action'
public class HomeNewImageCaptureAction : ActionCommand { }

// Factories end with 'Factory'
public class AppThemeViewModelFactory { }
```

### Methods

```csharp
// Use PascalCase
public void LoadSettings() { }

// Use descriptive names
public async Task SaveUserPreferencesAsync() { } // Good
public async Task SaveAsync() { } // Less clear

// Async methods end with 'Async'
public async Task InitializeAsync() { }

// Boolean methods start with 'Can', 'Is', 'Has', 'Should'
public bool CanExecute() { }
public bool IsInitialized() { }
public bool HasChanges() { }
```

### Variables and Parameters

```csharp
// Use camelCase for local variables and parameters
public void ProcessFile(string filePath)
{
    var fileContent = File.ReadAllText(filePath);
}

// Use _camelCase for private fields
private readonly ISettingsService _settingsService;

// Use PascalCase for properties
public string UserName { get; set; }

// Use PascalCase for constants
private const int MaxRetryAttempts = 3;
```

## Dependency Injection

### Registration

Register services in `AppServiceProvider`:

```csharp
// Singletons for stateful services
collection.AddSingleton<ISettingsService, LocalSettingsService>();

// Transient for stateless services and ViewModels
collection.AddTransient<HomePageViewModel>();
collection.AddTransient<IHomeNewImageCaptureAction, HomeNewImageCaptureAction>();
```

### Constructor Injection

```csharp
public class HomePageViewModel : ViewModelBase
{
    private readonly IHomeActions _homeActions;
    private readonly ITelemetryService _telemetryService;
    
    public HomePageViewModel(
        IHomeActions homeActions,
        ITelemetryService telemetryService)
    {
        _homeActions = homeActions;
        _telemetryService = telemetryService;
        
        NewImageCaptureCommand = new(NewImageCapture);
    }
}
```

**Rules:**
- Always inject interfaces, not implementations
- Store injected dependencies in readonly fields
- Keep constructors simple (assignment only)
- Don't call virtual methods in constructors

### Avoiding Service Locator

❌ **Don't do this:**
```csharp
public void DoSomething()
{
    var service = AppServiceLocator.GetService<IMyService>();
    service.Execute();
}
```

✅ **Do this:**
```csharp
private readonly IMyService _myService;

public MyClass(IMyService myService)
{
    _myService = myService;
}

public void DoSomething()
{
    _myService.Execute();
}
```

## Async/Await

### Best Practices

```csharp
// Always use ConfigureAwait(false) in libraries (not needed in UI code)
// WinUI apps can omit it

// Return Task directly when possible
public Task LoadAsync(CancellationToken ct)
{
    return _service.LoadDataAsync(ct);
}

// Use async/await when you need to do work after awaiting
public async Task LoadAsync(CancellationToken ct)
{
    await _service.LoadDataAsync(ct);
    IsLoaded = true;
}

// Always accept CancellationToken for long-running operations
public async Task ProcessAsync(CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    // ...
}
```

### Naming

```csharp
// Async methods must end with 'Async'
public async Task LoadSettingsAsync() { }

// Corresponding sync methods don't have suffix
public void LoadSettings() { }

// Interfaces reflect async nature
public interface IAsyncActionCommand
{
    Task ExecuteAsync(CancellationToken ct);
}
```

## MVVM Patterns

### ViewModels

```csharp
public sealed partial class HomePageViewModel : ViewModelBase
{
    // Commands
    public RelayCommand NewImageCaptureCommand { get; }
    public AsyncRelayCommand LoadCommand { get; }
    
    // Properties (use property pattern with field)
    public bool IsEnabled
    {
        get => field;
        private set => Set(ref field, value);
    }
    
    // Constructor
    public HomePageViewModel(IDependency dependency)
    {
        _dependency = dependency;
        NewImageCaptureCommand = new(OnNewImageCapture);
    }
    
    // Private methods
    private void OnNewImageCapture()
    {
        _dependency.Execute();
    }
}
```

### Commands

```csharp
// Use RelayCommand for simple UI commands
NewImageCaptureCommand = new(NewImageCapture);

// Use AsyncRelayCommand for async operations
LoadCommand = new(LoadAsync);

// Use RelayCommand<T> when you need parameters
DeleteItemCommand = new<int>(DeleteItem);

// Actions from Core layer for business logic
_homeActions.NewImageCapture();
```

### Property Change Notification

```csharp
// Use Set method from ViewModelBase
public string UserName
{
    get => field;
    set => Set(ref field, value);
}

// Raises PropertyChanged automatically
// field keyword (C# 13+) provides compiler-generated backing field
```

## Action Pattern

### Creating Actions

```csharp
// Simple action
public class HomeNewImageCaptureAction : ActionCommand, IHomeNewImageCaptureAction
{
    private readonly IAppNavigation _appNavigation;
    
    public HomeNewImageCaptureAction(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }
    
    public override void Execute()
    {
        _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
    }
}

// Action with parameter
public class SettingsClearTempFilesAction : ActionCommand<string>, ISettingsClearTempFilesAction
{
    public override void Execute(string tempFolderPath)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(tempFolderPath))
        {
            throw new ArgumentException("Path cannot be null or empty", nameof(tempFolderPath));
        }
        
        // Perform action
        ClearFolder(tempFolderPath);
    }
}

// Async action
public class SettingsRestoreDefaultsAction : AsyncActionCommand, ISettingsRestoreDefaultsAction
{
    private readonly ISettingsService _settingsService;
    
    public SettingsRestoreDefaultsAction(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }
    
    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _settingsService.ClearAllSettings();
        await _settingsService.TrySaveAsync(cancellationToken);
    }
}
```

### Action Aggregates

```csharp
// Combine related actions into an aggregate
public interface ISettingsActions
{
    void GoBack();
    void RestartApp();
    Task UpdateImageAutoCopyAsync(bool value, CancellationToken ct);
}

public class SettingsActions : ISettingsActions
{
    private readonly ISettingsGoBackAction _goBack;
    private readonly ISettingsRestartAppAction _restart;
    private readonly ISettingsUpdateImageAutoCopyAction _updateImageAutoCopy;
    
    // Inject individual actions
    public SettingsActions(
        ISettingsGoBackAction goBack,
        ISettingsRestartAppAction restart,
        ISettingsUpdateImageAutoCopyAction updateImageAutoCopy)
    {
        _goBack = goBack;
        _restart = restart;
        _updateImageAutoCopy = updateImageAutoCopy;
    }
    
    // Delegate to individual actions
    public void GoBack() => _goBack.ExecuteCommand();
    public void RestartApp() => _restart.ExecuteCommand();
    public Task UpdateImageAutoCopyAsync(bool value, CancellationToken ct) 
        => _updateImageAutoCopy.ExecuteCommandAsync(value);
}
```

## Factory Pattern

Use factories for objects requiring runtime parameters:

```csharp
// Define factory interface
public interface IFactoryServiceWithArgs<TResult, TArg>
{
    TResult Create(TArg arg);
}

// Implement factory
public class SettingsOpenTempFolderActionFactory 
    : IFactoryServiceWithArgs<ISettingsOpenTempFolderAction, string>
{
    public ISettingsOpenTempFolderAction Create(string tempFolderPath)
    {
        return new SettingsOpenTempFolderAction(tempFolderPath);
    }
}

// Register in DI
collection.AddTransient<
    IFactoryServiceWithArgs<ISettingsOpenTempFolderAction, string>, 
    SettingsOpenTempFolderActionFactory>();

// Use in ViewModels
public class SettingsPageViewModel
{
    private readonly IFactoryServiceWithArgs<ISettingsOpenTempFolderAction, string> _factory;
    
    public SettingsPageViewModel(
        IFactoryServiceWithArgs<ISettingsOpenTempFolderAction, string> factory)
    {
        _factory = factory;
    }
    
    private void OpenTempFolder()
    {
        var action = _factory.Create(TemporaryFilesFolderPath);
        action.Execute();
    }
}
```

## Testing

### Unit Test Structure

```csharp
[TestClass]
public class HomePageViewModelTests
{
    [TestMethod]
    public void NewImageCapture_WhenCalled_ExecutesHomeAction()
    {
        // Arrange - Set up mocks and dependencies
        var mockHomeActions = new Mock<IHomeActions>();
        var mockTelemetry = new Mock<ITelemetryService>();
        var mockFeatureManager = new Mock<IFeatureManager>();
        
        var vm = new HomePageViewModel(
            mockHomeActions.Object,
            mockFeatureManager.Object,
            mockTelemetry.Object);
        
        // Act - Execute the operation
        vm.NewImageCaptureCommand.Execute(null);
        
        // Assert - Verify the results
        mockHomeActions.Verify(x => x.NewImageCapture(), Times.Once);
    }
}
```

### Test Naming

`MethodName_Scenario_ExpectedBehavior`

Examples:
- `Execute_WhenFolderDoesNotExist_ThrowsException`
- `LoadAsync_WhenCanceled_ThrowsOperationCanceledException`
- `CanExecute_WhenNotInitialized_ReturnsFalse`

### Mocking with Moq

```csharp
// Setup method return value
mock.Setup(x => x.GetValue()).Returns(42);

// Setup async method
mock.Setup(x => x.GetValueAsync()).ReturnsAsync(42);

// Setup method to throw exception
mock.Setup(x => x.Execute()).Throws<InvalidOperationException>();

// Verify method was called
mock.Verify(x => x.Execute(), Times.Once);

// Verify method was never called
mock.Verify(x => x.Execute(), Times.Never);
```

## Comments

### When to Comment

✅ **Do comment:**
- Complex algorithms
- Non-obvious business rules
- Public APIs (XML documentation)
- Workarounds and why they exist
- TODOs with context

❌ **Don't comment:**
- Obvious code
- Instead of refactoring bad code
- Old code (use version control)

### XML Documentation

```csharp
/// <summary>
/// Loads user settings from persistent storage.
/// </summary>
/// <param name="filePath">The path to the settings file.</param>
/// <param name="cancellationToken">Token to cancel the operation.</param>
/// <returns>A task representing the async operation.</returns>
/// <exception cref="FileNotFoundException">If the settings file doesn't exist.</exception>
public async Task LoadAsync(string filePath, CancellationToken cancellationToken)
{
    // Implementation
}
```

## Performance

### General Guidelines

- Avoid premature optimization
- Profile before optimizing
- Focus on algorithmic improvements first

### Specific Recommendations

```csharp
// Use StringBuilder for string concatenation in loops
var sb = new StringBuilder();
for (int i = 0; i < 1000; i++)
{
    sb.Append(i);
}

// Use collection expressions (C# 12+)
List<int> numbers = [1, 2, 3, 4, 5];
int[] array = [1, 2, 3];

// Use collection initializers
var dict = new Dictionary<string, int>
{
    ["one"] = 1,
    ["two"] = 2
};

// Dispose resources properly
using var stream = File.OpenRead(path);
// or
using (var stream = File.OpenRead(path))
{
    // Use stream
}

// Use async I/O for file operations
await File.WriteAllTextAsync(path, content);

// Use ConfigureAwait(false) in library code (not UI code)
// WinUI apps typically don't need this
```

## Security

### Input Validation

```csharp
public void ProcessFile(string filePath)
{
    if (string.IsNullOrWhiteSpace(filePath))
    {
        throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
    }
    
    if (!File.Exists(filePath))
    {
        throw new FileNotFoundException($"File not found: {filePath}");
    }
    
    // Process file
}
```

### Avoid SQL Injection (if applicable)

Use parameterized queries or ORMs.

### Don't Log Sensitive Data

```csharp
// Bad
_logger.LogInfo($"User {username} logged in with password {password}");

// Good
_logger.LogInfo($"User {username} logged in successfully");
```

## Code Review Checklist

Before submitting code:

- [ ] Follows naming conventions
- [ ] Follows project structure
- [ ] Uses dependency injection properly
- [ ] Handles errors appropriately
- [ ] Includes unit tests
- [ ] No commented-out code
- [ ] No debug statements
- [ ] XML documentation for public APIs
- [ ] Async methods end with 'Async'
- [ ] Disposal handled properly
- [ ] No security vulnerabilities
- [ ] No performance issues

## Resources

- [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [.NET Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)
- [Clean Code by Robert C. Martin](https://www.amazon.com/Clean-Code-Handbook-Software-Craftsmanship/dp/0132350882)
- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)
