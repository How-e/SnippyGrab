namespace SnippyGrab.Core;

public sealed record TimingSummary(int Count, double MinimumMs, double MedianMs, double P95Ms, double MaximumMs)
{
    public static TimingSummary From(IEnumerable<double> samples)
    {
        var sorted = samples.Order().ToArray();
        if (sorted.Length == 0 || sorted.Any(n => !double.IsFinite(n) || n < 0)) throw new ArgumentException("Finite nonnegative samples required.");
        double Percentile(double p) => sorted[Math.Max(0, (int)Math.Ceiling(p * sorted.Length) - 1)];
        return new(sorted.Length, sorted[0], Percentile(.5), Percentile(.95), sorted[^1]);
    }
}
