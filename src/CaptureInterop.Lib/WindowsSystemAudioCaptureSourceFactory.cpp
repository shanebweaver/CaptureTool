#include "pch.h"
#include "WindowsSystemAudioCaptureSourceFactory.h"
#include "WindowsSystemAudioCaptureSource.h"

std::unique_ptr<IAudioCaptureSource> WindowsSystemAudioCaptureSourceFactory::CreateAudioCaptureSource()
{
    return std::make_unique<WindowsSystemAudioCaptureSource>();
}
