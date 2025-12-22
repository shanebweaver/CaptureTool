#pragma once
#include "IMediaClock.h"
#include <memory>

/// <summary>
/// Factory interface for creating media clock instances.
/// Provides abstraction for clock creation to enable dependency injection and testing.
/// </summary>
class IMediaClockFactory
{
public:
    virtual ~IMediaClockFactory() = default;

    /// <summary>
    /// Create a new media clock instance.
    /// </summary>
    /// <returns>A unique pointer to a new IMediaClock implementation.</returns>
    virtual std::unique_ptr<IMediaClock> CreateClock() = 0;
};
