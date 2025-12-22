# Callback Flow Diagram

## High-Level Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         User Application (C#)                        │
│                                                                      │
│  var recorder = new WindowsScreenRecorder();                        │
│  recorder.SetVideoFrameCallback(OnVideoFrame);                      │
│  recorder.SetAudioSampleCallback(OnAudioSample);                    │
│  recorder.StartRecording(monitor, "output.mp4", true);              │
│                                                                      │
└──────────────────────────────┬──────────────────────────────────────┘
                               │
                               │ P/Invoke
                               ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      Native DLL (CaptureInterop.dll)                │
│                                                                      │
│  SetVideoFrameCallback(callback_ptr)   ────────────────────┐        │
│  SetAudioSampleCallback(callback_ptr)  ────────────┐       │        │
│  TryStartRecording(...)                            │       │        │
│                                                     │       │        │
└─────────────────────────────────────────────────────┼───────┼────────┘
                                                      │       │
                                                      │       │
                      ┌───────────────────────────────┘       │
                      │                                       │
                      ▼                                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│                   ScreenRecorderImpl (C++)                           │
│                                                                       │
│  m_videoFrameCallback = callback_ptr;                                │
│  m_audioSampleCallback = callback_ptr;                               │
│  m_captureSession = factory->CreateSession(config_with_callbacks);   │
│                                                                       │
└────────────────────────────┬─────────────────────────────────────────┘
                             │
                             │ Create session with callbacks
                             ▼
┌──────────────────────────────────────────────────────────────────────┐
│              WindowsGraphicsCaptureSession (C++)                     │
│                                                                       │
│  SetAudioSampleReadyCallback([this](args) {                          │
│      m_sinkWriter->WriteAudioSample(args);  // Write to file         │
│      if (m_config.audioSampleCallback)                               │
│          m_config.audioSampleCallback(&data); // Call managed        │
│  });                                                                  │
│                                                                       │
│  SetVideoFrameReadyCallback([this](args) {                           │
│      m_sinkWriter->WriteFrame(args);  // Write to file               │
│      if (m_config.videoFrameCallback)                                │
│          m_config.videoFrameCallback(&data); // Call managed         │
│  });                                                                  │
│                                                                       │
└────┬──────────────────────────────────────────────────────────┬──────┘
     │                                                           │
     │ Set callbacks on sources                                 │
     │                                                           │
     ▼                                                           ▼
┌─────────────────────┐                           ┌─────────────────────┐
│ AudioCaptureHandler │                           │ FrameArrivedHandler │
│                     │                           │                     │
│ WASAPI Audio Loop   │                           │ Graphics Capture    │
│                     │                           │ Frame Event         │
└──────┬──────────────┘                           └──────┬──────────────┘
       │                                                 │
       │ Audio samples ready                            │ Video frames ready
       │                                                 │
       ▼                                                 ▼
┌──────────────────────────────────────────────────────────────────────┐
│                    Back to WindowsGraphicsCaptureSession             │
│                                                                       │
│  AudioSampleReadyCallback invoked ──▶ Write to file ──▶ Call managed│
│  VideoFrameReadyCallback invoked ───▶ Write to file ──▶ Call managed│
│                                                                       │
└───────────────────────────────┬──────────────────────────────────────┘
                                │
                                │ Call function pointer
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────────┐
│                    Managed Callback (C#)                             │
│                                                                       │
│  void OnVideoFrameReceived(ref VideoFrameData frameData)            │
│  {                                                                    │
│      // Called on native thread!                                     │
│      // Process frame, forward to pipeline, etc.                     │
│  }                                                                    │
│                                                                       │
│  void OnAudioSampleReceived(ref AudioSampleData sampleData)         │
│  {                                                                    │
│      // Called on native thread!                                     │
│      // Process audio, forward to pipeline, etc.                     │
│  }                                                                    │
└──────────────────────────────────────────────────────────────────────┘
```

## Detailed Sequence Diagram

```
User App           CaptureInterop    ScreenRecorderImpl    CaptureSession    Capture Sources
   │                     │                   │                    │                │
   │──SetVideoCallback─▶│                   │                    │                │
   │                     │──Store callback─▶│                    │                │
   │                     │                   │                    │                │
   │──SetAudioCallback─▶│                   │                    │                │
   │                     │──Store callback─▶│                    │                │
   │                     │                   │                    │                │
   │──StartRecording───▶│                   │                    │                │
   │                     │──StartRecording─▶│                    │                │
   │                     │                   │──CreateSession───▶│                │
   │                     │                   │  (with callbacks)  │                │
   │                     │                   │                    │──Initialize──▶│
   │                     │                   │                    │                │
   │                     │                   │                    │──Start────────▶│
   │                     │                   │                    │                │
   │                     │                   │                    │                │
   │                     │                   │                    │◀─FrameReady───│
   │                     │                   │                    │                │
   │                     │                   │       ┌────────────┴────────────┐  │
   │                     │                   │       │ Write to MP4 sink       │  │
   │                     │                   │       │ Invoke video callback   │  │
   │                     │                   │       └────────────┬────────────┘  │
   │                     │                   │                    │                │
   │◀────────────────────────────────────────────────VideoCallback────────────────│
   │  OnVideoFrame()     │                   │                    │                │
   │──────────────────▶│                   │                    │                │
   │  (process frame)    │                   │                    │                │
   │                     │                   │                    │                │
   │                     │                   │                    │◀SampleReady───│
   │                     │                   │                    │                │
   │                     │                   │       ┌────────────┴────────────┐  │
   │                     │                   │       │ Write to MP4 sink       │  │
   │                     │                   │       │ Invoke audio callback   │  │
   │                     │                   │       └────────────┬────────────┘  │
   │                     │                   │                    │                │
   │◀────────────────────────────────────────────────AudioCallback────────────────│
   │  OnAudioSample()    │                   │                    │                │
   │──────────────────▶│                   │                    │                │
   │  (process audio)    │                   │                    │                │
   │                     │                   │                    │                │
   ...                  ...                 ...                  ...              ...
   │                     │                   │                    │                │
   │──StopRecording────▶│                   │                    │                │
   │                     │──StopRecording──▶│                    │                │
   │                     │                   │──Stop─────────────▶│                │
   │                     │                   │                    │──Stop─────────▶│
   │                     │                   │                    │                │
   │──ClearCallbacks───▶│                   │                    │                │
   │                     │──Clear──────────▶│                    │                │
   │                     │                   │                    │                │
```

## Key Points

1. **Callback Registration**: Callbacks are registered before starting recording
2. **Configuration Flow**: Callbacks flow through config to the capture session
3. **Dual Purpose**: Frames/samples are BOTH written to file AND forwarded to callbacks
4. **Native Threads**: Callbacks are invoked on native capture threads, not UI thread
5. **Lifetime**: Managed delegates must be kept alive (GC prevention)

## Thread Context

```
┌─────────────────────────────────────────────────────────┐
│ FrameArrivedHandler Background Thread                  │
│  - Processes video frames from queue                    │
│  - Calls videoFrameCallback on THIS thread              │
│  - NOT synchronized with UI thread                      │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ AudioCaptureHandler WASAPI Thread                       │
│  - High priority thread for audio capture               │
│  - Calls audioSampleCallback on THIS thread             │
│  - NOT synchronized with UI thread                      │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ UI Thread (WinUI)                                       │
│  - User interaction, rendering                          │
│  - Must marshal from callback threads                   │
│  - Use Dispatcher.InvokeAsync()                         │
└─────────────────────────────────────────────────────────┘
```
