# Architecture Goals and Design Patterns

## Purpose

This document outlines the architectural principles and design patterns to guide the evolution of the CaptureTool capture pipeline. These are generic goals that establish a foundation for building robust, maintainable, and testable code. The patterns described here are inspired by Clean Architecture principles and battle-tested patterns from systems programming languages like Rust.

## Core Architectural Principles

### 1. Separation of Concerns

**Goal**: Each component should have a single, well-defined responsibility.

**Guidelines**:
- Business logic should be independent of infrastructure details (Windows APIs, COM, Media Foundation)
- Domain models should not depend on presentation or persistence concerns
- Use layer boundaries to enforce separation (interfaces, abstract base classes)
- Dependencies should flow inward toward business logic, never outward

**Benefits**:
- Easier to test components in isolation
- Reduced coupling between subsystems
- Easier to understand and maintain code
- Flexibility to swap implementations

### 2. Dependency Inversion

**Goal**: High-level modules should not depend on low-level modules. Both should depend on abstractions.

**Guidelines**:
- Define interfaces for all major abstractions (clocks, sources, sinks)
- Concrete implementations depend on abstractions, not the reverse
- Use factories to create concrete instances while consuming code depends on interfaces
- Configuration and wiring should happen at composition root

**Benefits**:
- Enables dependency injection for testing
- Makes code more flexible and extensible
- Reduces ripple effects from changes
- Supports multiple implementations of the same abstraction

### 3. Interface Segregation

**Goal**: No client should be forced to depend on methods it does not use.

**Guidelines**:
- Keep interfaces small and focused (e.g., IMediaClockReader vs IMediaClockWriter)
- Split large interfaces into role-specific interfaces
- Favor composition of interfaces over monolithic interfaces
- Consider the consumer's needs when designing interfaces

**Benefits**:
- Reduces unnecessary coupling
- Makes testing easier (fewer methods to mock)
- Clearer contracts and expectations
- Better compatibility with existing code

### 4. Explicit Ownership and Lifetime Management

**Goal**: Resource ownership and lifetime should be explicit and predictable.

**Guidelines**:
- Use RAII (Resource Acquisition Is Initialization) pattern
- Constructor acquires resources, destructor releases them
- Use smart pointers (wil::com_ptr, std::unique_ptr, std::shared_ptr) to express ownership
- Avoid raw pointers except for non-owning references
- Make ownership transfer explicit through move semantics
- Disable copy operations when inappropriate (rule of zero or rule of five)

**Benefits**:
- Prevents resource leaks
- Makes lifetime expectations clear
- Reduces cognitive load on developers
- Catches lifetime errors at compile time

**Example patterns**:
```cpp
// Unique ownership - only one owner
std::unique_ptr<IMP4SinkWriter> writer;

// Shared ownership - reference counted
std::shared_ptr<IMediaClock> clock;

// Non-owning reference - caller manages lifetime
void ProcessFrame(IVideoCaptureSource* source);  // source != nullptr

// Optional non-owning reference
void ProcessFrame(IVideoCaptureSource* source = nullptr);

// COM lifetime - ref-counted
wil::com_ptr<ID3D11Device> device;
```

### 5. Fail Fast and Explicit Error Handling

**Goal**: Errors should be visible, handled explicitly, and fail as early as possible.

**Guidelines**:
- Use return types that make success/failure explicit (HRESULT, std::optional, bool with out params)
- Don't silently ignore errors
- Validate inputs at system boundaries
- Use assertions for programming errors (invariants, preconditions)
- Use error returns for runtime errors (file not found, device unavailable)
- Document error conditions in function contracts

**Benefits**:
- Easier to debug (errors caught close to root cause)
- More reliable code (no hidden error states)
- Clear error handling paths
- Better error messages for users

**Example patterns**:
```cpp
// Explicit success/failure with error detail
bool Initialize(HRESULT* outHr = nullptr);

// Optional return value
std::optional<VideoFrame> CaptureFrame();

// Result type with error detail
struct Result {
    bool success;
    HRESULT error;
};
```

