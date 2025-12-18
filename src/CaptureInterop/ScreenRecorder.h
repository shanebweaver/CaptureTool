#pragma once
#include "AudioDeviceEnumerator.h"
#include "SourceManager.h"

extern "C"
{    
	// Recording API - supports both desktop audio and microphone
	// For 3-parameter version (legacy), pass false for captureMicrophone
	__declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureDesktopAudio, bool captureMicrophone);
	
	__declspec(dllexport) void TryPauseRecording();
	__declspec(dllexport) void TryResumeRecording();
	__declspec(dllexport) void TryStopRecording();
	__declspec(dllexport) void TryToggleAudioCapture(bool enabled);
	
	// Audio device enumeration
	__declspec(dllexport) int EnumerateAudioCaptureDevices(AudioDeviceInfo** devices);
	__declspec(dllexport) int EnumerateAudioRenderDevices(AudioDeviceInfo** devices);
	__declspec(dllexport) void FreeAudioDeviceInfo(AudioDeviceInfo* devices);
	
	// Source management exports
	__declspec(dllexport) SourceHandle RegisterVideoSource(void* sourcePtr);
	__declspec(dllexport) SourceHandle RegisterAudioSource(void* sourcePtr);
	__declspec(dllexport) void UnregisterSource(SourceHandle handle);
	__declspec(dllexport) bool StartAllSources();
	__declspec(dllexport) void StopAllSources();
	__declspec(dllexport) int GetSourceCount();
	
	// Audio routing configuration exports
	__declspec(dllexport) void SetAudioSourceTrack(uint64_t sourceHandle, int trackIndex);
	__declspec(dllexport) int GetAudioSourceTrack(uint64_t sourceHandle);
	__declspec(dllexport) void SetAudioSourceVolume(uint64_t sourceHandle, float volume);
	__declspec(dllexport) float GetAudioSourceVolume(uint64_t sourceHandle);
	__declspec(dllexport) void SetAudioSourceMuted(uint64_t sourceHandle, bool muted);
	__declspec(dllexport) bool GetAudioSourceMuted(uint64_t sourceHandle);
	__declspec(dllexport) void SetAudioTrackName(int trackIndex, const wchar_t* name);
	__declspec(dllexport) void SetAudioMixingMode(bool mixedMode);
	__declspec(dllexport) bool GetAudioMixingMode();
	
	// Phase 4: Encoder pipeline configuration exports
	__declspec(dllexport) void UseEncoderPipeline(bool enable);
	__declspec(dllexport) bool IsEncoderPipelineEnabled();
	__declspec(dllexport) void SetVideoEncoderPreset(int preset);
	__declspec(dllexport) int GetVideoEncoderPreset();
	__declspec(dllexport) void SetAudioEncoderQuality(int quality);
	__declspec(dllexport) int GetAudioEncoderQuality();
	__declspec(dllexport) void EnableHardwareEncoding(bool enable);
	__declspec(dllexport) bool IsHardwareEncodingEnabled();
}