# Next Improvements for Capture Session & Clock Code

## Recently Completed ✅

Recent PRs have successfully implemented major improvements:
- **State machine pattern** (PR #164) - Proper lifecycle management with validated state transitions
- **Modern C++ features** (PR #165) - std::span, constexpr, and other C++20/23 improvements
- **Error handling with Result types** (PR #163) - Structured error reporting instead of bool + HRESULT
- **Callback safety** (PR #163) - RAII handle pattern for safe callback lifecycle
- **Configuration validation** (PR #163) - Validate config parameters at construction time
- **Clean architecture patterns** (PR #162) - Dependency injection and clear separation of concerns
- **Memory leak fixes** (PRs #158, #160, #161) - Proper resource cleanup and lifetime management

## Remaining Improvements

### 1. Test Doubles for Unit Testing
**Priority: High** | **Effort: Medium (1 week)**

Currently, tests rely on real hardware (audio devices, graphics capture). Create mock implementations to enable fast, isolated unit testing.

**What to Create:**
- `src/CaptureInterop.Tests/Mocks/MockMediaClock.h` - Controllable time source for testing
- `src/CaptureInterop.Tests/Mocks/MockAudioCaptureSource.h` - Simulated audio samples
- `src/CaptureInterop.Tests/Mocks/MockVideoCaptureSource.h` - Simulated video frames
- `src/CaptureInterop.Tests/Mocks/MockMP4SinkWriter.h` - Verify encoding calls

**Benefits:**
- Fast unit tests (no hardware dependencies)
- Test edge cases easily (simulate errors, timing issues)
- Better code coverage
- CI/CD friendly (no audio/video hardware required)

**Example Usage:**
```cpp
TEST_METHOD(Session_Initialize_ConfiguresAllDependencies)
{
    CaptureSessionConfig config(nullptr, L"test.mp4");
    auto mockClock = std::make_unique<MockMediaClock>();
    auto mockAudio = std::make_unique<MockAudioCaptureSource>();
    auto mockVideo = std::make_unique<MockVideoCaptureSource>();
    auto mockSink = std::make_unique<MockMP4SinkWriter>();
    
    auto* clockPtr = mockClock.get();
    WindowsGraphicsCaptureSession session(
        config,
        std::move(mockClock),
        std::move(mockAudio),
        std::move(mockVideo),
        std::move(mockSink));
    
    auto result = session.Initialize();
    
    Assert::IsTrue(result.IsOk());
    Assert::IsTrue(mockAudio->WasInitialized());
    Assert::IsTrue(clockPtr->WasStartCalled());
}
```

**Implementation Notes:**
- Each mock should implement its respective interface (IMediaClock, IAudioCaptureSource, etc.)
- Use simple state tracking (booleans, counters) to verify behavior
- Keep mocks simple - they're for testing, not production
- Reference existing mock patterns from PR #163 documentation

---

### 2. Clock Pause/Resume Implementation
**Priority: Medium** | **Effort: Low-Medium (3-5 days)**

The `SimpleMediaClock` class has `Pause()` and `Resume()` methods, but they're not fully integrated with the capture session.

**Current State:**
```cpp
void SimpleMediaClock::Pause() {
    // TODO: Implement pause logic
}

void SimpleMediaClock::Resume() {
    // TODO: Implement resume logic
}
```

**What to Implement:**
1. **Clock pause behavior** - Freeze time advancement when paused
2. **Resume from correct position** - Continue from where we left off
3. **Source coordination** - Audio/video sources respect pause state
4. **Session integration** - Wire Pause()/Resume() through ICaptureSession
5. **State validation** - Can only pause when Active, resume when Paused

**Example:**
```cpp
session.Start();  // Clock starts at 0
// ... 5 seconds of recording ...
session.Pause();  // Clock frozen at 5.0 seconds
// ... 10 seconds pass in real time ...
session.Resume(); // Clock continues from 5.0 seconds
```

**Files to Modify:**
- `src/CaptureInterop.Lib/SimpleMediaClock.cpp` - Implement pause/resume logic
- `src/CaptureInterop.Lib/WindowsGraphicsCaptureSession.cpp` - Wire to session lifecycle
- `src/CaptureInterop.Lib/ICaptureSession.h` - Add Pause()/Resume() to interface
- `src/CaptureInterop.Tests/SimpleMediaClockTests.cpp` - Add pause/resume tests

**Testing Requirements:**
- Clock time doesn't advance when paused
- Resume continues from correct position
- Audio/video sources honor pause state
- Multiple pause/resume cycles work correctly

---

### 3. Dependency Injection Audit
**Priority: Medium** | **Effort: Low (2-3 days)**

PR #162 added DI, but there's room for improvement in ownership clarity.

**Current Pattern:**
```cpp
// Factory creates dependencies
auto mediaClock = mediaClockFactory->CreateMediaClock();
auto audio = audioFactory->CreateAudioCaptureSource();

// Session receives raw pointers (ambiguous ownership)
auto session = std::make_unique<WindowsGraphicsCaptureSession>(
    config,
    mediaClock.get(),  // Who owns this?
    audio.get(),       // Who owns this?
    // ...
);
```

**Recommended Pattern:**
```cpp
// Clear ownership transfer via std::unique_ptr
auto session = std::make_unique<WindowsGraphicsCaptureSession>(
    config,
    std::move(mediaClock),  // Session takes ownership
    std::move(audio),       // Session takes ownership
    // ...
);
```

**Tasks:**
1. Audit all factory interfaces - check if ownership is clear
2. Update constructors to accept `std::unique_ptr` where appropriate
3. Document ownership contracts in interface comments
4. Update all callers to use `std::move`

**Files to Review:**
- `src/CaptureInterop.Lib/IMediaClockFactory.h`
- `src/CaptureInterop.Lib/IAudioCaptureSourceFactory.h`
- `src/CaptureInterop.Lib/IVideoCaptureSourceFactory.h`
- `src/CaptureInterop.Lib/IMP4SinkWriterFactory.h`
- `src/CaptureInterop.Lib/WindowsGraphicsCaptureSessionFactory.cpp`

---

### 4. Configuration Immutability
**Priority: Low** | **Effort: Low (1-2 days)**

Make `CaptureSessionConfig` immutable after construction to prevent mid-session modification bugs.

**Current:**
```cpp
struct CaptureSessionConfig {
    std::wstring outputPath;
    uint32_t videoWidth;
    uint32_t videoHeight;
    // ... mutable members
};
```

**Recommended:**
```cpp
struct CaptureSessionConfig {
    const std::wstring outputPath;
    const uint32_t videoWidth;
    const uint32_t videoHeight;
    // ... all const members
    
    // Use designated initializers for construction
};
```

**Benefits:**
- Prevents accidental modification after session starts
- Makes config lifetime simpler (copy-free after construction)
- Clearer intent - config is a "value type"

**Note:** This is a breaking change if any code modifies config after construction. Audit usage first.

---

### 5. Clock Advancer Validation
**Priority: Low** | **Effort: Low (1 day)**

Add validation that only one clock advancer can be registered at a time.

**Current Behavior:**
```cpp
mediaClock.SetClockAdvancer(&audioSource);  // OK
mediaClock.SetClockAdvancer(&videoSource);  // Silently replaces! Could be a bug
```

**Recommended:**
```cpp
// Add validation
void SimpleMediaClock::SetClockAdvancer(IMediaClockAdvancer* advancer) {
    if (m_advancer != nullptr && advancer != nullptr) {
        // Log warning or assert
        // This is likely a bug - only audio should advance the clock
    }
    m_advancer = advancer;
}
```

**Also Add:**
- Documentation explaining why audio is the authoritative time source
- Test that verifies only one advancer can be set
- Handle advancer lifecycle (what if audio source fails to start?)

---

## Implementation Roadmap

### Phase 1: Enable Better Testing (Week 1-2)
- Create test doubles for all major interfaces
- Add mock-based unit tests for session lifecycle
- Document mock patterns for future use

### Phase 2: Complete Clock Features (Week 3)
- Implement pause/resume for SimpleMediaClock
- Integrate with session lifecycle
- Add comprehensive tests

### Phase 3: Architecture Polish (Week 4)
- DI audit and ownership clarification
- Configuration immutability
- Clock advancer validation

---

## Not Included (Out of Scope)

The following are **not** priorities right now per user request:
- ❌ Logging and telemetry infrastructure
- ❌ Performance profiling and metrics
- ❌ Timing diagnostics and drift detection
- ❌ Code organization refactoring

These can be revisited later if needed.

---

## Success Criteria

✅ All major interfaces have mock implementations  
✅ Unit tests can run without hardware  
✅ Pause/resume feature is fully functional  
✅ Ownership is unambiguous throughout  
✅ Configuration is immutable  
✅ Clock advancer has validation  
✅ Documentation is clear and focused
