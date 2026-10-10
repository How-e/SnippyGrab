namespace SnippyGrab.Core;

public sealed record ScrollMatch(int? Overlap, bool Duplicate, double Error);
public static class ScrollOverlap
{
    public static int TrimmedHeight(int height, int top, int bottom)
    {
        if (height < 32 || top < 0 || bottom < 0 || (long)top + bottom > height - 32)
            throw new InvalidDataException("Trims must leave at least 32 pixels of viewport height.");
        return height - top - bottom;
    }
    public static ScrollMatch Match(byte[] previous, byte[] next, int width, int height, CancellationToken cancellation = default)
    {
        if (width is < 1 or > 4096 || height is < 32 or > 4096 || (long)width * height > 8_000_000 || previous.Length != (long)width * height * 4 || next.Length != previous.Length) throw new InvalidDataException("Scrolling frames exceed supported dimensions or differ in size.");
        cancellation.ThrowIfCancellationRequested();
        if (previous.AsSpan().SequenceEqual(next)) return new(null, true, 0);
        // Retain every vertical offset; skipping rows can miss a perfect narrow join.
        // Cache at most 32 luminance columns per row to bound both work and memory.
        var columnStep = Math.Max(1, (width + 31) / 32); var columns = (width + columnStep - 1) / columnStep;
        byte[] Luminance(byte[] image)
        {
            var gray = new byte[height * columns];
            for (var y = 0; y < height; y++)
            {
                cancellation.ThrowIfCancellationRequested();
                for (int x = 0, column = 0; x < width; x += columnStep, column++)
                { var p = (y * width + x) * 4; gray[y * columns + column] = (byte)((image[p + 2] * 77 + image[p + 1] * 150 + image[p] * 29) >> 8); }
            }
            return gray;
        }
        var previousGray = Luminance(previous); var nextGray = Luminance(next);
        var samples = new List<int>();
        for (var y = 0; y < height; y += Math.Max(1, (height + 63) / 64)) for (var x = 0; x < columns; x++) samples.Add(nextGray[y * columns + x]);
        var mean = samples.Average(); if (samples.Average(v => (v - mean) * (v - mean)) < 36) return new(null, false, double.PositiveInfinity);
        double Score(int overlap)
        {
            cancellation.ThrowIfCancellationRequested(); long error = 0; int count = 0;
            for (var y = 0; y < overlap; y += Math.Max(1, (overlap + 63) / 64))
                for (var x = 0; x < columns; x++) { error += Math.Abs(previousGray[(height - overlap + y) * columns + x] - nextGray[y * columns + x]); count++; }
            return error / (double)count;
        }
        var candidates = new Dictionary<int, double>(); int minimum = Math.Max(16, height / 10), maximum = height - 8;
        for (var overlap = minimum; overlap <= maximum; overlap++) candidates[overlap] = Score(overlap);
        var best = candidates.MinBy(c => c.Value);
        var runner = candidates.Where(c => Math.Abs(c.Key - best.Key) >= 8).Select(c => c.Value).DefaultIfEmpty(double.PositiveInfinity).Min();
        return new(best.Value <= 2 && runner - best.Value >= 3 ? best.Key : null, false, best.Value);
    }
}
