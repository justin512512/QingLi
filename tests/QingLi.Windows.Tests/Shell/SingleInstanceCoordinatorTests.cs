using QingLi.Windows.Shell;

#pragma warning disable xUnit1031 // Intentionally reproduces WPF shutdown's synchronous wait.

namespace QingLi.Windows.Tests.Shell;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public void Dispose_completes_when_caller_blocks_a_single_threaded_context()
    {
        var completed = false;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
            var coordinator = new SingleInstanceCoordinator($"QingLi.Test.{Guid.NewGuid():N}");
            Assert.True(coordinator.TryAcquireAsync().GetAwaiter().GetResult());

            completed = coordinator.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(1));
        });

        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(2)));
        Assert.True(completed);
    }

    [Fact]
    public async Task Second_instance_notifies_first_instance()
    {
        var name = $"QingLi.Test.{Guid.NewGuid():N}";
        await using var first = new SingleInstanceCoordinator(name);
        Assert.True(await first.TryAcquireAsync());
        var signal = first.WaitForActivationAsync();
        await using var second = new SingleInstanceCoordinator(name);
        Assert.False(await second.TryAcquireAsync());

        await second.SignalPrimaryAsync("show-calendar");

        Assert.Equal("show-calendar", await signal.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
        {
        }
    }
}

#pragma warning restore xUnit1031