### 6. Immutability by Default

**Goal**: Prefer immutable data structures and const-correctness.

**Guidelines**:
- Mark variables `const` whenever possible
- Pass by const reference for read-only parameters
- Make member functions `const` when they don't modify state
- Use `const` smart pointers when ownership is shared but read-only
- Document when mutation is intentional and necessary

**Benefits**:
- Easier to reason about code (no hidden state changes)
- Thread-safety by design
- Prevents accidental modifications
- Better optimization opportunities

### 7. Composability Over Inheritance

**Goal**: Favor composition and small, focused abstractions over deep inheritance hierarchies.

**Guidelines**:
- Prefer "has-a" relationships over "is-a" when possible
- Use interfaces for polymorphism, not implementation inheritance
- Keep inheritance hierarchies shallow (1-2 levels maximum)
- Use dependency injection to compose behavior
- Prefer strategies and policies over template method pattern

**Benefits**:
- More flexible code (composition is dynamic)
- Easier to test (inject test doubles)
- Avoids fragile base class problem
- Better encapsulation

### 8. Testability

**Goal**: Code should be designed to be testable in isolation.

**Guidelines**:
- All dependencies should be injectable via constructor or factory
- Use interfaces to enable test doubles (mocks, stubs, fakes)
- Avoid global state and singletons
- Separate construction from use
- Design with unit tests in mind
- Keep functions small and focused

**Benefits**:
- Faster test execution
- Better test coverage
- Easier to maintain tests
- Catches regressions early

## Pattern Catalog

### RAII (Resource Acquisition Is Initialization)

**Intent**: Bind resource lifetime to object lifetime, ensuring cleanup.

**When to use**:
- Managing Windows handles (HANDLE, HMONITOR)
- COM object lifetime (use wil::com_ptr)
- File handles and streams
- Locks and synchronization primitives
- D3D resources

**Implementation**:
- Acquire resource in constructor (or fail construction)
- Release resource in destructor
- Delete copy constructor and copy assignment
- Implement move constructor and move assignment if transfer is needed
- Use smart pointers instead of raw pointers

**Example**:
```cpp
class ScopedHandle {
public:
    explicit ScopedHandle(HANDLE h) : m_handle(h) {}
    ~ScopedHandle() { if (m_handle) CloseHandle(m_handle); }
    
    // Delete copy
    ScopedHandle(const ScopedHandle&) = delete;
    ScopedHandle& operator=(const ScopedHandle&) = delete;
    
    // Allow move
    ScopedHandle(ScopedHandle&& other) noexcept 
        : m_handle(std::exchange(other.m_handle, nullptr)) {}
    
    ScopedHandle& operator=(ScopedHandle&& other) noexcept {
        if (this != &other) {
            if (m_handle) CloseHandle(m_handle);
            m_handle = std::exchange(other.m_handle, nullptr);
        }
        return *this;
    }
    
    HANDLE get() const { return m_handle; }
    
private:
    HANDLE m_handle;
};
```

### Factory Pattern

**Intent**: Encapsulate object creation and return interface pointers.

**When to use**:
- Creating platform-specific implementations
- Complex object initialization
- Dependency injection configuration
- Testing (return mock implementations)

**Implementation**:
- Factory returns interface pointers (std::unique_ptr<IInterface>)
- Factory is itself an interface (IFactory) for testing
- Factory takes configuration as constructor parameters
- Consider abstract factory for families of related objects

**Example**:
```cpp
class IMediaClockFactory {
public:
    virtual ~IMediaClockFactory() = default;
    virtual std::unique_ptr<IMediaClock> Create() = 0;
};

class SimpleMediaClockFactory : public IMediaClockFactory {
public:
    std::unique_ptr<IMediaClock> Create() override {
        return std::make_unique<SimpleMediaClock>();
    }
};
```

### Strategy Pattern

