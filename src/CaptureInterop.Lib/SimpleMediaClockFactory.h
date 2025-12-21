#pragma once
#include "pch.h"
#include "IMediaClockFactory.h"

/// <summary>
/// Factory implementation for creating SimpleMediaClock instances.
/// Creates simple audio-driven media clocks for A/V synchronization.
/// </summary>
class SimpleMediaClockFactory : public IMediaClockFactory
{
public:
    SimpleMediaClockFactory() = default;
    ~SimpleMediaClockFactory() override = default;

    // Delete copy and move operations
    SimpleMediaClockFactory(const SimpleMediaClockFactory&) = delete;
    SimpleMediaClockFactory& operator=(const SimpleMediaClockFactory&) = delete;
    SimpleMediaClockFactory(SimpleMediaClockFactory&&) = delete;
    SimpleMediaClockFactory& operator=(SimpleMediaClockFactory&&) = delete;

    /// <summary>
    /// Create a new SimpleMediaClock instance.
    /// </summary>
    /// <returns>A unique pointer to a new SimpleMediaClock.</returns>
    std::unique_ptr<IMediaClock> CreateClock() override;
};
