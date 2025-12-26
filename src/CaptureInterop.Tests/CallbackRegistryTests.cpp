#include "pch.h"
#include "CppUnitTest.h"
#include "CallbackHandle.h"
#include "CallbackRegistry.h"
#include <thread>
#include <chrono>
#include <atomic>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop;

namespace CaptureInteropTests
{
    // Test data structure
    struct TestEventData
    {
        int value;
        std::string message;
    };

    TEST_CLASS(CallbackHandleTests)
    {
    public:
        
        TEST_METHOD(CallbackHandle_DefaultConstructor_IsInvalid)
        {
            // Arrange & Act
            CallbackHandle handle;
            
            // Assert
            Assert::IsFalse(handle.IsValid(), L"Default constructed handle should be invalid");
        }
        
        TEST_METHOD(CallbackHandle_WithUnregisterFunction_IsValid)
        {
            // Arrange
            bool unregisterCalled = false;
            
            // Act
            CallbackHandle handle([&unregisterCalled]() { unregisterCalled = true; });
            
            // Assert
            Assert::IsTrue(handle.IsValid(), L"Handle with function should be valid");
            Assert::IsFalse(unregisterCalled, L"Unregister should not be called yet");
        }
        
        TEST_METHOD(CallbackHandle_Destructor_CallsUnregisterFunction)
        {
            // Arrange
            bool unregisterCalled = false;
            
            // Act
            {
                CallbackHandle handle([&unregisterCalled]() { unregisterCalled = true; });
                Assert::IsFalse(unregisterCalled, L"Unregister should not be called yet");
            } // handle destroyed here
            
            // Assert
            Assert::IsTrue(unregisterCalled, L"Unregister should be called on destruction");
        }
        
        TEST_METHOD(CallbackHandle_ExplicitUnregister_CallsFunction)
        {
            // Arrange
            bool unregisterCalled = false;
            CallbackHandle handle([&unregisterCalled]() { unregisterCalled = true; });
            
            // Act
            handle.Unregister();
            
            // Assert
            Assert::IsTrue(unregisterCalled, L"Unregister should be called");
            Assert::IsFalse(handle.IsValid(), L"Handle should be invalid after unregister");
        }
        
        TEST_METHOD(CallbackHandle_UnregisterTwice_CallsFunctionOnce)
        {
            // Arrange
            int unregisterCount = 0;
            CallbackHandle handle([&unregisterCount]() { unregisterCount++; });
            
            // Act
            handle.Unregister();
            handle.Unregister(); // Second call should be no-op
            
            // Assert
            Assert::AreEqual(1, unregisterCount, L"Unregister function should be called only once");
        }
        
        TEST_METHOD(CallbackHandle_MoveConstructor_TransfersOwnership)
        {
            // Arrange
            bool unregisterCalled = false;
            CallbackHandle handle1([&unregisterCalled]() { unregisterCalled = true; });
            
            // Act
            CallbackHandle handle2(std::move(handle1));
            
            // Assert
            Assert::IsFalse(handle1.IsValid(), L"Source handle should be invalid");
            Assert::IsTrue(handle2.IsValid(), L"Destination handle should be valid");
            Assert::IsFalse(unregisterCalled, L"Unregister should not be called during move");
        }
        
        TEST_METHOD(CallbackHandle_MoveAssignment_TransfersOwnership)
        {
            // Arrange
            bool unregisterCalled1 = false;
            bool unregisterCalled2 = false;
            CallbackHandle handle1([&unregisterCalled1]() { unregisterCalled1 = true; });
            CallbackHandle handle2([&unregisterCalled2]() { unregisterCalled2 = true; });
            
            // Act
            handle2 = std::move(handle1);
            
            // Assert
            Assert::IsFalse(handle1.IsValid(), L"Source handle should be invalid");
            Assert::IsTrue(handle2.IsValid(), L"Destination handle should be valid");
            Assert::IsTrue(unregisterCalled2, L"Original handle2 should be unregistered");
            Assert::IsFalse(unregisterCalled1, L"handle1 should not be unregistered yet");
        }
    };

