namespace ScreenshotApp.Capture;

/// <summary>忽略啟動快捷鍵仍按住的 Ctrl，也不讓自動重複按鍵重新開啟放大鏡。</summary>
internal sealed class MagnifierInput(bool controlInitiallyDown)
{
    private bool _waitingForRelease = controlInitiallyDown;

    public bool IsVisible { get; private set; }

    public void ControlDown(bool isRepeat)
    {
        if (!_waitingForRelease && !isRepeat)
        {
            IsVisible = true;
        }
    }

    public void ControlUp(bool controlStillDown)
    {
        IsVisible = false;
        _waitingForRelease = controlStillDown;
    }

    public void Hide() => IsVisible = false;
}
