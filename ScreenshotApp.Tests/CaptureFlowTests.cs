using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Capture;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Platform;

namespace ScreenshotApp.Tests;

/// <summary>
/// 以測試替身取代視窗、螢幕與覆蓋層，驗證截圖流程的步驟順序，以及完成、取消、失敗、重入時都會恢復視窗並回到常駐狀態。
/// </summary>
public class CaptureFlowTests
{
    [Fact]
    public async Task RunAsync_SelectionCompleted_ReturnsCropOfTheChosenMonitor()
    {
        var environment = new FakeEnvironment();
        var state = new AppStateMachine();
        environment.Selection.SetResult(new RegionSelected(new SelectionResult(1, new PixelRect(2, 1, 3, 2))));

        var outcome = await new CaptureFlow(state, environment).RunAsync();

        Assert.Equal(CaptureStatus.Completed, outcome.Status);
        Assert.NotNull(outcome.Image);
        Assert.Equal(3, outcome.Image.PixelWidth);
        Assert.Equal(2, outcome.Image.PixelHeight);
        Assert.True(outcome.Image.IsFrozen);
        Assert.Equal(PatternPixels(monitorIndex: 1, new PixelRect(2, 1, 3, 2)), Pixels(outcome.Image));
        Assert.Equal(new[] { "hide", "snapshot", "select", "restore" }, environment.Calls);
        Assert.Equal(AppState.Resident, state.Current);
    }

    [Fact]
    public async Task RunAsync_SelectionCancelled_RestoresWindowsAndReturnsToResident()
    {
        var environment = new FakeEnvironment();
        var state = new AppStateMachine();
        environment.Selection.SetResult(null);

        var outcome = await new CaptureFlow(state, environment).RunAsync();

        Assert.Equal(CaptureStatus.Cancelled, outcome.Status);
        Assert.Null(outcome.Image);
        Assert.Equal(new[] { "hide", "snapshot", "select", "restore" }, environment.Calls);
        Assert.Equal(AppState.Resident, state.Current);
    }

    [Fact]
    public async Task RunAsync_SnapshotFails_ReportsTheErrorWithoutShowingTheOverlay()
    {
        var environment = new FakeEnvironment { Snapshot = () => throw new CaptureException("擷取失敗") };
        var state = new AppStateMachine();

        var outcome = await new CaptureFlow(state, environment).RunAsync();

        Assert.Equal(CaptureStatus.Failed, outcome.Status);
        Assert.Equal("擷取失敗", outcome.Error?.Message);
        Assert.Equal(new[] { "hide", "snapshot", "restore" }, environment.Calls);
        Assert.Equal(AppState.Resident, state.Current);
    }

    [Fact]
    public async Task RunAsync_DisplayChangedDuringSelection_ReportsTheError()
    {
        var environment = new FakeEnvironment();
        var state = new AppStateMachine();
        environment.Selection.SetException(new CaptureException("顯示設定已變更"));

        var outcome = await new CaptureFlow(state, environment).RunAsync();

        Assert.Equal(CaptureStatus.Failed, outcome.Status);
        Assert.Equal("顯示設定已變更", outcome.Error?.Message);
        Assert.Equal(new[] { "hide", "snapshot", "select", "restore" }, environment.Calls);
        Assert.Equal(AppState.Resident, state.Current);
    }

    [Fact]
    public async Task RunAsync_UnexpectedError_PropagatesAfterRestoringWindows()
    {
        var environment = new FakeEnvironment { Snapshot = () => throw new InvalidOperationException("未預期") };
        var state = new AppStateMachine();

        await Assert.ThrowsAsync<InvalidOperationException>(new CaptureFlow(state, environment).RunAsync);

        Assert.Equal(new[] { "hide", "snapshot", "restore" }, environment.Calls);
        Assert.Equal(AppState.Resident, state.Current);
    }

    [Fact]
    public async Task RunAsync_WhileSelecting_IgnoresTheNewRequest()
    {
        var environment = new FakeEnvironment();
        var state = new AppStateMachine();
        var flow = new CaptureFlow(state, environment);

        var first = flow.RunAsync();
        Assert.Equal(AppState.Selecting, state.Current);

        var second = await flow.RunAsync();

        Assert.Equal(CaptureStatus.Ignored, second.Status);
        Assert.Equal(new[] { "hide", "snapshot", "select" }, environment.Calls);
        Assert.Equal(AppState.Selecting, state.Current);

        environment.Selection.SetResult(null);
        Assert.Equal(CaptureStatus.Cancelled, (await first).Status);
        Assert.Equal(AppState.Resident, state.Current);
    }

