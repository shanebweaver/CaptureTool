using CaptureTool.Presentation.Activation;
using FluentAssertions;

namespace CaptureTool.Presentation.Tests.Activation;

[TestClass]
public sealed class StartupActivationQueueTests
{
    [TestMethod]
    public void PrimaryStartup_WithImmediateRedirect_HandlesPrimaryBeforeRedirect()
    {
        var queue = new StartupActivationQueue<string>();
        List<string> handled = [];

        queue.Enqueue("redirected protocol");
        handled.Add("primary launch");
        queue.Attach(handled.Add);

        handled.Should().Equal("primary launch", "redirected protocol");
    }

    [TestMethod]
    public void Attach_WithMultiplePendingActivations_DrainsInFifoOrder()
    {
        var queue = new StartupActivationQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);
        List<int> handled = [];

        queue.Attach(handled.Add);

        handled.Should().Equal(1, 2, 3);
    }

    [TestMethod]
    public void Enqueue_DuringDrain_DoesNotOvertakePendingActivations()
    {
        var queue = new StartupActivationQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);
        List<int> handled = [];

        queue.Attach(activation =>
        {
            handled.Add(activation);
            if (activation == 1)
            {
                queue.Enqueue(3);
            }
        });

        handled.Should().Equal(1, 2, 3);
    }

    [TestMethod]
    public async Task Enqueue_ConcurrentWithDrain_DoesNotLoseOrOvertakeActivation()
    {
        var queue = new StartupActivationQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);
        List<int> handled = [];
        var firstActivationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDrainToContinue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task attachTask = Task.Run(() => queue.Attach(activation =>
        {
            handled.Add(activation);
            if (activation == 1)
            {
                firstActivationStarted.SetResult(true);
                allowDrainToContinue.Task.GetAwaiter().GetResult();
            }
        }));

        await firstActivationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Run(() => queue.Enqueue(3)).WaitAsync(TimeSpan.FromSeconds(5));
        allowDrainToContinue.SetResult(true);
        await attachTask.WaitAsync(TimeSpan.FromSeconds(5));

        handled.Should().Equal(1, 2, 3);
    }

    [TestMethod]
    public void Attach_WhenConsumerIsAlreadyAttached_FailsExplicitly()
    {
        var queue = new StartupActivationQueue<int>();
        queue.Attach(_ => { });

        Action attachAgain = () => queue.Attach(_ => { });

        attachAgain.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void ConsumerFailure_RetainsActivationAheadOfLaterWork()
    {
        var queue = new StartupActivationQueue<int>();
        queue.Enqueue(1);
        List<int> handled = [];
        bool shouldFail = true;

        Action attach = () => queue.Attach(activation =>
        {
            if (shouldFail)
            {
                shouldFail = false;
                throw new InvalidOperationException("Consumer is unavailable.");
            }

            handled.Add(activation);
        });

        attach.Should().Throw<InvalidOperationException>();
        queue.Enqueue(2);

        handled.Should().Equal(1, 2);
    }
}
