using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class ScrollOverlapTests
{
    internal static byte[] Document(int width, int height)
    {
        var pixels = new byte[width * height * 4]; var random = new Random(123);
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        { var i = (y * width + x) * 4; var value = (byte)random.Next(16, 240); pixels[i] = pixels[i + 1] = pixels[i + 2] = value; pixels[i + 3] = 255; }
        return pixels;
    }
    [Fact]
    public void UniqueRowsAlignAndExactDuplicatesStopProgression()
    {
        var document = Document(40, 192); var previous = document[..(40 * 128 * 4)]; var next = document[(40 * 64 * 4)..];
        Assert.Equal(64, ScrollOverlap.Match(previous, next, 40, 128).Overlap);
        Assert.True(ScrollOverlap.Match(previous, previous, 40, 128).Duplicate);
    }
    [Fact]
    public void RepetitionBlankNoiseAndFractionalRowsStayUncertain()
    {
        var previous = new byte[40 * 128 * 4]; var next = previous.ToArray();
        for (var y = 0; y < 128; y++) for (var x = 0; x < 40; x++)
        { var i = (y * 40 + x) * 4; previous[i] = previous[i + 1] = previous[i + 2] = (byte)(y % 8 * 32); next[i] = next[i + 1] = next[i + 2] = (byte)((y + 1) % 8 * 32); previous[i + 3] = next[i + 3] = 255; }
        Assert.Null(ScrollOverlap.Match(previous, next, 40, 128).Overlap);
        Array.Fill(previous, (byte)40); Array.Fill(next, (byte)80); Assert.Null(ScrollOverlap.Match(previous, next, 40, 128).Overlap);
        var doc = Document(40, 193); previous = doc[..(40 * 128 * 4)]; next = doc[(40 * 64 * 4)..(40 * 192 * 4)];
        for (var i = 0; i < next.Length; i++) next[i] = (byte)((next[i] + doc[i + 40 * 65 * 4]) / 2);
        Assert.Null(ScrollOverlap.Match(previous, next, 40, 128).Overlap);
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); Assert.ThrowsAny<OperationCanceledException>(() => ScrollOverlap.Match(previous, next, 40, 128, cancel.Token));
        Assert.Throws<InvalidDataException>(() => ScrollOverlap.Match(previous, next, 40, 127));
    }
}
