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

    [Fact]
    public void BeginEditing_FromResident_EntersEditing()
    {
        var state = new AppStateMachine();

        state.BeginEditing();

        Assert.Equal(AppState.Editing, state.Current);
    }

    [Fact]
    public void BeginEditing_WhileEditing_StaysEditing()
    {
        var state = new AppStateMachine();
        state.BeginEditing();

        state.BeginEditing();

        Assert.Equal(AppState.Editing, state.Current);
    }

    [Fact]
    public void BeginEditing_WhileCapturingOrSelecting_ThrowsAndKeepsState()
    {
        var state = new AppStateMachine();
        state.TryBeginCapture();

        Assert.Throws<InvalidOperationException>(state.BeginEditing);
        Assert.Equal(AppState.Capturing, state.Current);

        state.BeginSelecting();

        Assert.Throws<InvalidOperationException>(state.BeginEditing);
        Assert.Equal(AppState.Selecting, state.Current);
    }

    [Fact]
    public void EndEditing_FromEditing_ReturnsToResident()
    {
        var state = new AppStateMachine();
        state.BeginEditing();

        state.EndEditing();

        Assert.Equal(AppState.Resident, state.Current);
    }

    [Fact]
    public void EndEditing_WhenNotEditing_ThrowsAndKeepsState()
    {
        var state = new AppStateMachine();

        Assert.Throws<InvalidOperationException>(state.EndEditing);
        Assert.Equal(AppState.Resident, state.Current);

        state.TryBeginCapture();

        Assert.Throws<InvalidOperationException>(state.EndEditing);
        Assert.Equal(AppState.Capturing, state.Current);

        state.BeginSelecting();

        Assert.Throws<InvalidOperationException>(state.EndEditing);
        Assert.Equal(AppState.Selecting, state.Current);
    }

    [Fact]
    public void TryBeginCapture_FromEditing_EntersCapturing()
    {
        var state = new AppStateMachine();
        state.BeginEditing();

        Assert.True(state.TryBeginCapture());
        Assert.Equal(AppState.Capturing, state.Current);
    }

    [Fact]
    public void EndCapture_CaptureStartedFromEditing_ReturnsToEditing()
    {
        var state = new AppStateMachine();
        state.BeginEditing();

        Assert.True(state.TryBeginCapture());
        state.EndCapture();

        Assert.Equal(AppState.Editing, state.Current);

        Assert.True(state.TryBeginCapture());
        state.BeginSelecting();
        state.EndCapture();

        Assert.Equal(AppState.Editing, state.Current);
    }

    [Fact]
    public void EndCapture_CaptureStartedFromResidentAfterEditingEnded_ReturnsToResident()
    {
        var state = new AppStateMachine();
        state.BeginEditing();
        state.TryBeginCapture();
        state.EndCapture();
        state.EndEditing();

        Assert.True(state.TryBeginCapture());
        state.BeginSelecting();
        state.EndCapture();

        Assert.Equal(AppState.Resident, state.Current);
    }

    [Fact]
    public void TryBeginCapture_WhileCaptureStartedFromEditing_IsIgnoredAndCaptureStillReturnsToEditing()
    {
        var state = new AppStateMachine();
        state.BeginEditing();
        state.TryBeginCapture();

        Assert.False(state.TryBeginCapture());
        Assert.Equal(AppState.Capturing, state.Current);

        state.BeginSelecting();

        Assert.False(state.TryBeginCapture());
        Assert.Equal(AppState.Selecting, state.Current);

        state.EndCapture();

        Assert.Equal(AppState.Editing, state.Current);
    }

    [Fact]
    public void EndCapture_WhenEditing_ThrowsAndKeepsEditing()
    {
        var state = new AppStateMachine();
        state.BeginEditing();

        Assert.Throws<InvalidOperationException>(state.EndCapture);
        Assert.Equal(AppState.Editing, state.Current);
    }

    [Fact]
    public void CompletedCapture_ThenBeginEditing_EntersEditingFromEitherStartingState()
    {
        // 截圖完成時流程先回到開始前的狀態，再由呼叫端進入編輯；編輯中再次截圖則以新文件取代目前的文件。
        var state = new AppStateMachine();
        state.TryBeginCapture();
        state.BeginSelecting();
        state.EndCapture();
        state.BeginEditing();

        Assert.Equal(AppState.Editing, state.Current);

        state.TryBeginCapture();
        state.BeginSelecting();
        state.EndCapture();
        state.BeginEditing();

        Assert.Equal(AppState.Editing, state.Current);
    }
}
