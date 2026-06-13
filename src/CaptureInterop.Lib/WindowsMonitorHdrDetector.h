#pragma once

#include "IMonitorHdrDetector.h"

class WindowsMonitorHdrDetector final : public IMonitorHdrDetector
{
public:
    MonitorHdrInfo Detect(HMONITOR monitor) override;
};
