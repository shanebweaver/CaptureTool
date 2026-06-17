#include "pch.h"
#include "WindowsLocalAudioCaptureSourceFactory.h"
#include "WindowsLocalAudioCaptureSource.h"

std::unique_ptr<IAudioCaptureSource> WindowsLocalAudioCaptureSourceFactory::CreateAudioCaptureSource(IMediaClockReader* clockReader, const std::wstring& sourceId)
{
    return std::make_unique<WindowsLocalAudioCaptureSource>(clockReader, sourceId);
}
