#include "pch.h"
#include "MediaFoundationLifecycleManagerFactory.h"
#include "MediaFoundationLifecycleManager.h"

std::unique_ptr<IMediaFoundationLifecycleManager> MediaFoundationLifecycleManagerFactory::CreateLifecycleManager()
{
    return std::make_unique<MediaFoundationLifecycleManager>();
}
