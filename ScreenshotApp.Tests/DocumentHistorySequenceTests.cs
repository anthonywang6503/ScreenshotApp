using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Tests.TestSupport;
using Xunit.Sdk;

namespace ScreenshotApp.Tests;

public class DocumentHistorySequenceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(771)]
    [InlineData(20261029)]
    public void MixedCommands_MatchIndependentSnapshotsAcrossHistoryLimitsAndExports(int seed)
    {
        StaRunner.Run(() =>
        {
            const int capacity = 100;
            var source = BitmapSource.Create(80, 60, 96, 96, PixelFormats.Bgr32, null, new byte[80 * 60 * 4], 80 * 4);
            source.Freeze();
            var document = new ImageDocument(source);
            var random = new Random(seed);

            // 預期值保存完整的標註／裁切快照，以游標移動；不呼叫命令的 Undo 計算期望內容。
            var states = new List<Snapshot> { new(0, [], new PixelRect(0, 0, 80, 60)) };
            var cursor = 0;
            var nextVersion = 1;
            var exportedVersion = 0;
            var pendingExportVersion = 0;
            var nextId = 1;
            var operation = 0;

            // 保證跨過容量上限、走到兩端，以及在復原後分支；不依賴亂數剛好涵蓋這些邊界。
            for (var i = 0; i < capacity + 10; i++)
            {
                Add();
            }

            for (var i = 0; i <= capacity; i++)
            {
                Undo();
            }

            for (var i = 0; i <= capacity; i++)
            {
                Redo();
            }
            Undo();
            Undo();
            pendingExportVersion = states[cursor].Version;
            Undo();
            Add();
            Export(pendingExportVersion);

            for (var i = 0; i < 1500; i++)
            {
                var current = states[cursor];
                var annotations = current.Annotations.ToList();
                switch (random.Next(11))
                {
                    case 0:
                        Add();
                        break;
                    case 1 when annotations.Count > 0:
                    {
                        var index = random.Next(annotations.Count);
                        var removed = annotations[index];
                        annotations.RemoveAt(index);
                        Execute(new RemoveAnnotationCommand(document, removed), annotations.ToArray(), current.Crop);
                        break;
                    }
                    case 2 when annotations.Count > 0:
                    {
                        var index = random.Next(annotations.Count);
                        var before = (StepNumberAnnotation)annotations[index];
                        var after = before with { Number = random.Next(1, 1000), Color = Colors.Blue };
                        annotations[index] = after;
                        Execute(new ReplaceAnnotationCommand(document, before, after), annotations.ToArray(), current.Crop);
                        break;
                    }
                    case 3 when annotations.Count > 1:
                    {
                        var from = random.Next(annotations.Count);
                        var to = random.Next(annotations.Count);
                        var moved = annotations[from];
                        annotations.RemoveAt(from);
                        annotations.Insert(to, moved);
                        Execute(new ReorderAnnotationCommand(document, moved.Id, from, to), annotations.ToArray(), current.Crop);
                        break;
                    }
                    case 4:
                    {
                        var x = random.Next(80);
                        var y = random.Next(60);
                        var crop = new PixelRect(x, y, random.Next(1, 81 - x), random.Next(1, 61 - y));
                        Execute(new SetCropCommand(document, current.Crop, crop), current.Annotations, crop);
                        break;
                    }
                    case 5 when annotations.Count > 0:
                    {
                        var commands = annotations.Select(annotation => (IEditCommand)new RemoveAnnotationCommand(document, annotation)).ToArray();
                        Execute(new CompositeCommand(commands), [], current.Crop);
                        break;
                    }
                    case 6:
                        Undo();
                        break;
                    case 7:
                        Redo();
                        break;
                    case 8:
                        pendingExportVersion = current.Version;
                        Check();
                        break;
                    case 9:
                        Export(pendingExportVersion);
                        break;
                    case 10:
                        Assert.Throws<ArgumentOutOfRangeException>(() => document.History.Execute(
                            new SetCropCommand(document, current.Crop, new PixelRect(-1, 0, 1, 1))));
                        Check();
                        break;
                    default:
                        Add();
                        break;
                }
            }

            // 混合操作後再走遍仍可復原／重做的快照，驗證每個保留狀態的實際內容。
            while (cursor > 0)
            {
                Undo();
            }

            while (cursor < states.Count - 1)
            {
                Redo();
            }

            void Add()
            {
                var current = states[cursor];
                var annotation = new StepNumberAnnotation
                {
                    Id = new Guid(nextId++, 0, 0, new byte[8]),
                    Color = Colors.Red,
                    Center = new PixelPoint(random.Next(80), random.Next(60)),
                    Radius = 8,
                    Number = random.Next(1, 1000),
                };
                Execute(new AddAnnotationCommand(document, annotation), [.. current.Annotations, annotation], current.Crop);
            }

            void Execute(IEditCommand command, Annotation[] annotations, PixelRect crop)
            {
                document.History.Execute(command);
                states.RemoveRange(cursor + 1, states.Count - cursor - 1);
                states.Add(new Snapshot(nextVersion++, annotations, crop));
                if (states.Count > capacity + 1)
                {
                    states.RemoveAt(0);
                }

                cursor = states.Count - 1;
                Check();
            }

            void Undo()
            {
                var available = cursor > 0;
                Assert.Equal(available, document.History.Undo());
                if (available)
                {
                    cursor--;
                }

                Check();
            }

            void Redo()
            {
                var available = cursor < states.Count - 1;
                Assert.Equal(available, document.History.Redo());
                if (available)
                {
                    cursor++;
                }

                Check();
            }

            void Export(int version)
            {
                document.MarkExported(version);
                exportedVersion = version;
                Check();
            }

            void Check()
            {
                operation++;
                var expected = states[cursor];
                try
                {
                    Assert.Equal(expected.Annotations, document.Annotations);
                    Assert.Equal(expected.Crop, document.CropRect);
                    Assert.Equal(expected.Version, document.History.Version);
                    Assert.Equal(exportedVersion, document.ExportedVersion);
                    Assert.Equal(expected.Version != exportedVersion, document.IsDirty);
                    Assert.Equal(cursor, document.History.UndoCount);
                    Assert.Equal(states.Count - cursor - 1, document.History.RedoCount);
                    Assert.Equal(cursor > 0, document.History.CanUndo);
                    Assert.Equal(cursor < states.Count - 1, document.History.CanRedo);
                    Assert.Same(source, document.Source);
                }
                catch (XunitException ex)
                {
                    throw new XunitException($"種子 {seed}，第 {operation} 個操作：{ex}");
                }
            }
        });
    }

    private sealed record Snapshot(int Version, Annotation[] Annotations, PixelRect Crop);
}
