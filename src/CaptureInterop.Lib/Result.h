#pragma once
#include "ErrorInfo.h"
#include <variant>
#include <utility>
#include <type_traits>

/// <summary>
/// Result type for operations that can succeed with a value or fail with an error.
/// Provides compile-time enforcement of error handling.
/// 
/// Usage:
///   Result<int> GetValue() {
///     if (error) return Result<int>::Error(errorInfo);
///     return Result<int>::Ok(42);
///   }
///   
///   auto result = GetValue();
///   if (result.IsOk()) {
///     int value = result.Value();
///   } else {
///     ErrorInfo error = result.Error();
///   }
/// </summary>
template<typename T>
class Result
{
public:
    /// <summary>
    /// Create a successful result with a value.
    /// </summary>
    static Result Ok(T&& value)
    {
        return Result(std::forward<T>(value));
    }
    
    /// <summary>
    /// Create a successful result with a value (lvalue reference).
    /// </summary>
    static Result Ok(const T& value)
    {
        return Result(value);
    }
    
    /// <summary>
    /// Create a failed result with error information.
    /// </summary>
    static Result Error(ErrorInfo error)
    {
        return Result(std::move(error));
    }
    
    /// <summary>
    /// Check if the result is successful.
    /// </summary>
    bool IsOk() const { return std::holds_alternative<T>(m_data); }
    
    /// <summary>
    /// Check if the result is an error.
    /// </summary>
    bool IsError() const { return !IsOk(); }
    
    /// <summary>
    /// Access the value. Undefined behavior if result is an error.
    /// Use IsOk() to check before calling.
    /// </summary>
    T& Value() { return std::get<T>(m_data); }
    const T& Value() const { return std::get<T>(m_data); }
    
    /// <summary>
    /// Access the error. Undefined behavior if result is ok.
    /// Use IsError() to check before calling.
    /// </summary>
    const ErrorInfo& Error() const { return std::get<ErrorInfo>(m_data); }
    
    /// <summary>
    /// Pattern match on the result with callbacks.
    /// Safe way to handle both success and error cases.
    /// </summary>
    template<typename OnOk, typename OnError>
    auto Match(OnOk&& onOk, OnError&& onError) const
    {
        if (IsOk())
            return onOk(std::get<T>(m_data));
        else
            return onError(std::get<ErrorInfo>(m_data));
    }
    
    /// <summary>
    /// Get the value or a default if error.
    /// </summary>
    T ValueOr(T&& defaultValue) const
    {
        if (IsOk())
            return std::get<T>(m_data);
        else
            return std::forward<T>(defaultValue);
    }
    
private:
    explicit Result(T&& value) : m_data(std::forward<T>(value)) {}
    explicit Result(const T& value) : m_data(value) {}
    explicit Result(ErrorInfo error) : m_data(std::move(error)) {}
    
    std::variant<T, ErrorInfo> m_data;
};

/// <summary>
/// Specialization for void operations (operations that don't return a value).
/// Used for operations that either succeed with no value or fail with an error.
/// 
/// Usage:
///   Result<void> DoSomething() {
///     if (error) return Result<void>::Error(errorInfo);
///     return Result<void>::Ok();
///   }
///   
///   auto result = DoSomething();
///   if (result.IsError()) {
///     ErrorInfo error = result.Error();
///   }
/// </summary>
template<>
class Result<void>
{
public:
    /// <summary>
    /// Create a successful result with no value.
    /// </summary>
    static Result Ok() { return Result(ErrorInfo::Success()); }
    
    /// <summary>
    /// Create a failed result with error information.
    /// </summary>
    static Result Error(ErrorInfo error) { return Result(std::move(error)); }
    
    /// <summary>
    /// Check if the result is successful.
    /// </summary>
    bool IsOk() const { return m_error.IsSuccess(); }
    
    /// <summary>
    /// Check if the result is an error.
    /// </summary>
    bool IsError() const { return !IsOk(); }
    
    /// <summary>
    /// Access the error. Undefined behavior if result is ok.
    /// Use IsError() to check before calling.
    /// </summary>
    const ErrorInfo& Error() const { return m_error; }
    
private:
    explicit Result(ErrorInfo error) : m_error(std::move(error)) {}
    ErrorInfo m_error;
};