    [Fact]
    public void Crop_OutsideTheMonitorSnapshot_Throws()
    {
        var snapshot = FakeEnvironment.CreateSnapshot();

        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.Crop(new SelectionResult(0, new PixelRect(5, 0, 4, 1))));
        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.Crop(new SelectionResult(0, new PixelRect(0, 0, 0, 1))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AT12_ColorPicked_RestoresPriorStateAndWindowsWithoutCreatingImage(bool editing)
    {
        var environment = new FakeEnvironment();
        var state = new AppStateMachine();
        if (editing)
        {
            state.BeginEditing();
        }

        var task = new CaptureFlow(state, environment).RunAsync();
        Assert.Equal(AppState.Selecting, state.Current);
        environment.Selection.SetResult(new ColorPicked("#01ABEF"));
        var outcome = await task;

        Assert.Equal(CaptureStatus.ColorPicked, outcome.Status);
        Assert.Equal("#01ABEF", outcome.ColorCode);
        Assert.Null(outcome.Image);
        Assert.Null(outcome.Error);
        Assert.Equal(new[] { "hide", "snapshot", "select", "restore" }, environment.Calls);
        Assert.Equal(editing ? AppState.Editing : AppState.Resident, state.Current);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public async Task AT16_ScrollingKeepsWindowsHiddenAndStateBusyUntilEveryExit(bool editing, int ending)
    {
        var environment = new FakeEnvironment();
        var state = new AppStateMachine();
        if (editing) state.BeginEditing();
        environment.Selection.SetResult(new RegionSelected(new SelectionResult(1, new PixelRect(1, 1, 4, 2))));
        var completion = new TaskCompletionSource<BitmapSource?>();
        var flow = new CaptureFlow(state, environment);
        var pending = flow.RunAsync(rect =>
        {
            Assert.Equal(new PixelRect(-5, 1, 4, 2), rect);
            Assert.True(environment.Scrolling);
            Assert.Equal(AppState.Selecting, state.Current);
            Assert.DoesNotContain("restore", environment.Calls);
            return completion.Task;
        });
        Assert.Equal(CaptureStatus.Ignored, (await flow.RunAsync()).Status);
        Assert.Equal(CaptureStatus.Ignored, (await flow.RunAsync(_ => throw new InvalidOperationException())).Status);
        var image = FakeEnvironment.CreateSnapshot().Monitors[1].Image;
        if (ending >= 2) completion.SetException(new CaptureException(ending == 2 ? "擷取失敗" : "顯示設定改變"));
        else completion.SetResult(ending == 0 ? image : null);
        var result = await pending;
        Assert.Equal(ending == 0 ? CaptureStatus.Completed : ending == 1 ? CaptureStatus.Cancelled : CaptureStatus.Failed, result.Status);
        Assert.Equal(ending == 0 ? image : null, result.Image);
        Assert.Equal(editing ? AppState.Editing : AppState.Resident, state.Current);
        Assert.Equal(new[] { "hide", "snapshot", "select", "restore" }, environment.Calls);
    }

    [Fact]
    public async Task AT16_SelectionCancellation_DoesNotStartScrolling()
    {
        var environment = new FakeEnvironment();
        environment.Selection.SetResult(null);
        var state = new AppStateMachine();
        var result = await new CaptureFlow(state, environment).RunAsync(_ => throw new InvalidOperationException());
        Assert.Equal(CaptureStatus.Cancelled, result.Status);
        Assert.Equal(AppState.Resident, state.Current);
        Assert.Equal("restore", environment.Calls.Last());
    }

    // 每個像素的值由顯示器序號與座標決定，裁切位置錯一格就會不同。
    private static byte[] PatternPixels(int monitorIndex, PixelRect rect)
    {
        var pixels = new List<byte>();
        for (var y = rect.Y; y < rect.Bottom; y++)
        {
            for (var x = rect.X; x < rect.Right; x++)
            {
                pixels.AddRange(PatternPixel(monitorIndex, x, y));
            }
        }

        return [.. pixels];
    }

    private static byte[] PatternPixel(int monitorIndex, int x, int y) => [(byte)x, (byte)y, (byte)(monitorIndex * 50), 0xFF];

    private static byte[] Pixels(BitmapSource image)
    {
        var stride = image.PixelWidth * 4;
        var pixels = new byte[stride * image.PixelHeight];
        image.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private sealed class FakeEnvironment : ICaptureEnvironment
    {
        public bool Scrolling { get; private set; }
        public List<string> Calls { get; } = [];

        public Func<DesktopSnapshot> Snapshot { get; init; } = CreateSnapshot;

        public TaskCompletionSource<OverlayResult?> Selection { get; } = new();

        public static DesktopSnapshot CreateSnapshot() => new(
        [
            new MonitorSnapshot(new MonitorInfo(0, new PixelRect(0, 0, 8, 6)), CreatePattern(0, 8, 6)),
            new MonitorSnapshot(new MonitorInfo(1, new PixelRect(-6, 0, 6, 4)), CreatePattern(1, 6, 4)),
        ]);

        public IDisposable HideOwnWindows()
        {
            Calls.Add("hide");
            return new Restorer(Calls);
        }

        public DesktopSnapshot TakeSnapshot()
        {
            Calls.Add("snapshot");
            return Snapshot();
        }

        public Task<OverlayResult?> SelectRegionAsync(DesktopSnapshot snapshot, bool scrolling = false)
        {
            Scrolling = scrolling;
            Calls.Add("select");
            return Selection.Task;
        }

        private static BitmapSource CreatePattern(int monitorIndex, int width, int height)
        {
            var pixels = new List<byte>();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pixels.AddRange(PatternPixel(monitorIndex, x, y));
                }
            }

            var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels.ToArray(), width * 4);
            image.Freeze();
            return image;
        }

        private sealed class Restorer(List<string> calls) : IDisposable
        {
            public void Dispose() => calls.Add("restore");
        }
    }
}
