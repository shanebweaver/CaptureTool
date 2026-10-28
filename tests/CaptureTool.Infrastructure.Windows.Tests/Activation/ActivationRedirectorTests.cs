using CaptureTool.Infrastructure.Windows.Activation;

namespace CaptureTool.Infrastructure.Windows.Tests.Activation;

[TestClass]
public sealed class ActivationRedirectorTests
{
    [TestMethod]
    public async Task SuccessfulRedirect_CompletesOnStaThread()
    {
        bool redirected = false;
        Exception? error = await OnStaThreadAsync(() => ActivationRedirector.Redirect(async () =>
        {
            await Task.Yield();
            redirected = true;
        }, TimeSpan.FromSeconds(5)));

        Assert.IsNull(error);
        Assert.IsTrue(redirected);
    }

    [TestMethod]
    public async Task SynchronousFailure_ReleasesStaWaitAndPreservesException()
    {
        var failure = new InvalidOperationException("Primary exited.");
        Exception? error = await OnStaThreadAsync(() => ActivationRedirector.Redirect(
            () => throw failure, TimeSpan.FromSeconds(5)));

        Assert.AreSame(failure, error);
    }

    [TestMethod]
    public async Task AsynchronousFailure_ReleasesStaWaitAndPreservesException()
    {
        var failure = new InvalidOperationException("Redirection failed.");
        Exception? error = await OnStaThreadAsync(() => ActivationRedirector.Redirect(async () =>
        {
            await Task.Yield();
            throw failure;
        }, TimeSpan.FromSeconds(5)));

        Assert.AreSame(failure, error);
    }

    [TestMethod]
    public async Task CancelledRedirect_ReleasesStaWait()
    {
        Exception? error = await OnStaThreadAsync(() => ActivationRedirector.Redirect(
            () => Task.FromCanceled(new CancellationToken(true)), TimeSpan.FromSeconds(5)));

        Assert.IsInstanceOfType<OperationCanceledException>(error);
    }

    [TestMethod]
    public async Task UnresponsivePrimary_TimesOutWithoutWaitingForRedirection()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            Exception? error = await OnStaThreadAsync(() => ActivationRedirector.Redirect(
                () => pending.Task, TimeSpan.FromMilliseconds(100)));

            Assert.IsInstanceOfType<TimeoutException>(error);
            Assert.IsFalse(pending.Task.IsCompleted);
        }
        finally
        {
            // Exercise completion after the caller has already stopped waiting.
            pending.SetException(new InvalidOperationException("Late redirect failure."));
        }
    }

    private static async Task<Exception?> OnStaThreadAsync(Action action)
    {
        var completion = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                action();
                completion.SetResult(null);
            }
            catch (Exception exception)
            {
                completion.SetResult(exception);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
