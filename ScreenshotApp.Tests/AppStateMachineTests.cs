namespace ScreenshotApp.Tests;

public class AppStateMachineTests
{
    [Fact]
    public void StartsResident()
    {
        Assert.Equal(AppState.Resident, new AppStateMachine().Current);
    }

    [Fact]
    public void TryBeginCapture_FromResident_EntersCapturing()
    {
        var state = new AppStateMachine();

        Assert.True(state.TryBeginCapture());
        Assert.Equal(AppState.Capturing, state.Current);
    }

    [Fact]
    public void TryBeginCapture_WhileCapturingOrSelecting_IsIgnored()
    {
        var state = new AppStateMachine();
        state.TryBeginCapture();

        Assert.False(state.TryBeginCapture());
        Assert.Equal(AppState.Capturing, state.Current);

        state.BeginSelecting();

        Assert.False(state.TryBeginCapture());
        Assert.Equal(AppState.Selecting, state.Current);
    }

    [Fact]
    public void BeginSelecting_OutsideCapturing_Throws()
    {
        var state = new AppStateMachine();

        Assert.Throws<InvalidOperationException>(state.BeginSelecting);

        state.TryBeginCapture();
        state.BeginSelecting();

        Assert.Throws<InvalidOperationException>(state.BeginSelecting);
        Assert.Equal(AppState.Selecting, state.Current);
    }

    [Fact]
    public void EndCapture_FromCapturingOrSelecting_ReturnsToResident()
    {
        var state = new AppStateMachine();
        state.TryBeginCapture();
        state.EndCapture();
        Assert.Equal(AppState.Resident, state.Current);

        state.TryBeginCapture();
        state.BeginSelecting();
        state.EndCapture();
        Assert.Equal(AppState.Resident, state.Current);
    }

    [Fact]
    public void EndCapture_WhenResident_Throws()
    {
        Assert.Throws<InvalidOperationException>(new AppStateMachine().EndCapture);
    }

    [Fact]
    public void CaptureCycle_CanRepeat()
    {
        var state = new AppStateMachine();

        for (var i = 0; i < 3; i++)
        {
            Assert.True(state.TryBeginCapture());
            state.BeginSelecting();
            state.EndCapture();
        }

        Assert.Equal(AppState.Resident, state.Current);
    }
}
