using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace SnippyGrab.App.Services;

internal static class DesignHandoff
{
    internal static async Task<byte[]> Render(CoreWebView2 editor, DesignDocument document, List<DesignLayer>? layers = null)
    {
        var expression = "SGScene.png(" + JsonSerializer.Serialize(document, DesignDocument.Json) + (layers is null ? "" : "," + JsonSerializer.Serialize(layers, DesignDocument.Json)) + ")";
        using var result = JsonDocument.Parse(await editor.CallDevToolsProtocolMethodAsync("Runtime.evaluate", JsonSerializer.Serialize(new { expression, awaitPromise = true, returnByValue = true })));
        if (result.RootElement.TryGetProperty("exceptionDetails", out _)) throw new InvalidDataException("The design could not be rendered. Check fonts, image resources and artboard limits.");
        var png = Convert.FromBase64String(result.RootElement.GetProperty("result").GetProperty("value").GetString()!);
        ImageLimits.ValidateEncoded(png.Length); return png;
    }
    internal static DesignDocument Sanitize(DesignDocument input)
    {
        var d = DesignDocument.Copy(input); var privateIds = d.PrivateIds.Union(d.Layers.Where(n => n.Private).Select(n => n.Id)).ToArray();
        var removed = DesignSession.Descendants(d.Layers, privateIds).Union(DesignSession.Descendants(d.Baseline, privateIds)).ToHashSet();
        if (removed.Count > 0)
        {
            var regions = Bounds(d.Baseline, removed).Concat(Bounds(d.Layers, removed)).Distinct().OrderBy(r => r.X).ThenBy(r => r.Y).ThenBy(r => r.Width).ThenBy(r => r.Height).ToList();
            if (d.Original.Length > 0)
            {
                var image = DesignBrowser.Decode(d.Assets[d.Original].Bytes()); var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen()) { dc.DrawImage(image, new Rect(0, 0, d.Width, d.Height)); foreach (var rect in regions) dc.DrawRectangle(Brushes.Black, null, rect); }
                var redacted = new RenderTargetBitmap((int)Math.Ceiling(d.Width), (int)Math.Ceiling(d.Height), 96, 96, PixelFormats.Pbgra32); redacted.Render(visual); redacted.Freeze(); d.Original = d.AddAsset(ImageService.Png(redacted));
            }
            d.Layers.RemoveAll(n => removed.Contains(n.Id)); d.Baseline.RemoveAll(n => removed.Contains(n.Id));
            // Overlapping raster crops may contain the same private pixels. Remove their bytes too.
            foreach (var layer in d.Layers.Concat(d.Baseline).Where(n => n.Kind == "image"))
            {
                var scene = d.Layers.Contains(layer) ? d.Layers : d.Baseline;
                if (!Bounds(scene, [layer.Id]).Any(b => regions.Any(b.IntersectsWith))) continue;
                layer.Kind = "frame"; layer.Asset = ""; layer.Fill = "#000000"; layer.Source = new() { Status = "unmapped" }; layer.Fallback = "Overlapping private image removed";
            }
            foreach (var rect in regions.Where(r => r.Width > 0 && r.Height > 0))
            {
                var identity = DesignDocument.Hash(JsonSerializer.SerializeToUtf8Bytes(new { d.Id, rect.X, rect.Y, rect.Width, rect.Height }));
                var mask = new DesignLayer { Id = "private-" + identity[..24], Kind = "frame", Name = "Private content removed", X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height, Fill = "#000000", Locked = true, Source = new() { Status = "unmapped" } };
                d.Layers.Add(mask); d.Baseline.Add(DesignDocument.Copy(mask));
            }
            d.Limitations.Add("Private content has been removed from the scene, baseline and both visual references.");
            d.BaselineHash = DesignDocument.Hash(JsonSerializer.SerializeToUtf8Bytes(d.Baseline, DesignDocument.Json));
        }
        d.PrivateIds.Clear(); d.History.Clear(); d.Head = 0;
        var needed = d.Layers.Concat(d.Baseline).Select(n => n.Asset).Where(a => a.Length > 0).Append(d.Original).ToHashSet();
        d.Assets = d.Assets.Where(a => needed.Contains(a.Key) || a.Value.MediaType.StartsWith("font/", StringComparison.Ordinal)).ToDictionary(); d.Validate(); return d;
    }
    internal static List<Rect> Bounds(List<DesignLayer> layers, IEnumerable<string> selected)
    {
        var ids = selected.ToHashSet(); var map = layers.ToDictionary(n => n.Id); var result = new List<Rect>();
        foreach (var n in layers.Where(n => ids.Contains(n.Id)))
        {
            var transform = Matrix.Identity; var chain = new List<DesignLayer> { n }; var p = n.Parent;
            while (p is not null) { var parent = map[p]; chain.Add(parent); p = parent.Parent; }
            foreach (var item in chain) { var m = Matrix.Identity; m.RotateAt(item.Rotation, item.Width / 2, item.Height / 2); m.Translate(item.X, item.Y); transform.Append(m); }
            result.Add(new MatrixTransform(transform).TransformBounds(new Rect(0, 0, n.Width, n.Height)));
        }
        return result;
    }
    internal static Dictionary<string, byte[]> Visuals(DesignDocument d, byte[] edited)
    {
        var original = d.Assets[d.Original].Bytes(); var result = new Dictionary<string, byte[]> { ["visuals/original.png"] = original, ["visuals/edited.png"] = edited };
        var before = DesignBrowser.Decode(original); var after = DesignBrowser.Decode(edited);
        foreach (var group in new DesignSession(d).NetChanges().GroupBy(c => c.LayerId).Take(1000))
        {
            var regions = Bounds(d.Baseline, [group.Key]).Concat(Bounds(d.Layers, [group.Key])).ToList(); if (regions.Count == 0) continue;
            var area = regions.Aggregate(Rect.Empty, (a, b) => { a.Union(b); return a; }); area.Inflate(12, 12);
            void Crop(BitmapSource image, string side)
            {
                var sx = image.PixelWidth / d.Width; var sy = image.PixelHeight / d.Height;
                var rect = new PixelRect((int)Math.Floor(area.X * sx), (int)Math.Floor(area.Y * sy), Math.Max(1, (int)Math.Ceiling(area.Width * sx)), Math.Max(1, (int)Math.Ceiling(area.Height * sy))).Intersect(new(0, 0, image.PixelWidth, image.PixelHeight));
                if (!rect.IsEmpty)
                {
                    BitmapSource crop = ImageService.Crop(image, rect);
                    if ((long)crop.PixelWidth * crop.PixelHeight > 4_000_000) { var factor = Math.Sqrt(4_000_000d / ((double)crop.PixelWidth * crop.PixelHeight)); crop = new TransformedBitmap(crop, new ScaleTransform(factor, factor)); crop.Freeze(); }
                    var bytes = ImageService.Png(crop); if (result.Values.Sum(b => b.LongLength) + bytes.LongLength > 128L * 1024 * 1024) throw new InvalidDataException("Visual evidence exceeds 128 MiB. Export fewer changes or a smaller artboard.");
                    result[$"visuals/changes/{group.Key}-{side}.png"] = bytes;
                }
            }
            Crop(before, "before"); Crop(after, "after"); result[$"visuals/changes/{group.Key}-bounds.json"] = JsonSerializer.SerializeToUtf8Bytes(new { area.X, area.Y, area.Width, area.Height, coordinateSpace = "artboard CSS pixels" }, DesignDocument.Json);
        }
        return result;
    }
}
