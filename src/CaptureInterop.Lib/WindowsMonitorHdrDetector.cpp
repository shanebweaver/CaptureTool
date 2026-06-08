#include "pch.h"
#include "WindowsMonitorHdrDetector.h"
#include "MonitorHdrColorSpaceMapper.h"

#include <dxgi1_6.h>

MonitorHdrInfo WindowsMonitorHdrDetector::Detect(HMONITOR monitor)
{
    if (monitor == nullptr)
    {
        return MonitorHdrInfo::Failed(MonitorHdrFallbackReason::OutputNotFound, E_INVALIDARG);
    }

    wil::com_ptr<IDXGIFactory1> factory;
    HRESULT hr = CreateDXGIFactory1(IID_PPV_ARGS(factory.put()));
    if (FAILED(hr))
    {
        return MonitorHdrInfo::Failed(MonitorHdrFallbackReason::DetectorUnavailable, hr);
    }

    for (UINT adapterIndex = 0;; ++adapterIndex)
    {
        wil::com_ptr<IDXGIAdapter1> adapter;
        hr = factory->EnumAdapters1(adapterIndex, adapter.put());
        if (hr == DXGI_ERROR_NOT_FOUND)
        {
            break;
        }
        if (FAILED(hr))
        {
            return MonitorHdrInfo::Failed(MonitorHdrFallbackReason::QueryFailed, hr);
        }

        for (UINT outputIndex = 0;; ++outputIndex)
        {
            wil::com_ptr<IDXGIOutput> output;
            hr = adapter->EnumOutputs(outputIndex, output.put());
            if (hr == DXGI_ERROR_NOT_FOUND)
            {
                break;
            }
            if (FAILED(hr))
            {
                return MonitorHdrInfo::Failed(MonitorHdrFallbackReason::QueryFailed, hr);
            }

            DXGI_OUTPUT_DESC outputDesc{};
            hr = output->GetDesc(&outputDesc);
            if (FAILED(hr))
            {
                return MonitorHdrInfo::Failed(MonitorHdrFallbackReason::QueryFailed, hr);
            }

            if (outputDesc.Monitor != monitor)
            {
                continue;
            }

            wil::com_ptr<IDXGIOutput6> output6;
            hr = output->QueryInterface(IID_PPV_ARGS(output6.put()));
            if (FAILED(hr))
            {
                return MonitorHdrInfo::Failed(MonitorHdrFallbackReason::QueryFailed, hr);
            }

            DXGI_OUTPUT_DESC1 outputDesc1{};
            hr = output6->GetDesc1(&outputDesc1);
            if (FAILED(hr))
            {
                return MonitorHdrInfo::Failed(MonitorHdrFallbackReason::QueryFailed, hr);
            }

            return MonitorHdrColorSpaceMapper::FromColorSpace(outputDesc1.ColorSpace);
        }
    }

    return MonitorHdrInfo::Failed(MonitorHdrFallbackReason::OutputNotFound, HRESULT_FROM_WIN32(ERROR_NOT_FOUND));
}
