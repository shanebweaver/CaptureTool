#pragma once
#include "IMP4SinkWriter.h"
#include <memory>

// Forward declarations
struct ID3D11Device;

/// <summary>
/// Factory interface for creating MP4 sink writer instances.
/// Provides abstraction for sink writer creation to enable dependency injection and testing.
/// </summary>
class IMP4SinkWriterFactory
{
public:
    virtual ~IMP4SinkWriterFactory() = default;

    /// <summary>
    /// Create a new MP4 sink writer instance.
    /// </summary>
    /// <returns>A unique pointer to a new IMP4SinkWriter implementation.</returns>
    virtual std::unique_ptr<IMP4SinkWriter> CreateSinkWriter() = 0;
};
