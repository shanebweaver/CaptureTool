#include "pch.h"
#include "CppUnitTest.h"
#include "CaptureSessionState.h"
#include <thread>
#include <chrono>
#include <atomic>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

// Add ToString specialization for CaptureSessionState to enable assertion output
namespace Microsoft
{
    namespace VisualStudio
    {
        namespace CppUnitTestFramework
        {
            template<>
            static std::wstring ToString<CaptureSessionState>(const CaptureSessionState& state)
            {
                switch (state)
                {
                case CaptureSessionState::Created:
                    return L"Created";
                case CaptureSessionState::Initialized:
                    return L"Initialized";
                case CaptureSessionState::Active:
                    return L"Active";
                case CaptureSessionState::Paused:
                    return L"Paused";
                case CaptureSessionState::Stopped:
                    return L"Stopped";
                case CaptureSessionState::Failed:
                    return L"Failed";
                default:
                    return L"Unknown";
                }
            }
        }
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(CaptureSessionStateTests)
    {
    public:
        
        TEST_METHOD(StateMachine_InitialState_IsCreated)
        {
            // Arrange & Act
            CaptureSessionStateMachine stateMachine;
            
            // Assert
            Assert::AreEqual(CaptureSessionState::Created, stateMachine.GetState(), 
                          L"Initial state should be Created");
            Assert::IsFalse(stateMachine.IsInitialized(), 
                           L"Should not be initialized in Created state");
            Assert::IsFalse(stateMachine.IsActive(), 
                           L"Should not be active in Created state");
        }
        
        TEST_METHOD(StateMachine_CreatedToInitialized_ValidTransition)
        {
            // Arrange
            CaptureSessionStateMachine stateMachine;
            
            // Act
            bool canTransition = stateMachine.CanTransitionTo(CaptureSessionState::Initialized);
            bool transitionSucceeded = stateMachine.TryTransitionTo(CaptureSessionState::Initialized);
            
            // Assert
            Assert::IsTrue(canTransition, L"Should be able to transition from Created to Initialized");
            Assert::IsTrue(transitionSucceeded, L"Transition should succeed");
            Assert::AreEqual(CaptureSessionState::Initialized, stateMachine.GetState(), 
                          L"State should be Initialized");
            Assert::IsTrue(stateMachine.IsInitialized(), 
                          L"Should be initialized after transition");
        }
        
        TEST_METHOD(StateMachine_InitializedToActive_ValidTransition)
        {
            // Arrange
            CaptureSessionStateMachine stateMachine;
            stateMachine.TryTransitionTo(CaptureSessionState::Initialized);
            
            // Act
            bool canTransition = stateMachine.CanTransitionTo(CaptureSessionState::Active);
            bool transitionSucceeded = stateMachine.TryTransitionTo(CaptureSessionState::Active);
            
            // Assert
            Assert::IsTrue(canTransition, L"Should be able to transition from Initialized to Active");
            Assert::IsTrue(transitionSucceeded, L"Transition should succeed");
            Assert::AreEqual(CaptureSessionState::Active, stateMachine.GetState(), 
                          L"State should be Active");
            Assert::IsTrue(stateMachine.IsActive(), L"Should be active after transition");
        }
        
        TEST_METHOD(StateMachine_ActiveToPaused_ValidTransition)
        {
            // Arrange
            CaptureSessionStateMachine stateMachine;
            stateMachine.TryTransitionTo(CaptureSessionState::Initialized);
            stateMachine.TryTransitionTo(CaptureSessionState::Active);
            
            // Act
            bool canTransition = stateMachine.CanTransitionTo(CaptureSessionState::Paused);
            bool transitionSucceeded = stateMachine.TryTransitionTo(CaptureSessionState::Paused);
            
            // Assert
            Assert::IsTrue(canTransition, L"Should be able to transition from Active to Paused");
            Assert::IsTrue(transitionSucceeded, L"Transition should succeed");
            Assert::AreEqual(CaptureSessionState::Paused, stateMachine.GetState(), 
                          L"State should be Paused");
            Assert::IsTrue(stateMachine.IsActive(), 
                          L"Should still be considered active (can stop) when paused");
        }
        
        TEST_METHOD(StateMachine_PausedToActive_ValidTransition)
        {
            // Arrange
            CaptureSessionStateMachine stateMachine;
            stateMachine.TryTransitionTo(CaptureSessionState::Initialized);
            stateMachine.TryTransitionTo(CaptureSessionState::Active);
            stateMachine.TryTransitionTo(CaptureSessionState::Paused);
            
            // Act
            bool canTransition = stateMachine.CanTransitionTo(CaptureSessionState::Active);
            bool transitionSucceeded = stateMachine.TryTransitionTo(CaptureSessionState::Active);
            
            // Assert
            Assert::IsTrue(canTransition, L"Should be able to transition from Paused to Active");
            Assert::IsTrue(transitionSucceeded, L"Transition should succeed");
            Assert::AreEqual(CaptureSessionState::Active, stateMachine.GetState(), 
                          L"State should be Active");
        }
        
        TEST_METHOD(StateMachine_ActiveToStopped_ValidTransition)
        {
            // Arrange
            CaptureSessionStateMachine stateMachine;
            stateMachine.TryTransitionTo(CaptureSessionState::Initialized);
            stateMachine.TryTransitionTo(CaptureSessionState::Active);
            
            // Act
            bool canTransition = stateMachine.CanTransitionTo(CaptureSessionState::Stopped);
            bool transitionSucceeded = stateMachine.TryTransitionTo(CaptureSessionState::Stopped);
            
            // Assert
            Assert::IsTrue(canTransition, L"Should be able to transition from Active to Stopped");
            Assert::IsTrue(transitionSucceeded, L"Transition should succeed");
            Assert::AreEqual(CaptureSessionState::Stopped, stateMachine.GetState(), 
                          L"State should be Stopped");
            Assert::IsFalse(stateMachine.IsActive(), L"Should not be active after stopping");
        }
        
        TEST_METHOD(StateMachine_PausedToStopped_ValidTransition)
        {
            // Arrange
            CaptureSessionStateMachine stateMachine;
            stateMachine.TryTransitionTo(CaptureSessionState::Initialized);
            stateMachine.TryTransitionTo(CaptureSessionState::Active);
            stateMachine.TryTransitionTo(CaptureSessionState::Paused);
            
            // Act
            bool transitionSucceeded = stateMachine.TryTransitionTo(CaptureSessionState::Stopped);
            
            // Assert
            Assert::IsTrue(transitionSucceeded, L"Should be able to transition from Paused to Stopped");
            Assert::AreEqual(CaptureSessionState::Stopped, stateMachine.GetState(), 
                          L"State should be Stopped");
        }
        
        TEST_METHOD(StateMachine_AnyToFailed_ValidTransition)
        {
            // Test that any non-terminal state can transition to Failed
            
            // Created -> Failed
            CaptureSessionStateMachine sm1;
            Assert::IsTrue(sm1.TryTransitionTo(CaptureSessionState::Failed), 
                          L"Should be able to transition from Created to Failed");
            
            // Initialized -> Failed
            CaptureSessionStateMachine sm2;
            sm2.TryTransitionTo(CaptureSessionState::Initialized);
            Assert::IsTrue(sm2.TryTransitionTo(CaptureSessionState::Failed), 
                          L"Should be able to transition from Initialized to Failed");
            
            // Active -> Failed
            CaptureSessionStateMachine sm3;
            sm3.TryTransitionTo(CaptureSessionState::Initialized);
            sm3.TryTransitionTo(CaptureSessionState::Active);
            Assert::IsTrue(sm3.TryTransitionTo(CaptureSessionState::Failed), 
                          L"Should be able to transition from Active to Failed");
            
            // Paused -> Failed
            CaptureSessionStateMachine sm4;
            sm4.TryTransitionTo(CaptureSessionState::Initialized);
            sm4.TryTransitionTo(CaptureSessionState::Active);
            sm4.TryTransitionTo(CaptureSessionState::Paused);
            Assert::IsTrue(sm4.TryTransitionTo(CaptureSessionState::Failed), 
                          L"Should be able to transition from Paused to Failed");
        }
        
        TEST_METHOD(StateMachine_CreatedToActive_InvalidTransition)
        {
            // Arrange
            CaptureSessionStateMachine stateMachine;
            
            // Act
            bool canTransition = stateMachine.CanTransitionTo(CaptureSessionState::Active);
            bool transitionSucceeded = stateMachine.TryTransitionTo(CaptureSessionState::Active);
            
            // Assert
            Assert::IsFalse(canTransition, L"Should not be able to skip Initialized state");
            Assert::IsFalse(transitionSucceeded, L"Transition should fail");
            Assert::AreEqual(CaptureSessionState::Created, stateMachine.GetState(), 
                          L"State should remain Created");
        }
        
        TEST_METHOD(StateMachine_InitializedToPaused_InvalidTransition)
        {
            // Arrange
            CaptureSessionStateMachine stateMachine;
            stateMachine.TryTransitionTo(CaptureSessionState::Initialized);
            
            // Act
            bool canTransition = stateMachine.CanTransitionTo(CaptureSessionState::Paused);
            bool transitionSucceeded = stateMachine.TryTransitionTo(CaptureSessionState::Paused);
            
            // Assert
            Assert::IsFalse(canTransition, L"Cannot pause before activating");
            Assert::IsFalse(transitionSucceeded, L"Transition should fail");
            Assert::AreEqual(CaptureSessionState::Initialized, stateMachine.GetState(), 
                          L"State should remain Initialized");
        }
        
        TEST_METHOD(StateMachine_StoppedIsTerminal_NoTransitions)
        {
            // Arrange
            CaptureSessionStateMachine stateMachine;
            stateMachine.TryTransitionTo(CaptureSessionState::Initialized);
            stateMachine.TryTransitionTo(CaptureSessionState::Active);
            stateMachine.TryTransitionTo(CaptureSessionState::Stopped);
            
            // Act & Assert - Try various transitions from Stopped
            Assert::IsFalse(stateMachine.TryTransitionTo(CaptureSessionState::Created), 
                           L"Cannot transition from Stopped to Created");
            Assert::IsFalse(stateMachine.TryTransitionTo(CaptureSessionState::Initialized), 
                           L"Cannot transition from Stopped to Initialized");
            Assert::IsFalse(stateMachine.TryTransitionTo(CaptureSessionState::Active), 
                           L"Cannot transition from Stopped to Active");
            Assert::IsFalse(stateMachine.TryTransitionTo(CaptureSessionState::Paused), 
                           L"Cannot transition from Stopped to Paused");
            Assert::IsFalse(stateMachine.TryTransitionTo(CaptureSessionState::Failed), 
                           L"Cannot transition from Stopped to Failed");
            
            Assert::AreEqual(CaptureSessionState::Stopped, stateMachine.GetState(), 
                          L"State should remain Stopped");
        }
        
        TEST_METHOD(StateMachine_FailedIsTerminal_NoTransitions)
        {
            // Arrange
            CaptureSessionStateMachine stateMachine;
            stateMachine.TryTransitionTo(CaptureSessionState::Failed);
            
            // Act & Assert - Try various transitions from Failed
            Assert::IsFalse(stateMachine.TryTransitionTo(CaptureSessionState::Created), 
                           L"Cannot transition from Failed to Created");
            Assert::IsFalse(stateMachine.TryTransitionTo(CaptureSessionState::Initialized), 
                           L"Cannot transition from Failed to Initialized");
            Assert::IsFalse(stateMachine.TryTransitionTo(CaptureSessionState::Active), 
                           L"Cannot transition from Failed to Active");
            Assert::IsFalse(stateMachine.TryTransitionTo(CaptureSessionState::Paused), 
                           L"Cannot transition from Failed to Paused");
            Assert::IsFalse(stateMachine.TryTransitionTo(CaptureSessionState::Stopped), 
                           L"Cannot transition from Failed to Stopped");
            
            Assert::AreEqual(CaptureSessionState::Failed, stateMachine.GetState(), 
                          L"State should remain Failed");
        }
        
        TEST_METHOD(StateMachine_IsActiveQuery_CorrectForAllStates)
        {
            CaptureSessionStateMachine stateMachine;
            
            // Created - not active
            Assert::IsFalse(stateMachine.IsActive(), L"Created state should not be active");
            
            // Initialized - not active
            stateMachine.TryTransitionTo(CaptureSessionState::Initialized);
            Assert::IsFalse(stateMachine.IsActive(), L"Initialized state should not be active");
            
            // Active - active
            stateMachine.TryTransitionTo(CaptureSessionState::Active);
            Assert::IsTrue(stateMachine.IsActive(), L"Active state should be active");
            
            // Paused - active (can still stop)
            stateMachine.TryTransitionTo(CaptureSessionState::Paused);
            Assert::IsTrue(stateMachine.IsActive(), L"Paused state should be considered active");
            
            // Stopped - not active
            stateMachine.TryTransitionTo(CaptureSessionState::Stopped);
            Assert::IsFalse(stateMachine.IsActive(), L"Stopped state should not be active");
            
            // Failed - not active
            CaptureSessionStateMachine failedMachine;
            failedMachine.TryTransitionTo(CaptureSessionState::Failed);
            Assert::IsFalse(failedMachine.IsActive(), L"Failed state should not be active");
        }
        
        TEST_METHOD(StateMachine_ThreadSafety_ConcurrentStateReads)
        {
            // Arrange
            CaptureSessionStateMachine stateMachine;
            stateMachine.TryTransitionTo(CaptureSessionState::Initialized);
            stateMachine.TryTransitionTo(CaptureSessionState::Active);
            
            std::atomic<int> activeCount{0};
            std::atomic<int> totalReads{0};
            const int numThreads = 10;
            const int readsPerThread = 1000;
            
            // Act - Multiple threads reading state
            std::vector<std::thread> threads;
            for (int i = 0; i < numThreads; i++)
            {
                threads.emplace_back([&]() {
                    for (int j = 0; j < readsPerThread; j++)
                    {
                        if (stateMachine.IsActive())
                        {
                            activeCount++;
                        }
                        totalReads++;
                    }
                });
            }
            
            for (auto& thread : threads)
            {
                thread.join();
            }
            
            // Assert
            Assert::AreEqual(numThreads * readsPerThread, totalReads.load(), 
                           L"All reads should complete");
            Assert::AreEqual(numThreads * readsPerThread, activeCount.load(), 
                           L"All reads should see active state");
        }
        
        TEST_METHOD(StateMachine_ThreadSafety_ConcurrentStateTransitions)
        {
            // Arrange
            const int numMachines = 100;
            std::vector<std::unique_ptr<CaptureSessionStateMachine>> machines;
            
            for (int i = 0; i < numMachines; i++)
            {
                machines.push_back(std::make_unique<CaptureSessionStateMachine>());
            }
            
            std::atomic<int> successfulTransitions{0};
            
            // Act - Multiple threads transitioning different machines
            std::vector<std::thread> threads;
            for (int i = 0; i < numMachines; i++)
            {
                threads.emplace_back([&, i]() {
                    auto& machine = *machines[i];
                    
                    if (machine.TryTransitionTo(CaptureSessionState::Initialized))
                    {
                        successfulTransitions++;
                        
                        if (machine.TryTransitionTo(CaptureSessionState::Active))
                        {
                            successfulTransitions++;
                            
                            if (machine.TryTransitionTo(CaptureSessionState::Paused))
                            {
                                successfulTransitions++;
                                
                                if (machine.TryTransitionTo(CaptureSessionState::Active))
                                {
                                    successfulTransitions++;
                                    
                                    if (machine.TryTransitionTo(CaptureSessionState::Stopped))
                                    {
                                        successfulTransitions++;
                                    }
                                }
                            }
                        }
                    }
                });
            }
            
            for (auto& thread : threads)
            {
                thread.join();
            }
            
            // Assert - All state machines should successfully complete their transitions
            Assert::AreEqual(numMachines * 5, successfulTransitions.load(), 
                           L"All state transitions should succeed");
            
            // Verify all machines ended in Stopped state
            for (const auto& machine : machines)
            {
                Assert::AreEqual(CaptureSessionState::Stopped, machine->GetState(),
                             L"All machines should be in Stopped state");
            }
        }
    };
}
