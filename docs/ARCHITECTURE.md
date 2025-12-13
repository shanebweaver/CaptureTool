# CaptureTool Architecture

## Overview

CaptureTool is a Windows screen capture application built using Clean Architecture principles with WinUI 3. The application follows a layered architecture that separates concerns and promotes maintainability, testability, and extensibility.

## Architecture Layers

### 1. UI Layer (`CaptureTool.UI.Windows`)
- **Responsibility**: Presentation and user interaction
- **Technology**: WinUI 3, XAML
- **Key Components**:
  - Windows, Pages, Views, Controls
  - XAML UI definitions
  - Platform-specific UI implementations
  
**Pattern**: MVVM (Model-View-ViewModel) with View-first approach using dependency injection.

### 2. ViewModels Layer (`CaptureTool.ViewModels`)
- **Responsibility**: Presentation logic and UI state management
- **Dependencies**: Core Interfaces, Services Interfaces
- **Key Components**:
  - ViewModels for each page/view
  - ViewModel factories for creating complex view models
  - Command patterns (RelayCommand, AsyncRelayCommand)
  
**Pattern**: MVVM ViewModels with testable business logic.

### 3. Core Layer
Split into interfaces and implementations for better testability and dependency inversion:

#### Core Interfaces (`CaptureTool.Core.Interfaces`)
- Actions interfaces (commands that perform business operations)
- Navigation interfaces
- Settings definitions
- Feature management constants

#### Core Implementations (`CaptureTool.Core.Implementations`)
- Action implementations (business logic commands)
- Navigation logic
- Image/Video capture handlers

### 4. Domain Layer
Domain-specific implementations separated by platform and feature:

#### Capture Domain
- **Interfaces** (`CaptureTool.Domains.Capture.Interfaces`): Screen capture abstractions
- **Windows Implementation** (`CaptureTool.Domains.Capture.Implementations.Windows`): Windows-specific screen capture using Windows APIs

#### Edit Domain
- **Interfaces** (`CaptureTool.Domains.Edit.Interfaces`): Image/video editing abstractions
- **Windows Implementation** (`CaptureTool.Domains.Edit.Implementations.Windows`): Windows-specific editing using Win2D

### 5. Services Layer
Infrastructure services separated by concern:

#### Service Interfaces (`CaptureTool.Services.Interfaces`)
- Platform-agnostic service contracts
- ~38 service interfaces covering:
  - Storage, Settings, Logging, Telemetry
  - Navigation, Themes, Localization
  - Clipboard, File Pickers, Share
  - Feature Management, Windowing

#### Service Implementations
- **Generic** (`CaptureTool.Services.Implementations`): Platform-agnostic implementations
- **Windows** (`CaptureTool.Services.Implementations.Windows`): Windows-specific implementations
- **Feature Management** (`CaptureTool.Services.Implementations.FeatureManagement`): Feature flag system

### 6. Common Layer (`CaptureTool.Common`)
- **Responsibility**: Shared utilities, base classes, and patterns
- **Key Components**:
  - ViewModelBase classes (with loading support)
  - Command pattern implementations (ActionCommand, RelayCommand, AsyncCommand)
  - Settings abstractions
  - Loading interfaces

### 7. Interop Layer (`CaptureInterop`)
- **Technology**: C++ with Windows Implementation Library (WIL)
- **Responsibility**: Low-level Windows API interactions
- **Purpose**: Performance-critical operations requiring direct Win32 access

## Design Patterns

### 1. Dependency Injection (DI)
**Implementation**: Microsoft.Extensions.DependencyInjection

All dependencies are registered in `AppServiceProvider` and injected through constructors. This enables:
- Testability (easy mocking)
- Loose coupling
- Centralized configuration

```csharp
// Service registration
collection.AddSingleton<INavigationService, NavigationService>();
collection.AddTransient<HomePageViewModel>();

// Service consumption
public HomePageViewModel(
    IHomeActions homeActions,
    IFeatureManager featureManager,
    ITelemetryService telemetryService)
{
    _homeActions = homeActions;
    _telemetryService = telemetryService;
    // ...
}
```

### 2. Command Pattern
**Purpose**: Encapsulate actions as objects for decoupling UI from business logic

Three command types:
- **ActionCommand**: Synchronous actions with execution logic
- **RelayCommand**: Lightweight commands delegating to methods
- **AsyncCommand**: Asynchronous operations with cancellation support

```csharp
public interface IActionCommand
{
    bool CanExecute();
    void Execute();
}

// Usage in ViewModels
public RelayCommand NewImageCaptureCommand { get; }
```

### 3. Action Pattern
**Purpose**: Business logic encapsulation with single responsibility

Each action represents one business operation:
```csharp
public interface IHomeNewImageCaptureAction : IActionCommand { }

public class HomeNewImageCaptureAction : ActionCommand, IHomeNewImageCaptureAction
{
    private readonly IAppNavigation _appNavigation;
    
    public override void Execute()
    {
        _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
    }
}
```

Benefits:
- Single Responsibility Principle
- Easy to test in isolation
- Composable (combined in action aggregates)

### 4. Factory Pattern
**Purpose**: Create complex ViewModels with runtime parameters

```csharp
public interface IFactoryServiceWithArgs<TResult, TArg>
{
    TResult Create(TArg arg);
}

// Used for ViewModels that need runtime data
collection.AddTransient<IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?>, 
                        AppLanguageViewModelFactory>();
```

### 5. Repository Pattern (Settings)
**Purpose**: Abstract data persistence

Settings are managed through `ISettingsService`:
- Type-safe setting definitions
- Centralized storage
- Event notification on changes

