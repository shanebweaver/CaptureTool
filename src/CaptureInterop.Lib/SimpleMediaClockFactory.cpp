#include "pch.h"
#include "SimpleMediaClockFactory.h"
#include "SimpleMediaClock.h"

std::unique_ptr<IMediaClock> SimpleMediaClockFactory::CreateClock()
{
    return std::make_unique<SimpleMediaClock>();
}
