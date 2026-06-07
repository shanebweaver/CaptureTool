#pragma once

#include "V2/Core/MediaTypes.h"

namespace CaptureInterop::V2::Desktop
{
    struct DesktopColorMetadata
    {
        ColorPrimaries colorPrimaries{ ColorPrimaries::Unknown };
        TransferFunction transferFunction{ TransferFunction::Unknown };
        ColorRange range{ ColorRange::Unknown };
    };

    struct DesktopColorDiagnostics
    {
        HdrPolicy hdrPolicy{ HdrPolicy::Auto };
        ColorPrimaries colorPrimaries{ ColorPrimaries::Unknown };
        TransferFunction transferFunction{ TransferFunction::Unknown };
        ColorRange colorRange{ ColorRange::Unknown };
        bool hdrInputDetected{ false };
        bool wideColorInputDetected{ false };
        bool hdrToneMappingPending{ false };
    };

    [[nodiscard]] inline VideoMediaType ApplyDesktopColorMetadata(
        VideoMediaType mediaType,
        DesktopColorMetadata metadata) noexcept
    {
        if (metadata.colorPrimaries != ColorPrimaries::Unknown)
        {
            mediaType.colorPrimaries = metadata.colorPrimaries;
        }

        if (metadata.transferFunction != TransferFunction::Unknown)
        {
            mediaType.transferFunction = metadata.transferFunction;
        }

        if (metadata.range != ColorRange::Unknown)
        {
            mediaType.range = metadata.range;
        }

        return mediaType;
    }

    [[nodiscard]] inline bool IsHdrTransferFunction(TransferFunction transferFunction) noexcept
    {
        return transferFunction == TransferFunction::St2084Pq
            || transferFunction == TransferFunction::Hlg;
    }

    [[nodiscard]] inline bool IsWideColorPrimaries(ColorPrimaries colorPrimaries) noexcept
    {
        return colorPrimaries == ColorPrimaries::Rec2020;
    }

    [[nodiscard]] inline bool IsHdrPixelFormat(VideoPixelFormat pixelFormat) noexcept
    {
        return pixelFormat == VideoPixelFormat::Rgba16Float
            || pixelFormat == VideoPixelFormat::P010;
    }

    [[nodiscard]] inline DesktopColorDiagnostics BuildDesktopColorDiagnostics(
        const VideoMediaType& mediaType,
        HdrPolicy hdrPolicy = HdrPolicy::Auto) noexcept
    {
        DesktopColorDiagnostics diagnostics;
        diagnostics.hdrPolicy = hdrPolicy;
        diagnostics.colorPrimaries = mediaType.colorPrimaries;
        diagnostics.transferFunction = mediaType.transferFunction;
        diagnostics.colorRange = mediaType.range;
        diagnostics.hdrInputDetected = IsHdrTransferFunction(mediaType.transferFunction)
            || IsHdrPixelFormat(mediaType.pixelFormat);
        diagnostics.wideColorInputDetected = IsWideColorPrimaries(mediaType.colorPrimaries);

        // Auto is currently a safe placeholder: detect HDR/wide color and report
        // that downstream tone mapping is still required, but do not transform pixels.
        diagnostics.hdrToneMappingPending =
            hdrPolicy == HdrPolicy::Auto
            && (diagnostics.hdrInputDetected || diagnostics.wideColorInputDetected);
        return diagnostics;
    }
}
