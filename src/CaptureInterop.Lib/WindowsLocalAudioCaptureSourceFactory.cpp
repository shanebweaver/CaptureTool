#include "pch.h"
#include "WindowsLocalAudioCaptureSourceFactory.h"
#include "WindowsLocalAudioCaptureSource.h"

std::unique_ptr<IAudioCaptureSource> WindowsLocalAudioCaptureSourceFactory::CreateAudioCaptureSource()
{
    return std::make_unique<WindowsLocalAudioCaptureSource>();
}