**Intent**: Define a family of algorithms, encapsulate each one, and make them interchangeable.

**When to use**:
- Multiple algorithms for the same task (compression, encoding)
- Behavior that varies by configuration
- Replacing conditional logic with polymorphism
- Enabling testing with test doubles

**Implementation**:
- Define interface for the strategy
- Concrete implementations for each algorithm
- Context object holds reference to strategy
- Strategy can be changed at runtime

### Observer Pattern (Callback Pattern)

**Intent**: Define a one-to-many dependency where observers are notified of state changes.

**When to use**:
- Event notifications (frame arrived, sample captured)
- Decoupling event producers from consumers
- Enabling multiple subscribers to the same event
- Cross-layer communication (native to managed)

**Implementation**:
- Use function pointers or std::function for callbacks
- Consider thread safety (which thread invokes callback)
- Document callback lifetime expectations
- Allow registration and unregistration
- Consider using weak references to avoid circular dependencies

### Null Object Pattern

**Intent**: Provide a default object that does nothing, avoiding null checks.

**When to use**:
- Optional dependencies that may not be configured
- Default "no-op" implementations
- Simplifying code by eliminating null checks

**Implementation**:
- Create concrete implementation of interface that does nothing
- Return null object instead of nullptr when dependency is missing
- Document which operations are safe to call

**Example**:
```cpp
class NullLogger : public ILogger {
public:
    void Log(const char* message) override { /* do nothing */ }
};
```

### Builder Pattern

**Intent**: Construct complex objects step by step.

**When to use**:
- Objects with many optional parameters
- Construction requires multiple steps
- Validation across multiple parameters
- Creating immutable objects with many fields

**Implementation**:
- Builder has fluent interface (returns *this)
- Build() method validates and constructs final object
- Builder can enforce invariants that constructor cannot

### Guard Pattern

**Intent**: Validate preconditions and fail fast if violated.

**When to use**:
- Public API boundaries
- Validating user input
- Checking resource availability before use
- Enforcing state machine invariants

**Implementation**:
- Check preconditions at start of function
- Return early with error if condition fails
- Use assertions for programming errors
- Use error returns for runtime errors

**Example**:
```cpp
bool StartRecording(const char* path) {
    // Validate input parameters
    if (!path || *path == '\0') {
        return false; // Invalid input - null or empty string
    }
    
    // Validate state
    if (m_isRecording) {
        return false; // Invalid state - already recording
    }
    
    // Proceed with recording...
}
```

## Architecture Layers

### Domain Layer (Core Business Logic)

**Responsibilities**:
- Define domain concepts (VideoFrame, AudioSample, MediaClock)
- Define core interfaces (IVideoCaptureSource, IAudioCaptureSource, IMP4SinkWriter)
- Implement domain rules and invariants
- Independent of infrastructure

**Dependencies**:
- No dependencies on UI or infrastructure
- Only depends on standard library and domain interfaces

**Testing**:
- Pure unit tests with no infrastructure dependencies
- Test doubles for all dependencies

### Infrastructure Layer (Platform Implementations)

**Responsibilities**:
- Implement domain interfaces using platform APIs
- Wrap Windows Media Foundation, COM, D3D11
- Handle platform-specific error codes
- Manage platform resource lifetimes

**Dependencies**:
- Depends on domain interfaces
- Depends on platform SDKs (Windows, DirectX)
- Uses RAII for all platform resources

**Testing**:
- Integration tests on real platform
- Unit tests with mocked platform APIs where possible

### Application Layer (Orchestration)

**Responsibilities**:
- Coordinate domain objects to fulfill use cases
- Handle application-level workflows
- Manage transactions and sessions
- Error handling and recovery

**Dependencies**:
- Depends on domain interfaces
- Uses factories to obtain implementations
- No direct dependency on infrastructure

**Testing**:
- Test with mock implementations of domain interfaces
- Integration tests with real implementations

### Presentation Layer (UI)

