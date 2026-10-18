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
        environment.Selection.SetResult(new SelectionResult(1, new PixelRect(2, 1, 3, 2)));

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
        public List<string> Calls { get; } = [];

        public Func<DesktopSnapshot> Snapshot { get; init; } = CreateSnapshot;

        public TaskCompletionSource<SelectionResult?> Selection { get; } = new();

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

        public Task<SelectionResult?> SelectRegionAsync(DesktopSnapshot snapshot)
        {
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
