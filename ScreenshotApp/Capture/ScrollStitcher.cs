namespace ScreenshotApp.Capture;

internal enum StitchStatus { Appended, Unchanged, Backward, CannotAlign, LimitReached }

/// <summary>只接受唯一且逐像素一致的向下重疊。保留新列，避免每次附加都複製整張長圖。</summary>
internal sealed class ScrollStitcher
{
    public const int MaximumHeight = 32_768;
    public const long MaximumPixels = 100_000_000;
    private readonly List<byte[]> _chunks = [];
    private readonly int _heightLimit;
    private ScrollFrame _previous;
    private int? _header;
    private int _footer;

    public int Width => _previous.Width;
    public int Height { get; private set; }
    public bool AtLimit => Height == _heightLimit;

    public static int HeightLimit(int width) => width <= 0 ? 0 : (int)Math.Min(MaximumHeight, MaximumPixels / width);

    public ScrollStitcher(ScrollFrame first)
    {
        _heightLimit = HeightLimit(first.Width);
        if (first.Height > _heightLimit) throw new ArgumentException("影格超過長截圖上限。");
        _previous = first;
        Height = first.Height;
    }

    public StitchStatus Append(ScrollFrame next)
    {
        if (next.Width != Width || next.Height != _previous.Height) return StitchStatus.CannotAlign;
        if (AtLimit) return StitchStatus.LimitReached;
        if (_previous.SamePixels(next)) return StitchStatus.Unchanged;

        var top = _header ?? 0;
        var bottom = _footer;
        if (_header is null)
        {
            while (top < next.Height && _previous.SameRow(top, next, top)) top++;
            while (bottom < next.Height - top && _previous.SameRow(next.Height - bottom - 1, next, next.Height - bottom - 1)) bottom++;
        }
        else
        {
            for (var y = 0; y < top; y++)
                if (!_previous.SameRow(y, next, y)) return StitchStatus.CannotAlign;
            for (var y = next.Height - bottom; y < next.Height; y++)
                if (!_previous.SameRow(y, next, y)) return StitchStatus.CannotAlign;
        }

        var length = next.Height - top - bottom;
        var forward = FindOverlaps(_previous, next, top, length);
        var backward = FindOverlaps(next, _previous, top, length);
        if (forward.Count != 1 || backward.Count > 0)
            return forward.Count == 0 && backward.Count == 1 ? StitchStatus.Backward : StitchStatus.CannotAlign;

        var shift = length - forward[0];
        var rows = Math.Min(shift, _heightLimit - Height);
        if (_header is null)
        {
            _header = top;
            _footer = bottom;
            _chunks.Add(_previous.Pixels[..((next.Height - bottom) * next.Stride)]);
        }
        var start = (next.Height - bottom - shift) * next.Stride;
        _chunks.Add(next.Pixels[start..(start + rows * next.Stride)]);
        Height += rows;
        _previous = next;
        return AtLimit ? StitchStatus.LimitReached : StitchStatus.Appended;
    }

    // KMP 在列雜湊上尋找所有後綴／前綴重疊，再逐像素驗證候選；重複圖樣不可任選一個位移。
    private static List<int> FindOverlaps(ScrollFrame before, ScrollFrame after, int top, int length)
    {
        var result = new List<int>();
        var minimum = Math.Max(2, Math.Min(32, length / 4));
        if (length <= minimum) return result;
        var pattern = after.Rows.AsSpan(top, length);
        var prefix = new int[length];
        for (var i = 1; i < length; i++)
        {
            var j = prefix[i - 1];
            while (j > 0 && pattern[i] != pattern[j]) j = prefix[j - 1];
            if (pattern[i] == pattern[j]) j++;
            prefix[i] = j;
        }
        var matched = 0;
        for (var i = 0; i < length; i++)
        {
            var row = before.Rows[top + i];
            while (matched > 0 && (matched == length || row != pattern[matched])) matched = prefix[matched - 1];
            if (row == pattern[matched]) matched++;
        }
        if (matched == length) matched = prefix[matched - 1];
        while (matched >= minimum)
        {
            var valid = true;
            for (var y = 0; y < matched && valid; y++)
                valid = before.SameRow(top + length - matched + y, after, top + y);
            if (valid) result.Add(matched);
            if (result.Count > 1) break;
            matched = prefix[matched - 1];
        }
        return result;
    }

    public byte[] GetPixels()
    {
        if (_header is null) return (byte[])_previous.Pixels.Clone();
        var pixels = new byte[checked(Width * Height * 4)];
        var offset = 0;
        foreach (var chunk in _chunks)
        {
            chunk.CopyTo(pixels, offset);
            offset += chunk.Length;
        }
        _previous.Pixels.AsSpan((_previous.Height - _footer) * _previous.Stride).CopyTo(pixels.AsSpan(offset));
        return pixels;
    }

    public byte[] GetPreviewPixels(int maximumWidth, int maximumHeight, out int width, out int height)
    {
        var scale = Math.Min(1d, Math.Min((double)maximumWidth / Width, (double)maximumHeight / Height));
        width = Math.Max(1, (int)(Width * scale));
        height = Math.Max(1, (int)(Height * scale));
        var preview = new byte[width * height * 4];
        var chunks = _header is null ? [_previous.Pixels] : _chunks.ToArray();
        var chunkIndex = 0;
        var firstRow = 0;
        for (var y = 0; y < height; y++)
        {
            var sourceY = (int)((long)y * Height / height);
            while (chunkIndex < chunks.Length && sourceY >= firstRow + chunks[chunkIndex].Length / _previous.Stride)
                firstRow += chunks[chunkIndex++].Length / _previous.Stride;
            var source = chunkIndex < chunks.Length ? chunks[chunkIndex] : _previous.Pixels;
            var row = chunkIndex < chunks.Length ? sourceY - firstRow : _previous.Height - _footer + sourceY - firstRow;
            for (var x = 0; x < width; x++)
                source.AsSpan(row * _previous.Stride + (int)((long)x * Width / width) * 4, 4).CopyTo(preview.AsSpan((y * width + x) * 4, 4));
        }
        return preview;
    }
}