### 6. Strategy Pattern (Feature Flags)
**Purpose**: Runtime feature toggling

```csharp
if (_featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture))
{
    // Enable video capture UI
}
```

## Dependency Flow

```
UI Layer
  ↓ (depends on)
ViewModels Layer
  ↓ (depends on)
Core Interfaces ← (implemented by) → Core Implementations
  ↓ (depends on)
Services Interfaces ← (implemented by) → Services Implementations
  ↓ (depends on)
Domain Interfaces ← (implemented by) → Domain Implementations
```

**Key Principle**: Dependencies point inward (toward abstractions), never outward (toward concrete implementations).

## Project Organization

### Naming Conventions
- **Interfaces**: `I{Name}` prefix, in `*.Interfaces` projects
- **Implementations**: `{Name}`, in `*.Implementations` or `*.Implementations.{Platform}` projects
- **ViewModels**: `{Page/View}ViewModel` suffix
- **Actions**: `{Feature}{Action}Action` pattern (e.g., `HomeNewImageCaptureAction`)

### Project Dependencies
Uses centralized package management:
- `Directory.Packages.props`: Package versions
- `Directory.Build.props`: Shared build configuration
- `Directory.Version.props`: Application versioning

## Testing Strategy

### Test Projects
- `CaptureTool.Core.Tests`
- `CaptureTool.ViewModels.Tests`
- `CaptureTool.Services.Tests`
- `CaptureTool.Domains.Edit.Tests`

### Testing Approach
- **Unit Tests**: MSTest with Moq and AutoFixture
- **Isolation**: Interfaces enable easy mocking
- **Coverage**: Focus on business logic (ViewModels, Actions, Services)

### Test Structure
```csharp
[TestClass]
public class HomePageViewModelTests
{
    [TestMethod]
    public void NewImageCapture_ExecutesHomeAction()
    {
        // Arrange
        var mockHomeActions = new Mock<IHomeActions>();
        var vm = new HomePageViewModel(mockHomeActions.Object, ...);
        
        // Act
        vm.NewImageCaptureCommand.Execute(null);
        
        // Assert
        mockHomeActions.Verify(x => x.NewImageCapture(), Times.Once);
    }
}
```

## Recent Improvements

### 1. Eliminated Service Locator Anti-pattern
**Previous**: `AppServiceLocator` provided static access to services
```csharp
// Before (anti-pattern)
AppServiceLocator.Logging.LogException(ex);
AppServiceLocator.Navigation.GoToImageEdit(image);
```

**Current**: Proper dependency injection
```csharp
// After (proper DI)
public class PageBase<VM> : Page
{
    private readonly ILogService _logService;
    private readonly IAppNavigation _appNavigation;
    
    public PageBase()
    {
        _logService = App.Current.ServiceProvider.GetService<ILogService>();
        _appNavigation = App.Current.ServiceProvider.GetService<IAppNavigation>();
    }
}
```

**Benefits**:
- Improved testability (dependencies visible)
- Better maintainability (explicit dependencies)
- Reduced coupling (no global state)

## Key Architectural Principles

### 1. **Separation of Concerns**
Each layer has a clear, distinct responsibility.

### 2. **Dependency Inversion**
High-level modules don't depend on low-level modules; both depend on abstractions.

### 3. **Interface Segregation**
Small, focused interfaces (e.g., separate action interfaces for each operation).

### 4. **Single Responsibility**
Each class has one reason to change (e.g., one action per class).

### 5. **Open/Closed Principle**
Open for extension (new implementations), closed for modification (interfaces stable).

### 6. **Don't Repeat Yourself (DRY)**
Shared functionality in base classes (`ViewModelBase`, `ActionCommand`) and services.

## Performance Considerations

### 1. Async/Await Pattern
Heavy operations use async to keep UI responsive:
```csharp
public AsyncRelayCommand<bool> UpdateImageCaptureAutoSaveCommand { get; }
```

### 2. Lazy Loading
ViewModels load data on-demand when navigated to.

### 3. Disposal Pattern
Proper cleanup of resources:
- CancellationTokenSource disposal
- ViewModel disposal on navigation
- Service disposal in DI container

### 4. Memory Management
- Explicit GC collection after clearing navigation stacks
- WeakReference patterns where appropriate

## Future Considerations

### Potential Improvements

1. **Result Pattern**: Improve error handling with Result<T> types instead of exceptions for expected failures
2. **CQRS Pattern**: Separate read and write operations for complex scenarios
3. **Mediator Pattern**: For complex inter-feature communication
4. **Event Sourcing**: For undo/redo functionality in image editing
5. **Plugin Architecture**: For extensible capture/edit filters

### Scalability
The current architecture supports:
- Adding new platforms (by implementing platform-specific services)
- Adding new features (through interfaces and feature flags)
- Adding new capture/edit modes (through strategy pattern)

## Common Scenarios

### Adding a New Feature

1. Define interfaces in `Core.Interfaces` or `Services.Interfaces`
2. Implement in appropriate implementation project
3. Register in `AppServiceProvider`
4. Add feature flag if optional
5. Create ViewModel if UI needed
6. Wire up in XAML

### Adding Platform Support

1. Create new `*.Implementations.{Platform}` project
2. Implement platform-specific services
3. Conditional registration in `AppServiceProvider`
4. Platform-specific builds in CI/CD

### Adding Tests

1. Create test in appropriate test project
2. Use Moq to mock interfaces
3. Use AutoFixture for test data
4. Follow AAA pattern (Arrange, Act, Assert)

## Resources

- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [MVVM Pattern](https://learn.microsoft.com/en-us/windows/apps/design/basics/navigation-basics)
- [Dependency Injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [WinUI 3 Documentation](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
