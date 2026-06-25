using QingLi.Windows.Shell;

namespace QingLi.Windows.Tests.Shell;

public sealed class SingleInstanceCoordinatorTests
{
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
}
