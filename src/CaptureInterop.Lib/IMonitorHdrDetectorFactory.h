#pragma once

#include "IMonitorHdrDetector.h"
#include <memory>

class IMonitorHdrDetectorFactory
{
public:
    virtual ~IMonitorHdrDetectorFactory() = default;

    virtual std::unique_ptr<IMonitorHdrDetector> CreateMonitorHdrDetector() = 0;
};
