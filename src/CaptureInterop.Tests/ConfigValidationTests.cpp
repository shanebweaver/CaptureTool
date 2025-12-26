#include "pch.h"
#include "CppUnitTest.h"
#include "CaptureSessionConfig.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(ConfigValidationTests)
    {
    public:
        
        TEST_METHOD(Validate_ValidConfig_ReturnsOk)
        {
            // Arrange
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig config(hMonitor, L"C:\\test\\output.mp4", true, 30, 5'000'000, 128'000);
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsTrue(result.isValid, L"Configuration should be valid");
            Assert::AreEqual(size_t(0), result.errors.size(), L"Should have no errors");
        }
        
        TEST_METHOD(Validate_NullMonitor_ReturnsError)
        {
            // Arrange
            CaptureSessionConfig config(nullptr, L"C:\\test\\output.mp4");
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsFalse(result.isValid, L"Configuration should be invalid");
            Assert::IsTrue(result.errors.size() > 0, L"Should have at least one error");
            
            bool foundError = false;
            for (const auto& error : result.errors)
            {
                if (error.find("hMonitor") != std::string::npos)
                {
                    foundError = true;
                    break;
                }
            }
            Assert::IsTrue(foundError, L"Should have error about hMonitor");
        }
        
        TEST_METHOD(Validate_EmptyOutputPath_ReturnsError)
        {
            // Arrange
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig config(hMonitor, L"");
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsFalse(result.isValid, L"Configuration should be invalid");
            
            bool foundError = false;
            for (const auto& error : result.errors)
            {
                if (error.find("outputPath") != std::string::npos)
                {
                    foundError = true;
                    break;
                }
            }
            Assert::IsTrue(foundError, L"Should have error about outputPath");
        }
        
        TEST_METHOD(Validate_InvalidOutputPath_ReturnsError)
        {
            // Arrange
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig config(hMonitor, L"invalid<>path.mp4");
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsFalse(result.isValid, L"Configuration should be invalid");
            
            bool foundError = false;
            for (const auto& error : result.errors)
            {
                if (error.find("outputPath") != std::string::npos)
                {
                    foundError = true;
                    break;
                }
            }
            Assert::IsTrue(foundError, L"Should have error about invalid outputPath");
        }
        
        TEST_METHOD(Validate_FrameRateTooLow_ReturnsError)
        {
            // Arrange
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig config(hMonitor, L"C:\\test\\output.mp4", false, 0);  // frameRate = 0
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsFalse(result.isValid, L"Configuration should be invalid");
            
            bool foundError = false;
            for (const auto& error : result.errors)
            {
                if (error.find("frameRate") != std::string::npos)
                {
                    foundError = true;
                    break;
                }
            }
            Assert::IsTrue(foundError, L"Should have error about frameRate");
        }
        
        TEST_METHOD(Validate_FrameRateTooHigh_ReturnsError)
        {
            // Arrange
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig config(hMonitor, L"C:\\test\\output.mp4", false, 150);  // frameRate = 150
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsFalse(result.isValid, L"Configuration should be invalid");
            
            bool foundError = false;
            for (const auto& error : result.errors)
            {
                if (error.find("frameRate") != std::string::npos && error.find("120") != std::string::npos)
                {
                    foundError = true;
                    break;
                }
            }
            Assert::IsTrue(foundError, L"Should have error about frameRate being too high");
        }
        
        TEST_METHOD(Validate_LowFrameRate_ReturnsWarning)
        {
            // Arrange
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig config(hMonitor, L"C:\\test\\output.mp4", false, 10);  // frameRate = 10
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsTrue(result.isValid, L"Configuration should be valid");
            Assert::IsTrue(result.warnings.size() > 0, L"Should have at least one warning");
            
            bool foundWarning = false;
            for (const auto& warning : result.warnings)
            {
                if (warning.find("frameRate") != std::string::npos && warning.find("choppy") != std::string::npos)
                {
                    foundWarning = true;
                    break;
                }
            }
            Assert::IsTrue(foundWarning, L"Should have warning about low frameRate");
        }
        
        TEST_METHOD(Validate_VideoBitrateTooLow_ReturnsError)
        {
            // Arrange
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig config(hMonitor, L"C:\\test\\output.mp4", false, 30, 50'000);  // 50 kbps
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsFalse(result.isValid, L"Configuration should be invalid");
            
            bool foundError = false;
            for (const auto& error : result.errors)
            {
                if (error.find("videoBitrate") != std::string::npos)
                {
                    foundError = true;
                    break;
                }
            }
            Assert::IsTrue(foundError, L"Should have error about videoBitrate");
        }
        
        TEST_METHOD(Validate_VideoBitrateTooHigh_ReturnsError)
        {
            // Arrange
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig config(hMonitor, L"C:\\test\\output.mp4", false, 30, 100'000'000);  // 100 Mbps
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsFalse(result.isValid, L"Configuration should be invalid");
            
            bool foundError = false;
            for (const auto& error : result.errors)
            {
                if (error.find("videoBitrate") != std::string::npos)
                {
                    foundError = true;
                    break;
                }
            }
            Assert::IsTrue(foundError, L"Should have error about videoBitrate being too high");
        }
        
        TEST_METHOD(Validate_LowVideoBitrate_ReturnsWarning)
        {
            // Arrange
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig config(hMonitor, L"C:\\test\\output.mp4", false, 30, 500'000);  // 500 kbps
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsTrue(result.isValid, L"Configuration should be valid");
            Assert::IsTrue(result.warnings.size() > 0, L"Should have at least one warning");
            
            bool foundWarning = false;
            for (const auto& warning : result.warnings)
            {
                if (warning.find("videoBitrate") != std::string::npos && warning.find("quality") != std::string::npos)
                {
                    foundWarning = true;
                    break;
                }
            }
            Assert::IsTrue(foundWarning, L"Should have warning about low videoBitrate");
        }
        
        TEST_METHOD(Validate_AudioBitrateTooLow_WithAudioEnabled_ReturnsError)
        {
            // Arrange
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig config(hMonitor, L"C:\\test\\output.mp4", true, 30, 5'000'000, 16'000);  // 16 kbps
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsFalse(result.isValid, L"Configuration should be invalid");
            
            bool foundError = false;
            for (const auto& error : result.errors)
            {
                if (error.find("audioBitrate") != std::string::npos)
                {
                    foundError = true;
                    break;
                }
            }
            Assert::IsTrue(foundError, L"Should have error about audioBitrate");
        }
        
        TEST_METHOD(Validate_AudioBitrateTooHigh_WithAudioEnabled_ReturnsError)
        {
            // Arrange
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig config(hMonitor, L"C:\\test\\output.mp4", true, 30, 5'000'000, 500'000);  // 500 kbps
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsFalse(result.isValid, L"Configuration should be invalid");
            
            bool foundError = false;
            for (const auto& error : result.errors)
            {
                if (error.find("audioBitrate") != std::string::npos)
                {
                    foundError = true;
                    break;
                }
            }
            Assert::IsTrue(foundError, L"Should have error about audioBitrate being too high");
        }
        
        TEST_METHOD(Validate_AudioBitrateInvalid_WithAudioDisabled_NoError)
        {
            // Arrange - audio disabled with invalid bitrate
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig config(hMonitor, L"C:\\test\\output.mp4", false, 30, 5'000'000, 16'000);  // 16 kbps
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsTrue(result.isValid, L"Configuration should be valid when audio is disabled");
            
            // Should not have audio bitrate errors when audio is disabled
            bool foundAudioBitrateError = false;
            for (const auto& error : result.errors)
            {
                if (error.find("audioBitrate") != std::string::npos)
                {
                    foundAudioBitrateError = true;
                    break;
                }
            }
            Assert::IsFalse(foundAudioBitrateError, L"Should not have audioBitrate error when audio is disabled");
        }
        
        TEST_METHOD(Validate_MultipleErrors_ReturnsAllErrors)
        {
            // Arrange - config with multiple issues
            CaptureSessionConfig config(nullptr, L"", false, 0, 50'000, 16'000);
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsFalse(result.isValid, L"Configuration should be invalid");
            Assert::IsTrue(result.errors.size() >= 3, L"Should have multiple errors");
        }
        
        TEST_METHOD(IsValid_UsesValidateResult)
        {
            // Arrange
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig validConfig(hMonitor, L"C:\\test\\output.mp4");
            CaptureSessionConfig invalidConfig(nullptr, L"");
            
            // Act & Assert
            Assert::IsTrue(validConfig.IsValid(), L"Valid config should return true");
            Assert::IsFalse(invalidConfig.IsValid(), L"Invalid config should return false");
        }
        
        TEST_METHOD(Validate_PathWithoutExtension_ReturnsError)
        {
            // Arrange
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig config(hMonitor, L"C:\\test\\output");
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsFalse(result.isValid, L"Configuration should be invalid");
            
            bool foundError = false;
            for (const auto& error : result.errors)
            {
                if (error.find("outputPath") != std::string::npos)
                {
                    foundError = true;
                    break;
                }
            }
            Assert::IsTrue(foundError, L"Should have error about invalid outputPath");
        }
        
        TEST_METHOD(Validate_ValidEdgeCaseValues_ReturnsOk)
        {
            // Arrange - edge case but valid values
            HMONITOR hMonitor = reinterpret_cast<HMONITOR>(0x12345678);
            CaptureSessionConfig config(hMonitor, L"C:\\o.mp4", false, 15, 100'000, 32'000);
            
            // Act
            ConfigValidationResult result = config.Validate();
            
            // Assert
            Assert::IsTrue(result.isValid, L"Configuration with edge case valid values should be valid");
            Assert::AreEqual(size_t(0), result.errors.size(), L"Should have no errors");
        }
    };
}