**Responsibilities**:
- Display data to user
- Capture user input
- Handle UI-specific concerns (threading, data binding)
- Transform domain models to view models

**Dependencies**:
- Depends on application layer (use cases)
- May depend on platform UI frameworks
- Never directly depends on infrastructure

**Testing**:
- UI tests with mock application layer
- View model tests in isolation

## Threading and Concurrency

**Goals**:
- Explicit about which thread operations occur on
- Avoid data races and synchronization bugs
- Document thread safety expectations

**Guidelines**:
- Document thread safety in interface contracts
- Use const methods for thread-safe operations
- Protect mutable state with synchronization primitives
- Prefer message passing over shared state
- Use atomic operations for simple shared state
- Avoid blocking operations on high-priority threads
- Consider thread affinity for platform resources (COM, D3D)

**Patterns**:
- Single-threaded ownership (only one thread accesses object)
- Immutable shared state (read-only after construction)
- Synchronized shared state (mutex-protected)
- Message passing (producer-consumer queues)

## Memory Management

**Goals**:
- No memory leaks
- No use-after-free bugs
- Clear ownership semantics
- Efficient resource usage

**Guidelines**:
- Use smart pointers for ownership (unique_ptr, shared_ptr, com_ptr)
- Use raw pointers only for non-owning references
- Prefer stack allocation over heap when possible
- Use containers (vector, array) instead of raw arrays
- Follow rule of zero (no manual resource management) or rule of five (explicit control)
- Consider object pooling for frequently allocated objects
- Profile memory usage in performance-critical paths

## Error Handling Strategy

**Guidelines**:
- Use HRESULT for Windows API interop
- Use bool return + optional HRESULT out parameter for public APIs
- Use assertions (assert, THROW_IF_FAILED in debug) for programming errors
- Use error returns for runtime errors
- Log errors at the point of detection
- Provide context in error messages (what operation failed, why)
- Design for graceful degradation where possible
- Document error conditions in function contracts

## Documentation Standards

**Guidelines**:
- Document all public interfaces with XML comments
- Explain the "why" not just the "what"
- Document thread safety expectations
- Document ownership and lifetime semantics
- Document preconditions and postconditions
- Provide usage examples for complex APIs
- Keep documentation close to code (header files)

## Code Organization

**Guidelines**:
- One interface per header file (IMediaClock.h)
- One concrete class per implementation file pair (.h/.cpp)
- Group related interfaces in same directory
- Use namespaces to organize large codebases
- Keep public headers minimal (forward declarations)
- Separate public API from internal implementation

## Evolution Strategy

**Principles for evolving the codebase**:

1. **Start with interfaces**: Define what you need before how to implement it
2. **Refactor incrementally**: Small, safe steps with tests
3. **Respect existing code**: Don't break what works
4. **Test as you go**: Add tests for new code, add tests when refactoring
5. **Document decisions**: Capture why, not just what
6. **Review patterns**: Periodically assess if patterns are working
7. **Be pragmatic**: Apply patterns where they add value, not everywhere

## Future Considerations

As the architecture evolves, consider:

- **Async operations**: async/await patterns for long-running operations
- **Cancellation**: explicit cancellation tokens for stopping operations
- **Observability**: structured logging, metrics, tracing
- **Configuration**: externalized configuration, feature flags
- **Versioning**: API versioning strategy for evolution
- **Performance**: profiling, optimization, resource pooling
- **Resilience**: retry logic, circuit breakers, fallbacks

## Summary

These architectural goals and patterns provide a north star for evolving the CaptureTool capture pipeline. The key themes are:

- **Explicit is better than implicit** (ownership, errors, dependencies)
- **Interfaces over implementations** (testability, flexibility)
- **RAII for resource management** (correctness, safety)
- **Separation of concerns** (maintainability, understandability)
- **Fail fast** (reliability, debuggability)

By following these principles, we can build a codebase that is robust, maintainable, testable, and easy to evolve over time.
