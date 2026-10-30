namespace SnippyGrab.App.Services;

internal sealed record DesignComparison(string Summary, string SourceUrl, string ExpectedSourceRevision, string ImplementationRevision, bool SameViewport, bool SameRoute, bool SameTheme, double? MeanChannelDifference, long? ChangedPixels, string[] MissingTargets, string[] Limitations)
{
    internal static DesignComparison Create(DesignDocument expected, DesignDocument actual, byte[] rendered)
    {
        var sameViewport = expected.Width == actual.Width && expected.Height == actual.Height
            && (expected.Source.ViewportWidth == 0 || actual.Source.ViewportWidth == 0 || expected.Source.ViewportWidth == actual.Source.ViewportWidth)
            && (expected.Source.ViewportHeight == 0 || actual.Source.ViewportHeight == 0 || expected.Source.ViewportHeight == actual.Source.ViewportHeight);
        var sameRoute = expected.Source.Url == actual.Source.Url && (expected.Source.Url.Length > 0 || ReferenceEquals(expected, actual)); var sameTheme = expected.Source.Theme == actual.Source.Theme;
        double? mean = null; long? changed = null;
        if (sameViewport && sameRoute && sameTheme)
        {
            var a = new FormatConvertedBitmap(DesignBrowser.Decode(rendered), PixelFormats.Bgra32, null, 0);
            var b = new FormatConvertedBitmap(DesignBrowser.Decode(actual.Assets[actual.Original].Bytes()), PixelFormats.Bgra32, null, 0);
            if (a.PixelWidth == b.PixelWidth && a.PixelHeight == b.PixelHeight)
            {
                var rowA = new byte[a.PixelWidth * 4]; var rowB = new byte[rowA.Length]; long difference = 0, pixels = 0;
                for (var y = 0; y < a.PixelHeight; y++)
                {
                    a.CopyPixels(new Int32Rect(0, y, a.PixelWidth, 1), rowA, rowA.Length, 0); b.CopyPixels(new Int32Rect(0, y, b.PixelWidth, 1), rowB, rowB.Length, 0);
                    for (var x = 0; x < rowA.Length; x += 4) { var delta = Math.Abs(rowA[x] - rowB[x]) + Math.Abs(rowA[x + 1] - rowB[x + 1]) + Math.Abs(rowA[x + 2] - rowB[x + 2]); difference += delta; if (delta > 12) pixels++; }
                }
                mean = difference / ((double)a.PixelWidth * a.PixelHeight * 3); changed = pixels;
            }
        }
        var selectors = actual.Layers.Select(n => n.Source.Selector).ToHashSet();
        var missing = new DesignSession(expected).NetChanges().Where(c => c.Operation != "deleteSubtree" && c.Source.Selector.Length > 0 && !selectors.Contains(c.Source.Selector)).Select(c => c.Source.Selector).Distinct().ToArray();
        var summary = !sameViewport || !sameRoute || !sameTheme ? "Comparison context differs. Match route, viewport and theme before interpreting pixels." : mean is null ? "Captured image scales differ; pixel comparison was not performed." : $"Mean channel difference: {mean:F2}/255; {changed:N0} pixels differ. This is visual evidence, not behavioral acceptance.";
        if (missing.Length > 0) summary += $" {missing.Length} source target hints were not found in the recapture.";
        if (expected.Source.Revision.Length > 0 && expected.Source.Revision != actual.Source.Revision) summary += " Source revision differs; verify target mappings.";
        return new(summary, actual.Source.Url, expected.Source.Revision, actual.Source.Revision, sameViewport, sameRoute, sameTheme, mean, changed, missing, ["Source selectors can drift after implementation.", "Pixel differences include font rendering and dynamic content.", "Behavior, responsiveness and accessibility require separate checks."]);
    }
}
