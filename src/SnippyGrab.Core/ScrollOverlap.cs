namespace SnippyGrab.Core;

public sealed record ScrollMatch(int? Overlap, bool Duplicate, double Error);
public static class ScrollOverlap
{
    public static ScrollMatch Match(byte[] previous, byte[] next, int width, int height, CancellationToken cancellation = default)
    {
        if (width is < 1 or > 4096 || height is < 32 or > 4096 || (long)width * height > 8_000_000 || previous.Length != (long)width * height * 4 || next.Length != previous.Length) throw new InvalidDataException("Scrolling frames exceed supported dimensions or differ in size.");
        cancellation.ThrowIfCancellationRequested();
        if (previous.AsSpan().SequenceEqual(next)) return new(null, true, 0);
        int Gray(byte[] image, int x, int y) { var p = (y * width + x) * 4; return (image[p + 2] * 77 + image[p + 1] * 150 + image[p] * 29) >> 8; }
        var samples = new List<int>();
        for (var y = 0; y < height; y += Math.Max(1, (height + 63) / 64)) for (var x = 0; x < width; x += Math.Max(1, (width + 31) / 32)) samples.Add(Gray(next, x, y));
        var mean = samples.Average(); if (samples.Average(v => (v - mean) * (v - mean)) < 36) return new(null, false, double.PositiveInfinity);
        double Score(int overlap)
        {
            cancellation.ThrowIfCancellationRequested(); long error = 0; int count = 0;
            for (var y = 0; y < overlap; y += Math.Max(1, (overlap + 63) / 64))
                for (var x = 0; x < width; x += Math.Max(1, (width + 31) / 32)) { error += Math.Abs(Gray(previous, x, height - overlap + y) - Gray(next, x, y)); count++; }
            return error / (double)count;
        }
        var candidates = new Dictionary<int, double>(); var step = Math.Max(1, (height + 511) / 512); int minimum = Math.Max(16, height / 10), maximum = height - 8;
        for (var overlap = minimum; overlap <= maximum; overlap += step) candidates[overlap] = Score(overlap);
        var coarse = candidates.MinBy(c => c.Value).Key;
        for (var overlap = Math.Max(minimum, coarse - step); overlap <= Math.Min(maximum, coarse + step); overlap++) candidates[overlap] = Score(overlap);
        var best = candidates.MinBy(c => c.Value);
        var runner = candidates.Where(c => Math.Abs(c.Key - best.Key) >= Math.Max(8, step * 2)).Select(c => c.Value).DefaultIfEmpty(double.PositiveInfinity).Min();
        return new(best.Value <= 2 && runner - best.Value >= 3 ? best.Key : null, false, best.Value);
    }
}
