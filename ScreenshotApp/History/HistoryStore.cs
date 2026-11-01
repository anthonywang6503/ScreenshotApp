using System.Globalization;
using System.IO;
using System.Security;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Export;
using ScreenshotApp.Resources;

namespace ScreenshotApp.History;

internal sealed record HistoryResult<T>(T? Value, string? Error = null, bool CreatedRoot = false);
internal sealed record HistoryEntry(string Id, DateTimeOffset Created, int Width, int Height, BitmapSource Thumbnail)
{
    public string Description => Strings.Format("History_Entry", Created.LocalDateTime, Width, Height);
}
internal sealed record HistoryLoaded(HistorySnapshot Snapshot, bool OriginalOnly);

/// <summary>單一背景佇列序列化全部存取；刪除後的更新不建立目錄。</summary>
internal sealed class HistoryStore
{
    private readonly object _gate = new();
    private Task _tail = Task.CompletedTask;
    private bool _enabled = true;
    private int _generation;
    private int _limit = 50;
    private static readonly Regex EntryName = new(@"^\d{8}T\d{9}Z_[0-9a-f]{32}$", RegexOptions.CultureInvariant);

    public HistoryStore(string root) => Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    public string Root { get; }
    public string? LastError { get; private set; }

    public void Configure(bool enabled, int limit)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
        {
            if (_enabled && !enabled) _generation++;
            _enabled = enabled;
            _limit = limit;
        }
    }

    public static string CreateId(DateTimeOffset created) =>
        created.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N");

    public Task<HistoryResult<bool>> AddAsync(string id, HistorySnapshot snapshot)
    {
        var generation = Generation();
        var createdRoot = false;
        return Queue<bool>(() =>
        {
            if (!CanWrite(generation)) return new(false);
            var directory = EntryPath(id);
            var rootMissing = !Directory.Exists(Root);
            Directory.CreateDirectory(Root);
            createdRoot = rootMissing;
            CheckPath(Root);
            if (Directory.Exists(directory)) throw new IOException(Strings.Get("History_AlreadyExists"));
            Directory.CreateDirectory(directory);
            PngFileService.Save(snapshot.Source, SafeFile(directory, "original.png"));
            var scale = Math.Min(1, 160.0 / Math.Max(snapshot.Source.PixelWidth, snapshot.Source.PixelHeight));
            var thumb = new TransformedBitmap(snapshot.Source, new ScaleTransform(scale, scale));
            thumb.Freeze();
            PngFileService.Save(thumb, SafeFile(directory, "thumb.png"));
            if (!WriteJson(directory, snapshot, () => CanWrite(generation)))
            {
                DeleteDirectory(directory);
                return new(false, CreatedRoot: createdRoot);
            }
            Prune(generation);
            return new(true, CreatedRoot: createdRoot);
        }, () => createdRoot);
    }

    public Task<HistoryResult<bool>> UpdateAsync(string id, HistorySnapshot snapshot)
    {
        var generation = Generation();
        return Queue<bool>(() =>
        {
            var directory = EntryPath(id);
            if (!CanWrite(generation) || !IsComplete(directory)) return new(false);
            // 損毀或未來版本不覆寫；建立時間一律沿用磁碟內容。
            var previous = HistoryJson.Deserialize(File.ReadAllText(SafeFile(directory, "document.json")), snapshot.Source);
            if (previous is null) return new(false);
            return new(WriteJson(directory, snapshot with { Created = previous.Created }, () => CanWrite(generation)));
        });
    }

    public Task<HistoryResult<IReadOnlyList<HistoryEntry>>> ListAsync() => Queue<IReadOnlyList<HistoryEntry>>(() =>
    {
        var entries = new List<HistoryEntry>();
        foreach (var directory in Directories())
        {
            if (!IsComplete(directory)) continue;
            var original = ImageFileLoader.Load(SafeFile(directory, "thumb.png"));
            if (original.Image is null) continue;
            // 只讀影像標頭取得原圖尺寸；不把每一張原圖全部解碼到記憶體。
            using var stream = File.OpenRead(SafeFile(directory, "original.png"));
            try
            {
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                var id = Path.GetFileName(directory);
                entries.Add(new(id, TimeFromId(id), frame.PixelWidth, frame.PixelHeight, original.Image));
            }
            catch (Exception ex) when (ex is NotSupportedException or IOException or ArgumentException or System.Runtime.InteropServices.COMException)
            {
                // 原圖損毀的條目不可開啟，但仍可由全部清除移除。
            }
        }
        return new(entries.OrderByDescending(e => e.Created).ThenByDescending(e => e.Id).ToArray());
    });

    public Task<HistoryResult<HistoryLoaded>> LoadAsync(string id) => Queue<HistoryLoaded>(() =>
    {
        var directory = EntryPath(id);
        if (!IsComplete(directory)) return new(null, Strings.Get("History_Missing"));
        var image = ImageFileLoader.Load(SafeFile(directory, "original.png"));
        if (image.Image is null) return new(null, image.Error);
        var snapshot = HistoryJson.Deserialize(File.ReadAllText(SafeFile(directory, "document.json")), image.Image);
        return new(new(snapshot ?? new HistorySnapshot(image.Image, [], new PixelRect(0, 0, image.Image.PixelWidth, image.Image.PixelHeight), TimeFromId(id)), snapshot is null));
    });

    public Task<HistoryResult<bool>> DeleteAsync(string id) => Queue<bool>(() =>
    {
        DeleteDirectory(EntryPath(id));
        return new(true);
    });

    public Task<HistoryResult<bool>> ClearAsync() => Queue<bool>(() =>
    {
        foreach (var directory in Directories()) DeleteDirectory(directory);
        return new(true);
    });

    public Task<HistoryResult<bool>> MaintainAsync()
    {
        var generation = Generation();
        return Queue<bool>(() =>
        {
            if (!CanWrite(generation)) return new(false);
            foreach (var directory in Directories())
            {
                if (!CanWrite(generation)) return new(false);
                if (!IsComplete(directory)) DeleteDirectory(directory);
                else foreach (var file in Directory.EnumerateFiles(directory, "*.tmp"))
                {
                    if (!CanWrite(generation)) return new(false);
                    CheckPath(file);
                    File.Delete(file);
                }
            }
            Prune(generation);
            return new(true);
        });
    }

    public Task Pending { get { lock (_gate) return _tail; } }

    private Task<HistoryResult<T>> Queue<T>(Func<HistoryResult<T>> action, Func<bool>? createdRoot = null)
    {
        lock (_gate)
        {
            var task = _tail.ContinueWith(_ =>
            {
                try
                {
                    var result = action();
                    if (result.Error is not null) LastError = result.Error;
                    return result;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException
                    or OutOfMemoryException or ArgumentException or FormatException or NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException)
                {
                    LastError = ex is OutOfMemoryException ? Strings.Get("ImageLoad_OutOfMemory") : ErrorText.Describe(ex);
                    return new HistoryResult<T>(default, LastError, createdRoot?.Invoke() == true);
                }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            _tail = task;
            return task;
        }
    }

    private int Generation() { lock (_gate) return _enabled ? _generation : -1; }
    private bool CanWrite(int generation) { lock (_gate) return _enabled && generation == _generation; }

    private void Prune(int generation)
    {
        int limit;
        lock (_gate) limit = _limit;
        foreach (var directory in Directories().Where(IsComplete).OrderByDescending(Path.GetFileName, StringComparer.Ordinal).Skip(limit))
        {
            if (!CanWrite(generation)) return;
            DeleteDirectory(directory);
        }
    }

    private IEnumerable<string> Directories()
    {
        CheckPath(Root);
        return Directory.Exists(Root) ? Directory.GetDirectories(Root).Where(path => EntryName.IsMatch(Path.GetFileName(path))) : [];
    }

    private string EntryPath(string id)
    {
        if (!EntryName.IsMatch(id)) throw new ArgumentException(Strings.Get("History_InvalidId"));
        var path = Path.GetFullPath(Path.Combine(Root, id));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(Strings.Get("History_InvalidPath"));
        CheckPath(path);
        return path;
    }

    private static DateTimeOffset TimeFromId(string id) => new(DateTime.ParseExact(id[..19], "yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));

    private static string SafeFile(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        CheckPath(path);
        return path;
    }

    private static bool IsComplete(string directory) => Directory.Exists(directory)
        && File.Exists(SafeFile(directory, "original.png")) && File.Exists(SafeFile(directory, "thumb.png"))
        && File.Exists(SafeFile(directory, "document.json"));

    private static void CheckPath(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException(Strings.Get("History_LinkRejected"));
        }
    }

    private static bool WriteJson(string directory, HistorySnapshot snapshot, Func<bool> canCommit)
    {
        var target = SafeFile(directory, "document.json");
        var temp = SafeFile(directory, "document.json.tmp");
        try
        {
            File.WriteAllText(temp, HistoryJson.Serialize(snapshot));
            if (!canCommit()) return false;
            File.Move(temp, target, overwrite: true);
            return true;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private void DeleteDirectory(string directory)
    {
        // 不遞迴刪除，且先驗證所有子項目，避免跟隨使用者放入的連結。
        EntryPath(Path.GetFileName(directory));
        if (!Directory.Exists(directory)) return;
        var files = Directory.GetFiles(directory);
        foreach (var file in files) CheckPath(file);
        if (Directory.GetDirectories(directory).Length != 0) throw new IOException(Strings.Get("History_UnexpectedDirectory"));
        foreach (var file in files) File.Delete(file);
        Directory.Delete(directory);
    }
}
