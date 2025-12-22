#include "pch.h"
#include "WindowsLocalAudioCaptureSourceFactory.h"
#include "WindowsLocalAudioCaptureSource.h"

std::unique_ptr<IAudioCaptureSource> WindowsLocalAudioCaptureSourceFactory::CreateAudioCaptureSource(IMediaClockReader* clockReader)
{
    return std::make_unique<WindowsLocalAudioCaptureSource>(clockReader);
}
