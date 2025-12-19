# MediaClock Design - Implementation Plan

This directory contains the complete design documentation for implementing a shared `MediaClock` class for audio/video synchronization in the CaptureInterop project.

## 📋 Overview

The MediaClock provides a single, authoritative source of elapsed recording time, eliminating timing drift between audio and video pipelines and establishing a foundation for future architectural improvements.

## 📚 Documentation

### Main Documents

1. **[MediaClock-Architecture.md](./MediaClock-Architecture.md)** - Complete design specification
   - Detailed API design
   - Integration plan for each component
   - Migration strategy with phases
   - Testing strategy
   - Performance analysis
   - Future enhancements

2. **[MediaClock-Diagram.md](./MediaClock-Diagram.md)** - Visual architecture
   - Current vs. proposed architecture
   - Component interaction sequences
   - Timing flow visualization
   - Class structure diagrams
   - Migration phases timeline

## 🎯 Quick Start

### For Reviewers

1. Start with the **Executive Summary** in [MediaClock-Architecture.md](./MediaClock-Architecture.md#executive-summary)
2. Review the **Architecture Diagram** section or view [MediaClock-Diagram.md](./MediaClock-Diagram.md)
3. Examine the **MediaClock Class Design** section for API details
4. Check the **Integration Plan** for implementation steps

### For Implementers

1. Review the complete [Architecture document](./MediaClock-Architecture.md)
2. Follow the **Migration Strategy** phases in order
3. Reference the **Testing Strategy** for each phase
4. Use the diagrams in [MediaClock-Diagram.md](./MediaClock-Diagram.md) as reference

## 🔑 Key Design Decisions

### 1. QPC-Based Timing
- **Why**: Proven reliable in current audio pipeline
- **Benefits**: High precision, monotonic, low overhead
- **Alternative considered**: `std::chrono::steady_clock`

### 2. Thread Safety
- **Lock-free reads**: Atomic check + direct access (hot path)
- **Mutex-protected writes**: Rare initialization operations
- **Result**: No contention on common operations

### 3. Ownership Model
- **Phase 1**: MediaClock owned by `MP4SinkWriter`
- **Rationale**: Minimal changes to existing code
- **Future**: Can migrate to `ScreenRecorderImpl` later

### 4. Phased Migration
- **6 phases**: From standalone class to full integration
- **Backward compatible**: Old code works during migration
- **Low risk**: Each phase independently testable

## 📊 Benefits

### Immediate Benefits
- ✅ Eliminates timing drift between audio and video
- ✅ Single initialization point (no race conditions)
- ✅ Clearer ownership of timing state
- ✅ Simplified debugging

### Long-Term Benefits
- 🚀 Foundation for pause/resume feature
- 🚀 Supports independent source architecture
- 🚀 Enables multiple simultaneous recordings
- 🚀 Facilitates additional stream types

## 🏗️ Implementation Phases

```
Phase 1: Create MediaClock class (standalone)          [3-5 days]
Phase 2: Integrate with MP4SinkWriter                  [2-3 days]
Phase 3: Migrate AudioCaptureHandler                   [3-5 days]
Phase 4: Migrate FrameArrivedHandler                   [3-5 days]
Phase 5: Update ScreenRecorderImpl                     [2-4 days]
Phase 6: Remove old synchronization code               [2-4 days]
                                                        ───────────
Total estimated time:                                  [15-26 days]
```

Each phase includes:
- Implementation
- Unit tests
- Integration tests
- Documentation updates
- Code review

## 📈 Performance Impact

| Metric | Current | With MediaClock | Impact |
|--------|---------|-----------------|--------|
| CPU overhead | Baseline | +0.01% | Negligible |
| Memory per session | 40 bytes | 65 bytes | +25 bytes |
| Thread contention | None | None | No change |
| Timing precision | ~100ns | ~100ns | Identical |

**Conclusion**: No measurable performance impact

## 🧪 Testing Strategy

### Unit Tests (MediaClock class)
- Construction and initialization
- Start/Stop lifecycle
- Time query accuracy
- Thread safety
- Precision verification

### Integration Tests
- MP4SinkWriter integration
- Audio/video synchronization
- Start/stop cycles
- Error handling

### Manual Testing
- Short recordings (30s)
- Long recordings (10+ minutes)
- Toggle audio during recording
- Multiple start/stop cycles

## 🔮 Future Enhancements

### Pause/Resume Support
Add pause/resume capability to MediaClock, enabling:
- Recording pause without stopping capture
- Time accumulation during pause
- Seamless resume without drift

### Independent Source Architecture
Refactor to support:
- Multiple audio sources (loopback + microphone)
- Multiple video sources (screen + webcam)
- Flexible routing and mixing
- Multiple output formats

### Multiple Clock Support
Enable simultaneous multi-monitor recording:
- One clock per recording session
- Clock synchronization across sessions
- Correlated timestamps

## 📝 Review Checklist

Before approving implementation:

- [ ] **Architecture Review**
  - [ ] API design is clear and minimal
  - [ ] Ownership model is appropriate
  - [ ] Thread safety approach is sound
  
- [ ] **Integration Review**
  - [ ] Migration strategy minimizes risk
  - [ ] Backward compatibility maintained
  - [ ] Integration points clearly defined
  
- [ ] **Testing Review**
  - [ ] Unit test coverage is comprehensive
  - [ ] Integration test plan is adequate
  - [ ] Performance testing included
  
- [ ] **Documentation Review**
  - [ ] API documentation is complete
  - [ ] Architecture diagrams are clear
  - [ ] Migration guide is detailed

## 🤝 Contributing

This is a design document for review. Implementation will follow after approval.

### Review Process
1. Read both architecture and diagram documents
2. Provide feedback via PR comments
3. Suggest improvements or alternatives
4. Approve when satisfied

### Questions or Concerns?
- Open an issue with questions about the design
- Comment on specific sections in the PR
- Request clarification on any aspect

## 📖 Additional Resources

### Current Implementation References
- `src/CaptureInterop.Lib/AudioCaptureHandler.cpp` - Audio timing (QPC-based)
- `src/CaptureInterop.Lib/FrameArrivedHandler.cpp` - Video timing (system time)
- `src/CaptureInterop.Lib/MP4SinkWriter.cpp` - Current synchronization point
- `src/CaptureInterop.Lib/ScreenRecorderImpl.cpp` - Component lifecycle

### External Documentation
- [QueryPerformanceCounter (Microsoft Docs)](https://docs.microsoft.com/en-us/windows/win32/api/profileapi/nf-profileapi-queryperformancecounter)
- [Media Foundation Time Format (Microsoft Docs)](https://docs.microsoft.com/en-us/windows/win32/medfound/media-foundation-time)
- [Windows Graphics Capture API (Microsoft Docs)](https://docs.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture)

## 📅 Version History

| Version | Date | Description |
|---------|------|-------------|
| 1.0 | 2025-12-19 | Initial design document created |

## 📧 Contact

For questions or feedback about this design:
- Open an issue in the repository
- Comment on the PR
- Contact the project maintainers

---

**Status**: 📋 Design phase - awaiting review and approval

**Next Step**: Review and refine design based on feedback
