namespace ScreenshotApp.Capture;

/// <summary>穩定影格與自動捲動節奏，不依賴桌面、計時器或輸入注入。</summary>
internal sealed class ScrollCaptureSession(ScrollFrame first)
{
    public ScrollStitcher Stitcher { get; } = new(first);
    public bool Automatic { get; private set; }
    public bool Complete { get; private set; }
    public bool NeedsScroll => Automatic && !_awaitingFrame;
    public bool Unstable { get; private set; }
    public bool NoScrollResponse { get; private set; }
    private ScrollFrame? _sample;
    private int _stableSamples;
    private int _attemptSamples;
    private int _unchangedAttempts;
    private bool _awaitingFrame;
    private bool _advancedAutomatically;

    public void StartAutomatic()
    {
        if (Stitcher.AtLimit) return;
        Automatic = true;
        NoScrollResponse = false;
        _unchangedAttempts = 0;
        _advancedAutomatically = false;
    }

    public void StopAutomatic()
    {
        Automatic = false;
        _awaitingFrame = false;
    }

    public void ScrollSent()
    {
        _awaitingFrame = true;
        _sample = null;
        _stableSamples = 0;
        _attemptSamples = 0;
    }

    public StitchStatus? Observe(ScrollFrame frame)
    {
        if (Stitcher.AtLimit) return StitchStatus.LimitReached;
        _attemptSamples++;
        _stableSamples = _sample is not null && frame.SamePixels(_sample) ? _stableSamples + 1 : 1;
        _sample = frame;
        if (_stableSamples < 3)
        {
            if (_attemptSamples >= 27)
            {
                Unstable = true;
                StopAutomatic();
            }
            return null;
        }
        Unstable = false;
        _attemptSamples = 0;
        var result = Stitcher.Append(frame);
        if (Automatic && _awaitingFrame)
        {
            _awaitingFrame = false;
            if (result == StitchStatus.Unchanged)
            {
                if (++_unchangedAttempts >= 3)
                {
                    // 完全沒有捲動成功也可能是不支援訊息注入，不將其宣稱為到底。
                    Complete = _advancedAutomatically;
                    NoScrollResponse = !Complete;
                    StopAutomatic();
                }
            }
            else if (result == StitchStatus.Appended)
            {
                _advancedAutomatically = true;
                _unchangedAttempts = 0;
            }
        }
        if (result is StitchStatus.Backward or StitchStatus.CannotAlign or StitchStatus.LimitReached) StopAutomatic();
        return result;
    }
}
