#pragma once
#include <Windows.h>
#include <d3d11.h>
#include <d3d11_1.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mutex>

namespace CaptureInterop
{
    /// <summary>
    /// Utility class for converting D3D11 textures to Media Foundation samples.
    /// Handles format conversion (BGRA→NV12) using hardware video processor.
    /// Optimized for minimal CPU overhead and efficient GPU processing.
    /// </summary>
    class TextureConverter
    {
    public:
        TextureConverter();
        ~TextureConverter();

        /// <summary>
        /// Initialize the converter with D3D11 device.
        /// </summary>
        /// <param name="pDevice">D3D11 device for texture operations</param>
        /// <param name="width">Expected texture width</param>
        /// <param name="height">Expected texture height</param>
        HRESULT Initialize(ID3D11Device* pDevice, UINT32 width, UINT32 height);

        /// <summary>
        /// Convert D3D11 texture to Media Foundation sample.
        /// </summary>
        /// <param name="pTexture">Source texture (BGRA format)</param>
        /// <param name="timestamp">Timestamp for the sample (100-nanosecond units)</param>
        /// <param name="ppSample">Receives the converted Media Foundation sample</param>
        HRESULT ConvertTextureToSample(ID3D11Texture2D* pTexture, int64_t timestamp, IMFSample** ppSample);

        /// <summary>
        /// Handle resolution change.
        /// </summary>
        /// <param name="width">New width</param>
        /// <param name="height">New height</param>
        HRESULT UpdateResolution(UINT32 width, UINT32 height);

        /// <summary>
        /// Get performance statistics.
        /// </summary>
        double GetAverageConversionTimeMs() const;
        uint64_t GetConversionCount() const;

    private:
        // Initialization helpers
        HRESULT CreateVideoProcessor();
        HRESULT CreateStagingTextures();
        HRESULT CreateNV12Texture();

        // Conversion helpers
        HRESULT ConvertBGRAToNV12(ID3D11Texture2D* pSource, ID3D11Texture2D* pDest);
        HRESULT CopyTextureToMFSample(ID3D11Texture2D* pTexture, IMFSample* pSample);
        HRESULT CreateMFSampleFromTexture(ID3D11Texture2D* pTexture, int64_t timestamp, IMFSample** ppSample);

        // D3D11 resources
        ID3D11Device* m_pDevice;
        ID3D11DeviceContext* m_pContext;
        ID3D11VideoDevice* m_pVideoDevice;
        ID3D11VideoContext* m_pVideoContext;
        ID3D11VideoProcessor* m_pVideoProcessor;
        ID3D11VideoProcessorEnumerator* m_pVideoProcessorEnum;

        // Textures for conversion pipeline
        ID3D11Texture2D* m_pStagingTexture;   // For reading from GPU
        ID3D11Texture2D* m_pNV12Texture;      // NV12 format output
        ID3D11VideoProcessorInputView* m_pInputView;
        ID3D11VideoProcessorOutputView* m_pOutputView;

        // Configuration
        UINT32 m_width;
        UINT32 m_height;
        bool m_initialized;

        // Statistics
        uint64_t m_conversionCount;
        double m_totalConversionTimeMs;
        mutable std::mutex m_mutex;

        // Constants
        static const UINT32 MAX_RECENT_SAMPLES = 100;
    };
}
