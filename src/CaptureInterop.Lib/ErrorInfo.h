#pragma once
#include <string>
#include <Windows.h>
#include <comdef.h>
#include <sstream>
#include <iomanip>

/// <summary>
/// Structured error information providing richer diagnostics than HRESULT alone.
/// Combines HRESULT codes with human-readable messages and context information.
/// </summary>
struct ErrorInfo
{
    HRESULT hr;
    std::string message;
    std::string context;  // e.g., "InitializeSinkWriter", "StartAudioCapture"
    
    /// <summary>
    /// Create a success error info (no error).
    /// </summary>
    static ErrorInfo Success() 
    { 
        return ErrorInfo{ S_OK, "", "" }; 
    }
    
    /// <summary>
    /// Create error info from HRESULT with context.
    /// Automatically generates a human-readable message from the HRESULT.
    /// </summary>
    static ErrorInfo FromHResult(HRESULT hr, const char* context)
    {
        return ErrorInfo{ hr, HResultToString(hr), context };
    }
    
    /// <summary>
    /// Create error info with custom message and context.
    /// </summary>
    static ErrorInfo FromMessage(HRESULT hr, const std::string& message, const char* context)
    {
        return ErrorInfo{ hr, message, context };
    }
    
    /// <summary>
    /// Check if this represents a successful operation.
    /// </summary>
    bool IsSuccess() const { return SUCCEEDED(hr); }
    
    /// <summary>
    /// Get a formatted error string combining all information.
    /// </summary>
    std::string ToString() const
    {
        if (IsSuccess())
        {
            return "Success";
        }
        
        std::string result;
        if (!context.empty())
        {
            result += context + ": ";
        }
        if (!message.empty())
        {
            result += message;
        }
        else
        {
            result += "Error 0x" + ToHexString(hr);
        }
        return result;
    }
    
private:
    /// <summary>
    /// Convert HRESULT to human-readable string.
    /// </summary>
    static std::string HResultToString(HRESULT hr)
    {
        if (SUCCEEDED(hr))
        {
            return "Success";
        }
        
        // Try to get system error message
        _com_error err(hr);
        LPCTSTR errMsg = err.ErrorMessage();
        if (errMsg)
        {
            // Convert TCHAR to string
            #ifdef UNICODE
            int size = WideCharToMultiByte(CP_UTF8, 0, errMsg, -1, nullptr, 0, nullptr, nullptr);
            if (size > 0)
            {
                std::string result(size - 1, 0);
                WideCharToMultiByte(CP_UTF8, 0, errMsg, -1, &result[0], size, nullptr, nullptr);
                return result;
            }
            #else
            return std::string(errMsg);
            #endif
        }
        
        // Fallback to hex representation
        return "HRESULT 0x" + ToHexString(hr);
    }
    
    /// <summary>
    /// Convert HRESULT to hexadecimal string.
    /// </summary>
    static std::string ToHexString(HRESULT hr)
    {
        std::ostringstream stream;
        stream << std::uppercase << std::hex << std::setw(8) << std::setfill('0') 
               << static_cast<unsigned int>(hr);
        return stream.str();
    }
};
