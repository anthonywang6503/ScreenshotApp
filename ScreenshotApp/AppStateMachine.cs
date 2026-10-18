namespace ScreenshotApp;

internal enum AppState
{
    /// <summary>常駐系統匣，等待快捷鍵或選單操作。</summary>
    Resident,

    /// <summary>隱藏自家視窗並擷取桌面快照。</summary>
    Capturing,

    /// <summary>顯示覆蓋層讓使用者選取區域。</summary>
    Selecting,

    /// <summary>編輯視窗開著一份文件。</summary>
    Editing,
}

/// <summary>
/// 應用程式狀態與轉換守衛，只在 UI 執行緒使用。截圖或選取進行中再次要求開始截圖時回傳 false 表示忽略，不會重入；
/// 其他不合法的轉換代表程式錯誤，直接拋出例外。
/// </summary>
internal sealed class AppStateMachine
{
    private AppState _beforeCapture = AppState.Resident;

    public AppState Current { get; private set; } = AppState.Resident;

    /// <summary>開始截圖；常駐或編輯中都可以開始，並記住開始前的狀態。</summary>
    public bool TryBeginCapture()
    {
        if (Current is not (AppState.Resident or AppState.Editing))
        {
            return false;
        }

        _beforeCapture = Current;
        Current = AppState.Capturing;
        return true;
    }

    /// <summary>快照完成，進入區域選取。</summary>
    public void BeginSelecting()
    {
        if (Current != AppState.Capturing)
        {
            throw InvalidTransition(AppState.Selecting);
        }

        Current = AppState.Selecting;
    }

    /// <summary>截圖流程結束（完成、取消或失敗），回到開始截圖前的狀態；完成時由呼叫端再進入編輯。</summary>
    public void EndCapture()
    {
        if (Current is not (AppState.Capturing or AppState.Selecting))
        {
            throw InvalidTransition(_beforeCapture);
        }

        Current = _beforeCapture;
    }

    /// <summary>開啟編輯文件；已在編輯時代表以新文件取代目前的文件。</summary>
    public void BeginEditing()
    {
        if (Current is not (AppState.Resident or AppState.Editing))
        {
            throw InvalidTransition(AppState.Editing);
        }

        Current = AppState.Editing;
    }

    /// <summary>關閉編輯視窗，回到常駐。</summary>
    public void EndEditing()
    {
        if (Current != AppState.Editing)
        {
            throw InvalidTransition(AppState.Resident);
        }

        Current = AppState.Resident;
    }

    private InvalidOperationException InvalidTransition(AppState target) => new($"無法由 {Current} 轉換到 {target}。");
}
