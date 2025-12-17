#pragma once

extern "C"
{    
	__declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio = false);
	__declspec(dllexport) void TryPauseRecording();
	__declspec(dllexport) void TryResumeRecording();
	__declspec(dllexport) void TryStopRecording();
	__declspec(dllexport) void TryToggleAudioCapture(bool enabled);
}