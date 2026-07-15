using QingLi.Windows.Views;

namespace QingLi.Windows.Tests.Views;

public sealed class CalendarPopupLayoutStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"QingLi.Windows.Tests.{Guid.NewGuid():N}");
    private readonly string _path;

    public CalendarPopupLayoutStoreTests()
    {
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "calendar-popup-layout.json");
    }

    [Fact]
    public async Task SaveAndLoadRoundTripLayout()
    {
        var store = new JsonCalendarPopupLayoutStore(_path);
        var expected = new CalendarPopupLayout(120.5, 80.25, 1040, 520, true);

        await store.SaveAsync(expected, CancellationToken.None);
        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task LoadReturnsNullWhenFileIsMissing()
    {
        var store = new JsonCalendarPopupLayoutStore(_path);

        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Null(actual);
    }

    [Fact]
    public async Task LoadReturnsNullForCorruptJson()
    {
        await File.WriteAllTextAsync(_path, "{not-json");
        var store = new JsonCalendarPopupLayoutStore(_path);

        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Null(actual);
    }

    [Theory]
    [InlineData("{\"Left\":1e400,\"Top\":20,\"Width\":800,\"Height\":500,\"IsCustomized\":true}")]
    [InlineData("{\"Left\":10,\"Top\":1e400,\"Width\":800,\"Height\":500,\"IsCustomized\":true}")]
    [InlineData("{\"Left\":10,\"Top\":20,\"Width\":759.99,\"Height\":500,\"IsCustomized\":true}")]
    [InlineData("{\"Left\":10,\"Top\":20,\"Width\":800,\"Height\":419.99,\"IsCustomized\":true}")]
    public async Task LoadReturnsNullForInvalidLayout(string json)
    {
        await File.WriteAllTextAsync(_path, json);
        var store = new JsonCalendarPopupLayoutStore(_path);

        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Null(actual);
    }

    [Theory]
    [InlineData(double.NaN, 20, 800, 500)]
    [InlineData(10, double.PositiveInfinity, 800, 500)]
    [InlineData(10, 20, double.NegativeInfinity, 500)]
    [InlineData(10, 20, 759.99, 500)]
    [InlineData(10, 20, 800, 419.99)]
    public async Task SaveRejectsInvalidLayout(double left, double top, double width, double height)
    {
        var store = new JsonCalendarPopupLayoutStore(_path);
        var layout = new CalendarPopupLayout(left, top, width, height, true);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.SaveAsync(layout, CancellationToken.None));

        Assert.False(File.Exists(_path));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public async Task SaveAtomicallyReplacesExistingLayoutAndRemovesTempFile()
    {
        var store = new JsonCalendarPopupLayoutStore(_path);
        var original = new CalendarPopupLayout(10, 20, 800, 500, false);
        var replacement = new CalendarPopupLayout(30, 40, 900, 600, true);
        await store.SaveAsync(original, CancellationToken.None);

        await store.SaveAsync(replacement, CancellationToken.None);

        Assert.Equal(replacement, await store.LoadAsync(CancellationToken.None));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public async Task ConcurrentSavesCompleteInInvocationOrder()
    {
        var store = new JsonCalendarPopupLayoutStore(_path);
        var first = new CalendarPopupLayout(10, 20, 800, 500, false);
        var second = new CalendarPopupLayout(30, 40, 900, 600, true);

        var firstSave = store.SaveAsync(first, CancellationToken.None);
        var secondSave = store.SaveAsync(second, CancellationToken.None);
        await Task.WhenAll(firstSave, secondSave);

        Assert.Equal(second, await store.LoadAsync(CancellationToken.None));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public async Task ClearInvokedAfterSaveWaitsForSaveAndWins()
    {
        var store = new JsonCalendarPopupLayoutStore(_path);
        var layout = new CalendarPopupLayout(10, 20, 800, 500, true);

        var save = store.SaveAsync(layout, CancellationToken.None);
        var clear = store.ClearAsync(CancellationToken.None);
        await Task.WhenAll(save, clear);

        Assert.Null(await store.LoadAsync(CancellationToken.None));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public async Task SaveWaitsForConcurrentLoadBeforeReplacingLayout()
    {
        var store = new JsonCalendarPopupLayoutStore(_path);
        var original = new CalendarPopupLayout(10, 20, 800, 500, false);
        var replacement = new CalendarPopupLayout(30, 40, 900, 600, true);
        await WriteLargeLayoutFileAsync(original);

        var load = store.LoadAsync(CancellationToken.None);
        Assert.False(load.IsCompleted);
        var save = store.SaveAsync(replacement, CancellationToken.None);
        await Task.WhenAll(load, save);

        Assert.Equal(original, await load);
        Assert.Equal(replacement, await store.LoadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ClearWaitsForConcurrentLoadBeforeDeletingLayout()
    {
        var store = new JsonCalendarPopupLayoutStore(_path);
        var original = new CalendarPopupLayout(10, 20, 800, 500, false);
        await WriteLargeLayoutFileAsync(original);

        var load = store.LoadAsync(CancellationToken.None);
        Assert.False(load.IsCompleted);
        var clear = store.ClearAsync(CancellationToken.None);
        await Task.WhenAll(load, clear);

        Assert.Equal(original, await load);
        Assert.Null(await store.LoadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SaveCancelledAfterTempCreationPreservesLayoutAndRemovesTempFile()
    {
        var original = new CalendarPopupLayout(10, 20, 800, 500, false);
        var replacement = new CalendarPopupLayout(30, 40, 900, 600, true);
        await new JsonCalendarPopupLayoutStore(_path).SaveAsync(original, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var tempPath = _path + ".tmp";
        var tempObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new JsonCalendarPopupLayoutStore(
            _path,
            async (stream, _, token) =>
            {
                await stream.WriteAsync(new byte[] { (byte)'{' }, token);
                await stream.FlushAsync(token);
                tempObserved.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            });

        var save = store.SaveAsync(replacement, cancellation.Token);
        Assert.True(await tempObserved.Task);
        Assert.True(File.Exists(tempPath));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            save);

        Assert.Equal(original, await store.LoadAsync(CancellationToken.None));
        Assert.False(File.Exists(tempPath));
    }

    [Fact]
    public async Task FailedReplacementPreservesExistingLayoutAndRemovesTempFile()
    {
        var store = new JsonCalendarPopupLayoutStore(_path);
        var original = new CalendarPopupLayout(10, 20, 800, 500, false);
        var replacement = new CalendarPopupLayout(30, 40, 900, 600, true);
        await store.SaveAsync(original, CancellationToken.None);

        using (File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var exception = await Record.ExceptionAsync(() =>
                store.SaveAsync(replacement, CancellationToken.None));

            Assert.True(exception is IOException or UnauthorizedAccessException);
        }

        Assert.Equal(original, await store.LoadAsync(CancellationToken.None));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public async Task ClearDeletesLayoutAndStaleTempButPreservesSiblingFiles()
    {
        var store = new JsonCalendarPopupLayoutStore(_path);
        var siblingPath = Path.Combine(_directory, "keep.json");
        await File.WriteAllTextAsync(_path, "layout");
        await File.WriteAllTextAsync(_path + ".tmp", "stale");
        await File.WriteAllTextAsync(siblingPath, "keep");

        await store.ClearAsync(CancellationToken.None);

        Assert.False(File.Exists(_path));
        Assert.False(File.Exists(_path + ".tmp"));
        Assert.True(File.Exists(siblingPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private async Task WriteLargeLayoutFileAsync(CalendarPopupLayout layout)
    {
        await using var stream = new FileStream(
            _path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        await System.Text.Json.JsonSerializer.SerializeAsync(stream, layout);

        var padding = new byte[1024 * 1024];
        Array.Fill(padding, (byte)' ');
        for (var index = 0; index < 32; index++)
        {
            await stream.WriteAsync(padding);
        }
    }
}
