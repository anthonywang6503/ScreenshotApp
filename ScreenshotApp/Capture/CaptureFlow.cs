using System.Windows.Media.Imaging;
using ScreenshotApp.Platform;

namespace ScreenshotApp.Capture;

/// <summary>截圖流程需要的平台步驟；正式環境操作真實的視窗與螢幕，測試以替身取代。</summary>
internal interface ICaptureEnvironment
{
    /// <summary>隱藏自家可能遮住桌面的視窗並等待畫面更新；回傳的物件釋放時恢復這些視窗。</summary>
    IDisposable HideOwnWindows();

    /// <summary>擷取全部顯示器；失敗時拋出 <see cref="CaptureException"/>。</summary>
    DesktopSnapshot TakeSnapshot();

    /// <summary>顯示覆蓋層讓使用者選取；取消時回傳 null，選取期間顯示設定變更時拋出 <see cref="CaptureException"/>。</summary>
    Task<SelectionResult?> SelectRegionAsync(DesktopSnapshot snapshot);
}

internal enum CaptureStatus
{
    /// <summary>截圖或選取已在進行中，這次要求被忽略。</summary>
    Ignored,

    Cancelled,

    Failed,

    Completed,
}

/// <summary>截圖流程的結果：完成時附裁切後的圖片，失敗時附原因。</summary>
internal sealed record CaptureOutcome(CaptureStatus Status, BitmapSource? Image = null, CaptureException? Error = null);

/// <summary>
/// 截圖流程：隱藏自家視窗 → 快照 → 區域選取 → 裁切。截圖或選取進行中的要求一律忽略；
/// 完成、取消、失敗或未預期例外的任何結束路徑都會恢復視窗並回到常駐狀態。只在 UI 執行緒使用。
/// </summary>
internal sealed class CaptureFlow
{
    private readonly AppStateMachine _state;
    private readonly ICaptureEnvironment _environment;

    public CaptureFlow(AppStateMachine state, ICaptureEnvironment environment)
    {
        _state = state;
        _environment = environment;
    }

    public async Task<CaptureOutcome> RunAsync()
    {
        if (!_state.TryBeginCapture())
        {
            return new CaptureOutcome(CaptureStatus.Ignored);
        }

        try
        {
            using (_environment.HideOwnWindows())
            {
                var snapshot = _environment.TakeSnapshot();
                _state.BeginSelecting();
                var selection = await _environment.SelectRegionAsync(snapshot);
                return selection is { } chosen
                    ? new CaptureOutcome(CaptureStatus.Completed, snapshot.Crop(chosen))
                    : new CaptureOutcome(CaptureStatus.Cancelled);
            }
        }
        catch (CaptureException ex)
        {
            return new CaptureOutcome(CaptureStatus.Failed, Error: ex);
        }
        finally
        {
            _state.EndCapture();
        }
    }
}
