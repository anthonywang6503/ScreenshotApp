using System.Text.Json.Nodes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Export;
using ScreenshotApp.History;
using ScreenshotApp.Settings;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

public sealed class HistoryTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    internal static ImageDocument Document()
    {
        var pixels = new byte[120 * 90 * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = (byte)(i / 4); pixels[i + 1] = (byte)(i / 480); pixels[i + 2] = 90;
        }
        var source = BitmapSource.Create(120, 90, 96, 96, PixelFormats.Bgr32, null, pixels, 480);
        source.Freeze();
        return new(source);
    }

    internal static Annotation[] Annotations() =>
    [
        new RectangleAnnotation { Color = Colors.Red, Rect = new(4, 4, 12, 16), StrokeWidth = 2 },
        new EllipseAnnotation { Color = Colors.Blue, Rect = new(20, 4, 12, 16), StrokeWidth = 3 },
        new LineAnnotation { Color = Colors.Green, Start = new(10, 30), End = new(50, 30), StrokeWidth = 2 },
        new ArrowAnnotation { Color = Colors.Yellow, Start = new(10, 40), End = new(50, 45), StrokeWidth = 4 },
        new RulerAnnotation { Color = Colors.Red, Start = new(10, 65), End = new(90, 65), StrokeWidth = 1, ImageBounds = new(0, 0, 120, 90) },
        new TextAnnotation { Color = Color.FromArgb(130, 40, 50, 60), Origin = new(60, 4), Text = "測試\nAB", FontFamily = "Segoe UI", FontSize = 12 },
        new StepNumberAnnotation { Color = Colors.Red, Center = new(70, 40), Number = 3, Radius = 10 },
        new MosaicAnnotation { Color = Colors.Transparent, Rect = new(90, 4, 20, 20), Strength = 4 },
        new BlurAnnotation { Color = Colors.Transparent, Rect = new(90, 30, 20, 20), Strength = 3 },
    ];

    internal static ImageDocument AnnotatedDocument()
    {
        var document = Document();
        foreach (var annotation in Annotations()) document.History.Execute(new AddAnnotationCommand(document, annotation));
        document.History.Execute(new SetCropCommand(document, document.Bounds, new PixelRect(2, 2, 114, 84)));
        return document;
    }

    [Fact]
    public void AT15_AllKindsAndCrop_RoundTripWithEmptyUndoAndIdenticalOutput() => StaRunner.Run(() =>
    {
        var original = AnnotatedDocument();
        var snapshot = HistorySnapshot.Capture(original, DateTimeOffset.UtcNow);
        var restored = HistoryJson.Deserialize(HistoryJson.Serialize(snapshot), original.Source)!.Restore();
        Assert.Equal(original.Annotations, restored.Annotations);
        Assert.Equal(original.CropRect, restored.CropRect);
        Assert.False(restored.IsDirty);
        Assert.False(restored.History.CanUndo);
        Assert.False(restored.History.CanRedo);
        Assert.Equal(PngFileService.Encode(ImageRenderer.Render(original)), PngFileService.Encode(ImageRenderer.Render(restored)));
        restored.History.Execute(new AddAnnotationCommand(restored, Annotations()[0]));
        Assert.True(restored.IsDirty);
        restored.History.Undo();
        Assert.False(restored.IsDirty);
        Assert.True(original.IsDirty);
    });

    [Theory]
    [InlineData("future")]
    [InlineData("unknown")]
    [InlineData("size")]
    [InlineData("crop")]
    [InlineData("dimensions")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("overflow")]
    public void AT15_InvalidData_RejectsWholeDocument(string fault) => StaRunner.Run(() =>
    {
        var doc = AnnotatedDocument();
        var root = JsonNode.Parse(HistoryJson.Serialize(HistorySnapshot.Capture(doc, DateTimeOffset.UtcNow)))!;
        switch (fault)
        {
            case "future": root["Version"] = 2; break;
            case "unknown": root["Annotations"]![3]!["Kind"] = "future"; break;
            case "size": root["Annotations"]![1]!["Size"] = 0; break;
            case "crop": root["Crop"]![2] = 999; break;
            case "dimensions": root["Width"] = 121; break;
            case "duplicate": root["Annotations"]![1]!["Id"] = root["Annotations"]![0]!["Id"]!.DeepClone(); break;
            case "missing": root["Annotations"]![2]!.AsObject().Remove("Geometry"); break;
            case "null": root["Annotations"]![0] = null; break;
            case "overflow": root["Crop"]![0] = int.MaxValue; break;
        }
        Assert.Null(HistoryJson.Deserialize(root.ToJsonString(), doc.Source));
    });

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void AT15_MalformedJson_IsRejected(string json) => StaRunner.Run(() => Assert.Null(HistoryJson.Deserialize(json, Document().Source)));

    [Fact]
    public async Task AT15_RestartAndUpdates_PreserveOriginalTimeAndNewestState()
    {
        var store = new HistoryStore(_temp.Combine("history"));
        var doc = Document();
        var time = DateTimeOffset.UtcNow.AddMinutes(-1);
        var id = HistoryStore.CreateId(time);
        var added = await store.AddAsync(id, HistorySnapshot.Capture(doc, time));
        Assert.True(added.Value); Assert.True(added.CreatedRoot); Assert.Null(added.Error);
        var pending = new List<Task<HistoryResult<bool>>>();
        foreach (var annotation in Annotations())
        {
            doc.History.Execute(new AddAnnotationCommand(doc, annotation));
            pending.Add(store.UpdateAsync(id, HistorySnapshot.Capture(doc, DateTimeOffset.UtcNow)));
        }
        foreach (var result in await Task.WhenAll(pending)) Assert.True(result.Value);
        var restarted = new HistoryStore(store.Root);
        var loaded = await restarted.LoadAsync(id);
        Assert.Null(loaded.Error);
        Assert.False(loaded.Value!.OriginalOnly);
        Assert.Equal(time, loaded.Value.Snapshot.Created);
        Assert.Equal(doc.Annotations, loaded.Value.Snapshot.Annotations);
        var entry = Assert.Single((await restarted.ListAsync()).Value!);
        Assert.Equal(id, entry.Id); Assert.Equal(120, entry.Width); Assert.Equal(90, entry.Height);
        Assert.True(entry.Thumbnail.IsFrozen);
        Assert.True(entry.Thumbnail.PixelWidth <= 160);
    }

    [Fact]
    public async Task AT15_RetentionDeleteClearAndQueuedUpdates_DoNotResurrect()
    {
        var store = new HistoryStore(_temp.Combine("history"));
        store.Configure(true, 2);
        var snapshot = HistorySnapshot.Capture(Document(), DateTimeOffset.UtcNow);
        var ids = Enumerable.Range(0, 3).Select(i => HistoryStore.CreateId(snapshot.Created.AddMinutes(i))).ToArray();
        foreach (var id in ids) Assert.True((await store.AddAsync(id, snapshot)).Value);
        Assert.Equal(ids.Skip(1).Reverse(), (await store.ListAsync()).Value!.Select(e => e.Id));
        Assert.False((await store.UpdateAsync(ids[0], snapshot)).Value);
        var delete = store.DeleteAsync(ids[1]);
        var update = store.UpdateAsync(ids[1], snapshot);
        Assert.True((await delete).Value); Assert.False((await update).Value);
        var clear = store.ClearAsync();
        var updateAfterClear = store.UpdateAsync(ids[2], snapshot);
        Assert.True((await clear).Value); Assert.False((await updateAfterClear).Value);
        Assert.Empty((await store.ListAsync()).Value!);
        Assert.Empty(Directory.GetDirectories(store.Root));
        Assert.True(Directory.Exists(store.Root));
        Assert.False((await store.AddAsync(HistoryStore.CreateId(snapshot.Created), snapshot)).CreatedRoot);
    }

    [Fact]
    public async Task AT15_DisabledWrites_AreNotBackfilledAfterEnable()
    {
        var store = new HistoryStore(_temp.Combine("history"));
        var snapshot = HistorySnapshot.Capture(Document(), DateTimeOffset.UtcNow);
        var id = HistoryStore.CreateId(snapshot.Created);
        store.Configure(false, 50);
        var add = store.AddAsync(id, snapshot);
        store.Configure(true, 50);
        Assert.False((await add).Value);
        Assert.False(Directory.Exists(store.Root));
        Assert.True((await store.AddAsync(id, snapshot)).Value);
        var json = File.ReadAllText(Path.Combine(store.Root, id, "document.json"));
        store.Configure(false, 50);
        var update = store.UpdateAsync(id, snapshot with { Annotations = Annotations() });
        store.Configure(true, 50);
        Assert.False((await update).Value);
        Assert.Equal(json, File.ReadAllText(Path.Combine(store.Root, id, "document.json")));
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("future")]
    [InlineData("unknown")]
    public async Task AT15_CorruptJson_RestoresOnlyOriginalAndCannotBeOverwritten(string fault)
    {
        var store = new HistoryStore(_temp.Combine("history"));
        var snapshot = HistorySnapshot.Capture(Document(), DateTimeOffset.UtcNow);
        var id = HistoryStore.CreateId(snapshot.Created);
        await store.AddAsync(id, snapshot);
        var path = Path.Combine(store.Root, id, "document.json");
        var bad = fault == "corrupt" ? "{" : fault == "future" ? "{\"Version\":99}" :
            HistoryJson.Serialize(snapshot with { Annotations = Annotations() }).Replace("rectangle", "unknown");
        File.WriteAllText(path, bad);
        var loaded = await store.LoadAsync(id);
        Assert.Null(loaded.Error); Assert.True(loaded.Value!.OriginalOnly);
        Assert.Empty(loaded.Value.Snapshot.Annotations);
        Assert.Equal(new PixelRect(0, 0, 120, 90), loaded.Value.Snapshot.Crop);
        Assert.False((await store.UpdateAsync(id, snapshot)).Value);
        Assert.Equal(bad, File.ReadAllText(path));
    }

    [Fact]
    public async Task AT15_IncompleteEntry_IsSkippedAndCleanedOnStartup()
    {
        var store = new HistoryStore(_temp.Combine("history"));
        var id = HistoryStore.CreateId(DateTimeOffset.UtcNow);
        var dir = Path.Combine(store.Root, id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "original.png.tmp"), "partial");
        Assert.Empty((await store.ListAsync()).Value!);
        Assert.True((await store.MaintainAsync()).Value);
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public async Task AT15_ReadOnlyJson_UpdateReturnsErrorAndPreservesContent()
    {
        var store = new HistoryStore(_temp.Combine("history"));
        var snapshot = HistorySnapshot.Capture(Document(), DateTimeOffset.UtcNow);
        var id = HistoryStore.CreateId(snapshot.Created);
        await store.AddAsync(id, snapshot);
        var path = Path.Combine(store.Root, id, "document.json");
        var before = File.ReadAllBytes(path);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        var result = await store.UpdateAsync(id, snapshot with { Annotations = Annotations() });
        Assert.NotNull(result.Error); Assert.False(result.Value);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Fact]
    public async Task AT15_BlockedRoot_ReturnsErrorWithoutChangingDocument()
    {
        var path = _temp.Combine("history"); File.WriteAllText(path, "file");
        var store = new HistoryStore(path);
        var doc = Document();
        var snapshot = HistorySnapshot.Capture(doc, DateTimeOffset.UtcNow);
        var result = await store.AddAsync(HistoryStore.CreateId(snapshot.Created), snapshot);
        Assert.NotNull(result.Error); Assert.False(result.Value);
        Assert.Equal(120, doc.PixelWidth); Assert.False(doc.IsDirty);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("C:\\outside")]
    [InlineData("20261102T000000000Z_../../outside")]
    public async Task AT15_PathEscape_IsRejected(string id)
    {
        var store = new HistoryStore(_temp.Combine("history"));
        Assert.NotNull((await store.DeleteAsync(id)).Error);
        Assert.NotNull((await store.LoadAsync(id)).Error);
        Assert.False(Directory.Exists(store.Root));
    }

    [Fact]
    public async Task AT15_CorruptOriginal_CannotOpen()
    {
        var store = new HistoryStore(_temp.Combine("history"));
        var snapshot = HistorySnapshot.Capture(Document(), DateTimeOffset.UtcNow);
        var id = HistoryStore.CreateId(snapshot.Created);
        await store.AddAsync(id, snapshot);
        File.WriteAllText(Path.Combine(store.Root, id, "original.png"), "corrupt");
        Assert.Null((await store.LoadAsync(id)).Value);
        Assert.Empty((await store.ListAsync()).Value!);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("501")]
    [InlineData("2.5")]
    [InlineData("null")]
    [InlineData("\"50\"")]
    public void AT08_InvalidHistoryLimit_ResetsOnlyLimit(string value)
    {
        var store = new SettingsStore(_temp.Combine("settings.json"));
        File.WriteAllText(store.FilePath, "{\"HistoryEnabled\":false,\"HistoryLimit\":" + value + ",\"DefaultFontSize\":24}");
        var result = store.Load();
        Assert.Equal(50, result.Settings.HistoryLimit); Assert.False(result.Settings.HistoryEnabled);
        Assert.Equal(24, result.Settings.DefaultFontSize);
        Assert.Equal([SettingsStore.HistoryLimitField], result.InvalidFields);
    }

    [Fact]
    public void AT08_HistoryDefaultsAndRoundTrip()
    {
        var store = new SettingsStore(_temp.Combine("settings.json"));
        File.WriteAllText(store.FilePath, "{}");
        Assert.True(store.Load().Settings.HistoryEnabled); Assert.Equal(50, store.Load().Settings.HistoryLimit);
        Assert.Empty(store.Load().InvalidFields);
        var settings = new AppSettings { HistoryEnabled = false, HistoryLimit = 1 };
        Assert.True(store.Save(settings).Succeeded); Assert.Equal(settings, store.Load().Settings);
        File.WriteAllText(store.FilePath, "{\"HistoryEnabled\":0,\"HistoryLimit\":500}");
        var loaded = store.Load();
        Assert.True(loaded.Settings.HistoryEnabled); Assert.Equal(500, loaded.Settings.HistoryLimit);
        Assert.Equal([SettingsStore.HistoryEnabledField], loaded.InvalidFields);
    }
}