    TEST_CLASS(CallbackRegistryTests)
    {
    public:
        
        TEST_METHOD(CallbackRegistry_Register_ReturnsValidHandle)
        {
            // Arrange
            CallbackRegistry<TestEventData> registry;
            
            // Act
            auto handle = registry.Register([](const TestEventData&) {});
            
            // Assert
            Assert::IsTrue(handle.IsValid(), L"Registered handle should be valid");
            Assert::AreEqual(size_t(1), registry.Count(), L"Should have one registered callback");
        }
        
        TEST_METHOD(CallbackRegistry_Invoke_CallsRegisteredCallback)
        {
            // Arrange
            CallbackRegistry<TestEventData> registry;
            bool callbackCalled = false;
            int receivedValue = 0;
            
            auto handle = registry.Register([&](const TestEventData& data) {
                callbackCalled = true;
                receivedValue = data.value;
            });
            
            // Act
            TestEventData testData{ 42, "test" };
            registry.Invoke(testData);
            
            // Assert
            Assert::IsTrue(callbackCalled, L"Callback should be called");
            Assert::AreEqual(42, receivedValue, L"Callback should receive correct data");
        }
        
        TEST_METHOD(CallbackRegistry_MultipleCallbacks_AllInvoked)
        {
            // Arrange
            CallbackRegistry<TestEventData> registry;
            int callback1Count = 0;
            int callback2Count = 0;
            int callback3Count = 0;
            
            auto handle1 = registry.Register([&](const TestEventData&) { callback1Count++; });
            auto handle2 = registry.Register([&](const TestEventData&) { callback2Count++; });
            auto handle3 = registry.Register([&](const TestEventData&) { callback3Count++; });
            
            // Act
            TestEventData testData{ 1, "test" };
            registry.Invoke(testData);
            
            // Assert
            Assert::AreEqual(1, callback1Count, L"Callback 1 should be called once");
            Assert::AreEqual(1, callback2Count, L"Callback 2 should be called once");
            Assert::AreEqual(1, callback3Count, L"Callback 3 should be called once");
        }
        
        TEST_METHOD(CallbackRegistry_HandleDestroyed_CallbackNotInvoked)
        {
            // Arrange
            CallbackRegistry<TestEventData> registry;
            int callbackCount = 0;
            
            {
                auto handle = registry.Register([&](const TestEventData&) { callbackCount++; });
                Assert::AreEqual(size_t(1), registry.Count(), L"Should have one callback");
            } // handle destroyed here
            
            // Act
            TestEventData testData{ 1, "test" };
            registry.Invoke(testData);
            
            // Assert
            Assert::AreEqual(0, callbackCount, L"Callback should not be called after handle destroyed");
            Assert::AreEqual(size_t(0), registry.Count(), L"Should have zero callbacks");
        }
        
        TEST_METHOD(CallbackRegistry_Clear_RemovesAllCallbacks)
        {
            // Arrange
            CallbackRegistry<TestEventData> registry;
            auto handle1 = registry.Register([](const TestEventData&) {});
            auto handle2 = registry.Register([](const TestEventData&) {});
            
            Assert::AreEqual(size_t(2), registry.Count(), L"Should have two callbacks");
            
            // Act
            registry.Clear();
            
            // Assert
            Assert::AreEqual(size_t(0), registry.Count(), L"Should have zero callbacks after clear");
            Assert::IsFalse(registry.HasCallbacks(), L"Should not have callbacks");
        }
        
        TEST_METHOD(CallbackRegistry_HasCallbacks_ReturnsCorrectValue)
        {
            // Arrange
            CallbackRegistry<TestEventData> registry;
            
            // Act & Assert
            Assert::IsFalse(registry.HasCallbacks(), L"Should not have callbacks initially");
            
            auto handle = registry.Register([](const TestEventData&) {});
            Assert::IsTrue(registry.HasCallbacks(), L"Should have callbacks after register");
            
            handle.Unregister();
            Assert::IsFalse(registry.HasCallbacks(), L"Should not have callbacks after unregister");
        }
        
        TEST_METHOD(CallbackRegistry_ThreadSafety_ConcurrentRegisterUnregister)
        {
            // Arrange
            CallbackRegistry<TestEventData> registry;
            std::atomic<int> totalCallbacks{0};
            const int numThreads = 10;
            const int operationsPerThread = 100;
            
            // Act - Multiple threads registering and unregistering
            std::vector<std::thread> threads;
            for (int i = 0; i < numThreads; i++)
            {
                threads.emplace_back([&]() {
                    for (int j = 0; j < operationsPerThread; j++)
                    {
                        auto handle = registry.Register([&](const TestEventData&) {
                            totalCallbacks++;
                        });
                        
                        // Yield to allow better thread interleaving
                        std::this_thread::yield();
                        
                        // Handle will unregister on destruction
                    }
                });
            }
            
            for (auto& thread : threads)
            {
                thread.join();
            }
            
            // Assert - No crashes, all handles cleaned up
            Assert::AreEqual(size_t(0), registry.Count(), L"All callbacks should be unregistered");
        }
        
        TEST_METHOD(CallbackRegistry_ThreadSafety_ConcurrentInvoke)
        {
            // Arrange
            CallbackRegistry<TestEventData> registry;
            std::atomic<int> totalInvocations{0};
            
            auto handle1 = registry.Register([&](const TestEventData&) { totalInvocations++; });
            auto handle2 = registry.Register([&](const TestEventData&) { totalInvocations++; });
            auto handle3 = registry.Register([&](const TestEventData&) { totalInvocations++; });
            
            const int numThreads = 5;
            const int invocationsPerThread = 100;
            
            // Act - Multiple threads invoking callbacks
            std::vector<std::thread> threads;
            for (int i = 0; i < numThreads; i++)
            {
                threads.emplace_back([&]() {
                    for (int j = 0; j < invocationsPerThread; j++)
                    {
                        TestEventData data{ j, "test" };
                        registry.Invoke(data);
                    }
                });
            }
            
            for (auto& thread : threads)
            {
                thread.join();
            }
            
            // Assert - Each invocation calls 3 callbacks
            int expectedInvocations = numThreads * invocationsPerThread * 3;
            Assert::AreEqual(expectedInvocations, totalInvocations.load(), 
                           L"All callbacks should be invoked correct number of times");
        }
        
        TEST_METHOD(CallbackRegistry_InvokeDoesNotHoldLock_PreventsDeadlock)
        {
            // Arrange
            CallbackRegistry<TestEventData> registry;
            std::atomic<bool> callbackStarted{false};
            std::atomic<bool> callbackCompleted{false};
            
            // Register a callback that takes some time
            auto handle = registry.Register([&](const TestEventData&) {
                callbackStarted = true;
                std::this_thread::sleep_for(std::chrono::milliseconds(50));
                callbackCompleted = true;
            });
            
            // Act - Start invoke in another thread
            std::thread invokeThread([&]() {
                TestEventData data{ 1, "test" };
                registry.Invoke(data);
            });
            
            // Wait for callback to start
            while (!callbackStarted) {
                std::this_thread::sleep_for(std::chrono::milliseconds(1));
            }
            
            // Try to register another callback while first is executing
            // This should not deadlock because Invoke releases lock before calling callbacks
            auto handle2 = registry.Register([](const TestEventData&) {});
            
            invokeThread.join();
            
            // Assert
            Assert::IsTrue(callbackCompleted.load(), L"Callback should complete");
            Assert::AreEqual(size_t(2), registry.Count(), L"Should have two callbacks");
        }
    };
}
