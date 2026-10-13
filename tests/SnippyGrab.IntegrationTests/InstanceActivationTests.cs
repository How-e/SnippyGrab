using SnippyGrab.App.Services;

namespace SnippyGrab.IntegrationTests;

public sealed class InstanceActivationTests
{
    [Fact]
    public async Task SecondLaunchSignalsExistingInstanceEvenBeforeListenerIsReady()
    {
        var name = @"Local\SnippyGrab-activation-test-" + Guid.NewGuid().ToString("N");
        using var first = new InstanceActivation(name); using var second = new InstanceActivation(name);
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        second.Signal(); first.Listen(() => activated.TrySetResult());
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        activated = new(TaskCreationOptions.RunContinuationsAsynchronously); second.Signal();
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
