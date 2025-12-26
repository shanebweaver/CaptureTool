#include "pch.h"
#include "CppUnitTest.h"
#include "ErrorInfo.h"
#include "Result.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(ErrorInfoTests)
    {
    public:
        
        TEST_METHOD(ErrorInfo_Success_IsSuccess)
        {
            // Arrange & Act
            auto errorInfo = ErrorInfo::Success();
            
            // Assert
            Assert::IsTrue(errorInfo.IsSuccess(), L"Success should return true for IsSuccess()");
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(errorInfo.hr), L"HRESULT should be S_OK");
        }
        
        TEST_METHOD(ErrorInfo_FromHResult_CreatesErrorInfo)
        {
            // Arrange & Act
            auto errorInfo = ErrorInfo::FromHResult(E_FAIL, "TestContext");
            
            // Assert
            Assert::IsFalse(errorInfo.IsSuccess(), L"E_FAIL should not be success");
            Assert::AreEqual(static_cast<long>(E_FAIL), static_cast<long>(errorInfo.hr), L"HRESULT should be E_FAIL");
            Assert::AreEqual("TestContext", errorInfo.context.c_str(), L"Context should match");
            Assert::IsFalse(errorInfo.message.empty(), L"Message should not be empty");
        }
        
        TEST_METHOD(ErrorInfo_FromMessage_CreatesWithCustomMessage)
        {
            // Arrange & Act
            auto errorInfo = ErrorInfo::FromMessage(E_INVALIDARG, "Custom error message", "CustomContext");
            
            // Assert
            Assert::IsFalse(errorInfo.IsSuccess(), L"Should not be success");
            Assert::AreEqual(static_cast<long>(E_INVALIDARG), static_cast<long>(errorInfo.hr), L"HRESULT should be E_INVALIDARG");
            Assert::AreEqual("Custom error message", errorInfo.message.c_str(), L"Message should match");
            Assert::AreEqual("CustomContext", errorInfo.context.c_str(), L"Context should match");
        }
        
        TEST_METHOD(ErrorInfo_ToString_FormatsCorrectly)
        {
            // Arrange
            auto errorInfo = ErrorInfo::FromMessage(E_FAIL, "Test message", "TestContext");
            
            // Act
            std::string result = errorInfo.ToString();
            
            // Assert
            Assert::IsTrue(result.find("TestContext") != std::string::npos, L"ToString should contain context");
            Assert::IsTrue(result.find("Test message") != std::string::npos, L"ToString should contain message");
        }
        
        TEST_METHOD(ErrorInfo_Success_ToStringReturnsSuccess)
        {
            // Arrange
            auto errorInfo = ErrorInfo::Success();
            
            // Act
            std::string result = errorInfo.ToString();
            
            // Assert
            Assert::AreEqual("Success", result.c_str(), L"Success ToString should return 'Success'");
        }
    };

    TEST_CLASS(ResultTests)
    {
    public:
        
        TEST_METHOD(Result_Ok_IsOk)
        {
            // Arrange & Act
            auto result = Result<int>::Ok(42);
            
            // Assert
            Assert::IsTrue(result.IsOk(), L"Ok result should be ok");
            Assert::IsFalse(result.IsError(), L"Ok result should not be error");
            Assert::AreEqual(42, result.Value(), L"Value should be 42");
        }
        
        TEST_METHOD(Result_Error_IsError)
        {
            // Arrange
            auto errorInfo = ErrorInfo::FromHResult(E_FAIL, "TestError");
            
            // Act
            auto result = Result<int>::Error(errorInfo);
            
            // Assert
            Assert::IsFalse(result.IsOk(), L"Error result should not be ok");
            Assert::IsTrue(result.IsError(), L"Error result should be error");
            Assert::AreEqual(static_cast<long>(E_FAIL), static_cast<long>(result.Error().hr), L"Error HRESULT should match");
        }
        
        TEST_METHOD(Result_ValueOr_ReturnsValueWhenOk)
        {
            // Arrange
            auto result = Result<int>::Ok(42);
            
            // Act
            int value = result.ValueOr(0);
            
            // Assert
            Assert::AreEqual(42, value, L"ValueOr should return actual value when ok");
        }
        
        TEST_METHOD(Result_ValueOr_ReturnsDefaultWhenError)
        {
            // Arrange
            auto result = Result<int>::Error(ErrorInfo::FromHResult(E_FAIL, "Test"));
            
            // Act
            int value = result.ValueOr(99);
            
            // Assert
            Assert::AreEqual(99, value, L"ValueOr should return default value when error");
        }
        
        TEST_METHOD(Result_Match_CallsOkCallback)
        {
            // Arrange
            auto result = Result<int>::Ok(42);
            bool okCalled = false;
            bool errorCalled = false;
            
            // Act
            result.Match(
                [&](int value) { okCalled = true; return value * 2; },
                [&](const ErrorInfo&) { errorCalled = true; return 0; }
            );
            
            // Assert
            Assert::IsTrue(okCalled, L"Ok callback should be called");
            Assert::IsFalse(errorCalled, L"Error callback should not be called");
        }
        
        TEST_METHOD(Result_Match_CallsErrorCallback)
        {
            // Arrange
            auto result = Result<int>::Error(ErrorInfo::FromHResult(E_FAIL, "Test"));
            bool okCalled = false;
            bool errorCalled = false;
            
            // Act
            result.Match(
                [&](int value) { okCalled = true; return value * 2; },
                [&](const ErrorInfo&) { errorCalled = true; return 0; }
            );
            
            // Assert
            Assert::IsFalse(okCalled, L"Ok callback should not be called");
            Assert::IsTrue(errorCalled, L"Error callback should be called");
        }
        
        TEST_METHOD(ResultVoid_Ok_IsOk)
        {
            // Arrange & Act
            auto result = Result<void>::Ok();
            
            // Assert
            Assert::IsTrue(result.IsOk(), L"Ok result should be ok");
            Assert::IsFalse(result.IsError(), L"Ok result should not be error");
        }
        
        TEST_METHOD(ResultVoid_Error_IsError)
        {
            // Arrange
            auto errorInfo = ErrorInfo::FromHResult(E_ACCESSDENIED, "AccessTest");
            
            // Act
            auto result = Result<void>::Error(errorInfo);
            
            // Assert
            Assert::IsFalse(result.IsOk(), L"Error result should not be ok");
            Assert::IsTrue(result.IsError(), L"Error result should be error");
            Assert::AreEqual(static_cast<long>(E_ACCESSDENIED), static_cast<long>(result.Error().hr), 
                           L"Error HRESULT should match");
        }
        
        TEST_METHOD(Result_String_WorksWithStrings)
        {
            // Arrange & Act
            auto result = Result<std::string>::Ok(std::string("Hello"));
            
            // Assert
            Assert::IsTrue(result.IsOk(), L"Result should be ok");
            Assert::AreEqual("Hello", result.Value().c_str(), L"String value should match");
        }
        
        TEST_METHOD(Result_Pointer_WorksWithPointers)
        {
            // Arrange
            int value = 42;
            int* ptr = &value;
            
            // Act
            auto result = Result<int*>::Ok(ptr);
            
            // Assert
            Assert::IsTrue(result.IsOk(), L"Result should be ok");
            Assert::AreEqual(ptr, result.Value(), L"Pointer value should match");
            Assert::AreEqual(42, *result.Value(), L"Dereferenced value should be 42");
        }
    };

    // Integration tests showing usage patterns
    TEST_CLASS(ResultUsageTests)
    {
    public:
        
        // Helper function that returns Result<int>
        Result<int> DivideNumbers(int numerator, int denominator)
        {
            if (denominator == 0)
            {
                return Result<int>::Error(
                    ErrorInfo::FromMessage(E_INVALIDARG, "Division by zero", "DivideNumbers"));
            }
            return Result<int>::Ok(numerator / denominator);
        }
        
        // Helper function that returns Result<void>
        Result<void> ValidateInput(int value)
        {
            if (value < 0)
            {
                return Result<void>::Error(
                    ErrorInfo::FromMessage(E_INVALIDARG, "Value must be non-negative", "ValidateInput"));
            }
            return Result<void>::Ok();
        }
        
        TEST_METHOD(UsagePattern_SuccessfulOperation)
        {
            // Act
            auto result = DivideNumbers(10, 2);
            
            // Assert
            Assert::IsTrue(result.IsOk(), L"Division should succeed");
            Assert::AreEqual(5, result.Value(), L"Result should be 5");
        }
        
        TEST_METHOD(UsagePattern_ErrorHandling)
        {
            // Act
            auto result = DivideNumbers(10, 0);
            
            // Assert
            Assert::IsTrue(result.IsError(), L"Division by zero should fail");
            Assert::IsTrue(result.Error().message.find("Division by zero") != std::string::npos,
                         L"Error message should mention division by zero");
        }
        
        TEST_METHOD(UsagePattern_VoidSuccessful)
        {
            // Act
            auto result = ValidateInput(5);
            
            // Assert
            Assert::IsTrue(result.IsOk(), L"Validation should succeed for positive value");
        }
        
        TEST_METHOD(UsagePattern_VoidError)
        {
            // Act
            auto result = ValidateInput(-1);
            
            // Assert
            Assert::IsTrue(result.IsError(), L"Validation should fail for negative value");
            Assert::IsTrue(result.Error().message.find("non-negative") != std::string::npos,
                         L"Error message should mention non-negative");
        }
        
        TEST_METHOD(UsagePattern_Chaining)
        {
            // Arrange
            auto validateResult = ValidateInput(10);
            
            // Act & Assert
            if (validateResult.IsOk())
            {
                auto divideResult = DivideNumbers(10, 2);
                Assert::IsTrue(divideResult.IsOk(), L"After validation, division should succeed");
            }
            else
            {
                Assert::Fail(L"Validation should not fail");
            }
        }
    };
}
