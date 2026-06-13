#pragma once

#include "MonitorHdrInfo.h"
#include <Windows.h>

class IMonitorHdrDetector
{
public:
    virtual ~IMonitorHdrDetector() = default;

    virtual MonitorHdrInfo Detect(HMONITOR monitor) = 0;
};
