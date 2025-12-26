# 10 Rust Principles for C++ and C# Codebases

This document outlines 10 core principles inspired by Rust's design philosophy, adapted for application in C++ and C# projects to improve reliability, maintainability, and safety.

## Principle #1: Ownership and Borrowing

**Concept**: Every resource has a single owner, and ownership can be transferred or borrowed.

**C++ Implementation**:
- Use `std::unique_ptr<T>` for exclusive ownership
- Use `std::shared_ptr<T>` only when truly needed
- Use raw pointers or references for borrowing (non-owning access)
- Document ownership clearly in comments and function signatures

**C# Implementation**:
- Use IDisposable pattern for resource ownership
- Document ownership in XML comments
- Consider using `using` statements for automatic cleanup

## Principle #2: Move Semantics

**Concept**: Transfer ownership explicitly to avoid expensive copies.

**C++ Implementation**:
- Implement move constructors and move assignment operators
- Use `std::move()` to transfer ownership
- Delete copy operations when appropriate

**C# Implementation**:
- Transfer ownership through method parameters
- Use `IDisposable.Dispose()` to release resources explicitly

## Principle #3: No Nullable Pointers

**Concept**: Make null states explicit in the type system.

**C++ Implementation**:
- Use `std::optional<T>` instead of nullable pointers when a value may not exist
- Use references when a value must exist
- Avoid raw pointers except for non-owning borrows

**C# Implementation**:
- Enable nullable reference types
- Use `Nullable<T>` or `T?` for value types
- Use nullable reference types `T?` for reference types

## Principle #4: Explicit Error Handling

**Concept**: Make errors visible and impossible to ignore.

**C++ Implementation**:
- Use `std::expected<T, E>` or similar result types
- Avoid exceptions for expected failure cases
- Document error conditions clearly

**C# Implementation**:
- Use Result<T, E> pattern when appropriate
- Use exceptions for truly exceptional cases
- Document expected errors in XML comments

## Principle #5: RAII Everything

**Concept**: Resource Acquisition Is Initialization - tie resource lifetime to object lifetime.

**C++ Implementation**:
- Acquire resources in constructors
- Release resources in destructors
- Use stack allocation when possible
- Wrap all OS resources (handles, locks, memory) in RAII types

**C# Implementation**:
- Implement IDisposable for all resource-owning types
- Use `using` statements or `using` declarations
- Implement finalizers only when managing unmanaged resources

## Principle #6: No Globals

**Concept**: Avoid global mutable state.

**Implementation**:
- Pass dependencies explicitly through constructors (dependency injection)
- Use context objects to carry state through call chains
- Make static/global data const/readonly when necessary
- Prefer composition over global singletons

## Principle #7: Const Correctness

**Concept**: Express mutability explicitly in the type system.

**C++ Implementation**:
- Use `const` for all read-only parameters and methods
- Use `constexpr` for compile-time constants
- Make data members const when they shouldn't change

**C# Implementation**:
- Use `readonly` for fields that shouldn't change after construction
- Use `const` for compile-time constants
- Use `IReadOnlyCollection<T>` and similar interfaces

## Principle #8: Thread Safety by Design

**Concept**: Make concurrent access patterns explicit and safe.

**C++ Implementation**:
- Use `std::atomic<T>` for shared counters/flags
- Use `std::mutex` and RAII lock guards (`std::lock_guard`, `std::unique_lock`)
- Document thread-safety guarantees
- Use `const` methods for thread-safe read-only operations

**C# Implementation**:
- Use `lock` statements or `Monitor` for mutual exclusion
- Use `Interlocked` for atomic operations
- Use concurrent collections from `System.Collections.Concurrent`
- Document thread-safety in XML comments

## Principle #9: Encode State Machines in Types

**Concept**: Use the type system to prevent invalid states and transitions.

**Implementation**:
- Create separate types for each state (when beneficial)
- Use enums for state with validation logic
- Make invalid state transitions impossible at compile time
- Use state machine classes that validate transitions

**Example** (C++):
```cpp
enum class SessionState { Created, Initialized, Active, Paused, Stopped, Failed };

class SessionStateMachine {
    bool TryTransitionTo(SessionState newState);
    bool IsValidTransition(SessionState from, SessionState to);
};
```

## Principle #10: Zero-Cost Abstractions

**Concept**: Abstractions should have no runtime overhead compared to hand-written code.

**Implementation**:
- Use templates/generics for compile-time polymorphism
- Use inline functions for small, frequently-called operations
- Prefer constexpr over runtime computation
- Use interfaces/virtual functions only when dynamic dispatch is necessary
- Profile to ensure abstractions don't add overhead

## Application in CaptureTool

### Phase 1: Session and Clock Components

This refactoring applies principles #3, #5, #6, and #9 to the session and clock components:

1. **Principle #3 (No Nullable Pointers)**: Using `std::unique_ptr` and avoiding raw nullable pointers
2. **Principle #5 (RAII Everything)**: Automatic cleanup in destructors
3. **Principle #6 (No Globals)**: Dependency injection, no global session state
4. **Principle #9 (Encode State Machines)**: `CaptureSessionStateMachine` class with validated transitions

### Benefits

- **Reliability**: Type system prevents common bugs
- **Maintainability**: Clear ownership and lifecycle management
- **Performance**: Zero-cost abstractions with compile-time safety
- **Documentation**: Code expresses intent through types

## References

- [The Rust Programming Language Book](https://doc.rust-lang.org/book/)
- [C++ Core Guidelines](https://isocpp.github.io/CppCoreGuidelines/)
- [Modern C++ Design Patterns](https://en.cppreference.com/w/cpp)
