#include "TextureConversionExports.h"
#include <d3d11.h>
#include <wil/com.h>

extern "C"
{
    __declspec(dllexport) bool ConvertTextureToPixelBuffer(
        void* pTexture,
        void* pDevice,
        void* pContext,
        uint8_t* outBuffer,
        uint32_t bufferSize,
        uint32_t* outRowPitch)
    {
        if (!pTexture || !outBuffer || bufferSize == 0)
        {
            return false;
        }

        auto* texture = static_cast<ID3D11Texture2D*>(pTexture);
        
        // Get texture description
        D3D11_TEXTURE2D_DESC desc{};
        texture->GetDesc(&desc);

        // Validate format (must be BGRA8)
        if (desc.Format != DXGI_FORMAT_B8G8R8A8_UNORM)
        {
            return false;
        }

        // Calculate required buffer size
        const uint32_t rowPitch = desc.Width * 4;
        const uint32_t requiredSize = rowPitch * desc.Height;
        if (bufferSize < requiredSize)
        {
            return false;
        }

        // Get device and context
        ID3D11Device* device = nullptr;
        ID3D11DeviceContext* context = nullptr;
        wil::com_ptr<ID3D11Device> ownedDevice;
        wil::com_ptr<ID3D11DeviceContext> ownedContext;
        
        if (pDevice && pContext)
        {
            // Use provided device and context (borrowed references)
            device = static_cast<ID3D11Device*>(pDevice);
            context = static_cast<ID3D11DeviceContext*>(pContext);
        }
        else
        {
            // Get device from texture and take ownership
            texture->GetDevice(ownedDevice.put());
            if (!ownedDevice)
            {
                return false;
            }
            ownedDevice->GetImmediateContext(ownedContext.put());
            device = ownedDevice.get();
            context = ownedContext.get();
        }

        // Create staging texture
        D3D11_TEXTURE2D_DESC stagingDesc = desc;
        stagingDesc.Usage = D3D11_USAGE_STAGING;
        stagingDesc.BindFlags = 0;
        stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
        stagingDesc.MiscFlags = 0;

        wil::com_ptr<ID3D11Texture2D> stagingTexture;
        HRESULT hr = device->CreateTexture2D(&stagingDesc, nullptr, stagingTexture.put());
        if (FAILED(hr))
        {
            return false;
        }

        // Copy texture to staging
        context->CopyResource(stagingTexture.get(), texture);

        // Map the staging texture
        D3D11_MAPPED_SUBRESOURCE mapped{};
        hr = context->Map(stagingTexture.get(), 0, D3D11_MAP_READ, 0, &mapped);
        if (FAILED(hr))
        {
            return false;
        }

        // Copy pixel data row by row to handle different pitches
        for (uint32_t y = 0; y < desc.Height; y++)
        {
            const uint8_t* srcRow = static_cast<const uint8_t*>(mapped.pData) + (y * mapped.RowPitch);
            uint8_t* destRow = outBuffer + (y * rowPitch);
            memcpy(destRow, srcRow, rowPitch);
        }

        // Return row pitch
        if (outRowPitch)
        {
            *outRowPitch = rowPitch;
        }

        // Unmap the texture
        context->Unmap(stagingTexture.get(), 0);

        return true;
    }
}
