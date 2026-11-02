using ScreenshotApp.Capture;
using static ScreenshotApp.Tests.ScrollStitcherTests;

namespace ScreenshotApp.Tests;

public class ScrollCaptureSessionTests
{
    [Fact]
    public void AT16_Manual_WaitsForThreeIdenticalSamples()
    {
        var session = new ScrollCaptureSession(Frame(0));
        Assert.Null(session.Observe(Frame(10)));
        Assert.Null(session.Observe(Frame(20)));
        Assert.Null(session.Observe(Frame(20)));
        Assert.Equal(96, session.Stitcher.Height);
        Assert.Equal(StitchStatus.Appended, session.Observe(Frame(20)));
        Assert.Equal(116, session.Stitcher.Height);
        Assert.False(session.Automatic);
    }

    [Fact]
    public void AT16_Automatic_CompletesOnlyAfterProgressAndThreeUnchangedAttempts()
    {
        var session = new ScrollCaptureSession(Frame(0));
        session.StartAutomatic();
        Cycle(session, 20);
        for (var i = 0; i < 2; i++)
        {
            Cycle(session, 20);
            Assert.False(session.Complete);
        }
        Cycle(session, 20);
        Assert.True(session.Complete);
        Assert.False(session.Automatic);
    }

    [Fact]
    public void AT16_NoWheelResponse_StopsAndOffersManualInsteadOfClaimingBottom()
    {
        var session = new ScrollCaptureSession(Frame(0));
        session.StartAutomatic();
        for (var i = 0; i < 3; i++) Cycle(session, 0);
        Assert.True(session.NoScrollResponse);
        Assert.False(session.Complete);
        Assert.False(session.Automatic);
    }

    [Fact]
    public void AT16_StopPreservesImage_AndUnstableContentStopsAutomatic()
    {
        var session = new ScrollCaptureSession(Frame(0));
        session.StartAutomatic();
        Cycle(session, 20);
        session.StopAutomatic();
        Assert.False(session.NeedsScroll);
        Assert.Equal(116, session.Stitcher.Height);
        session.StartAutomatic();
        session.ScrollSent();
        for (var i = 0; i < 27; i++) session.Observe(Frame(30 + i));
        Assert.True(session.Unstable);
        Assert.False(session.Automatic);
        Assert.Equal(116, session.Stitcher.Height);
    }

    [Fact]
    public void AT16_FailedAlignment_StopsWithoutLosingInitialFrame()
    {
        var session = new ScrollCaptureSession(Frame(0));
        session.StartAutomatic();
        Cycle(session, 2000);
        Assert.False(session.Automatic);
        Assert.False(session.Complete);
        Assert.Equal(Frame(0).Pixels, session.Stitcher.GetPixels());
    }

    private static void Cycle(ScrollCaptureSession session, int offset)
    {
        Assert.True(session.NeedsScroll);
        session.ScrollSent();
        Assert.False(session.NeedsScroll);
        for (var i = 0; i < 3; i++) session.Observe(Frame(offset));
    }
}
