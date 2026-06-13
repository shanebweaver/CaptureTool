#include "pch.h"
#include "WindowsMonitorHdrDetectorFactory.h"
#include "WindowsMonitorHdrDetector.h"

std::unique_ptr<IMonitorHdrDetector> WindowsMonitorHdrDetectorFactory::CreateMonitorHdrDetector()
{
    return std::make_unique<WindowsMonitorHdrDetector>();
}
