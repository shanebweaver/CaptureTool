#pragma once

#include "IMonitorHdrDetectorFactory.h"

class WindowsMonitorHdrDetectorFactory final : public IMonitorHdrDetectorFactory
{
public:
    std::unique_ptr<IMonitorHdrDetector> CreateMonitorHdrDetector() override;
};
