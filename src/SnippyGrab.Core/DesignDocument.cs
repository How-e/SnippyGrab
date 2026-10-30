using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SnippyGrab.Core;

public sealed record DesignSource
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "Untitled UI";
    public string Theme { get; set; } = "light";
    public string Locale { get; set; } = "";
    public string Project { get; set; } = "";
    public string Revision { get; set; } = "";
    public DateTimeOffset CapturedUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed record DesignEvidence
{
    public string Key { get; set; } = "";
    public string Selector { get; set; } = "";
    public string Role { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "observed";
    public string File { get; set; } = "";
    public string Component { get; set; } = "";
}
public sealed record DesignLayer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? Parent { get; set; }
    public string Kind { get; set; } = "frame";
    public string Name { get; set; } = "Layer";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 100;
    public double Height { get; set; } = 30;
    public double Rotation { get; set; }
    public double Opacity { get; set; } = 1;
    public double Radius { get; set; }
    public string Fill { get; set; } = "#00000000";
    public string Stroke { get; set; } = "#00000000";
    public double StrokeWidth { get; set; }
    public string ShadowColor { get; set; } = "#00000000";
    public double ShadowX { get; set; }
    public double ShadowY { get; set; }
    public double ShadowBlur { get; set; }
    public double GradientAngle { get; set; } = 180;
    public List<DesignGradientStop> GradientStops { get; set; } = [];
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public bool Clip { get; set; }
    public bool Private { get; set; }
    public string Text { get; set; } = "";
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 14;
    public int FontWeight { get; set; } = 400;
    public double LineHeight { get; set; } = 20;
    public string Align { get; set; } = "left";
    public string Asset { get; set; } = "";
    public string Fallback { get; set; } = "";
    public string Path { get; set; } = "";
    public string ViewBox { get; set; } = "0 0 24 24";
    public string Layout { get; set; } = "free";
    public double Gap { get; set; }
    public string Intent { get; set; } = "";
    public string Scope { get; set; } = "instance";
    public DesignEvidence Source { get; set; } = new();
}
public sealed record DesignGradientStop(string Color, double Offset);
public sealed record DesignAsset
{
    public string MediaType { get; set; } = "image/png";
    public string Data { get; set; } = "";
    public string Family { get; set; } = "";
    public byte[] Bytes() => Convert.FromBase64String(Data);
}
public sealed class DesignDocument
{
    public int SchemaVersion { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Revision { get; set; }
    public double Width { get; set; } = 1280;
    public double Height { get; set; } = 800;
    public double DevicePixelRatio { get; set; } = 1;
    public DesignSource Source { get; set; } = new();
    public string BaselineHash { get; set; } = "";
    public List<DesignLayer> Baseline { get; set; } = [];
    public List<DesignLayer> Layers { get; set; } = [];
    public Dictionary<string, DesignAsset> Assets { get; set; } = [];
    public string Original { get; set; } = "";
    public List<string> Limitations { get; set; } = [];
    public List<DesignTransaction> History { get; set; } = [];
    public int Head { get; set; }
    public List<string> PrivateIds { get; set; } = [];
    [JsonIgnore] public string? LoadedFingerprint { get; set; }
    public static JsonSerializerOptions Json { get; } = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, MaxDepth = 160, WriteIndented = false, NumberHandling = JsonNumberHandling.Strict };
    public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value, Json), Json)!;
    public static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public string AddAsset(byte[] bytes, string mediaType = "image/png", string family = "")
    {
        var id = Hash(bytes); if (Assets.Count >= 20000 && !Assets.ContainsKey(id)) throw new InvalidDataException("Design asset inventory exceeds 20000 entries."); Assets.TryAdd(id, new() { Data = Convert.ToBase64String(bytes), MediaType = mediaType, Family = family }); return id;
    }
    public void SealBaseline()
    {
        Normalize(Layers);
        Baseline = Copy(Layers); BaselineHash = Hash(JsonSerializer.SerializeToUtf8Bytes(Baseline, Json)); History.Clear(); Head = 0; Revision = 0; Validate();
    }
    public static void Normalize(List<DesignLayer> layers)
    {
        foreach (var n in layers) { n.Fill = n.Fill.ToLowerInvariant(); n.Stroke = n.Stroke.ToLowerInvariant(); n.X = Math.Round(n.X, 3); n.Y = Math.Round(n.Y, 3); n.Width = Math.Round(n.Width, 3); n.Height = Math.Round(n.Height, 3); }
    }
    public static string SafeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return "";
        return new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.AbsoluteUri;
    }
    public void Validate()
    {
        if (SchemaVersion != 1 || Id == Guid.Empty || !Finite(Width, 1, 16384) || !Finite(Height, 1, 16384) || Width * Height > 64_000_000 || !Finite(DevicePixelRatio, .1, 8)) throw new InvalidDataException("Unsupported design version or artboard size (64 MP / 16384 per axis).");
        if (Source is null || Source.Url != SafeUrl(Source.Url) || Source.Title.Length > 500 || Source.Project.Length > 2000 || Source.Revision.Length > 200 || Limitations.Count > 1000 || Limitations.Any(s => s.Length > 2000)) throw new InvalidDataException("Invalid design source metadata.");
        if (Assets.Count > 20000) throw new InvalidDataException("Too many design assets.");
        long total = 0;
        foreach (var (key, asset) in Assets)
        {
            if (asset is null || !Regex.IsMatch(key, "^[a-f0-9]{64}$") || asset.Data.Length > 140_000_000 || asset.Family.Length > 300) throw new InvalidDataException("Invalid design asset.");
            var bytes = asset.Bytes(); total += bytes.Length;
            if (total > 256L * 1024 * 1024 || Hash(bytes) != key || !ValidAsset(bytes, asset.MediaType)) throw new InvalidDataException("Design asset type, hash or size is invalid.");
        }
        if (Original.Length != 0 && (!Assets.ContainsKey(Original) || Assets[Original].MediaType != "image/png")) throw new InvalidDataException("Original reference is missing.");
        ValidateLayers(Baseline); ValidateLayers(Layers);
        if (BaselineHash != Hash(JsonSerializer.SerializeToUtf8Bytes(Baseline, Json))) throw new InvalidDataException("Design baseline hash does not match.");
        if (Head < 0 || Head > History.Count || History.Count > 200 || Revision < 0 || PrivateIds.Count > 10000 || PrivateIds.Any(id => !Regex.IsMatch(id, "^[a-zA-Z0-9_-]{1,80}$"))) throw new InvalidDataException("Invalid design history.");
        foreach (var transaction in History)
        {
            if (transaction is null || transaction.Name.Length > 200 || transaction.Changes.Count > 20000) throw new InvalidDataException("Invalid design transaction.");
            foreach (var change in transaction.Changes)
                if (change.Before is null && change.After is null || change.Before is { } b && b.Id != change.Id || change.After is { } a && a.Id != change.Id) throw new InvalidDataException("Invalid design history target.");
        }
        if (JsonSerializer.SerializeToUtf8Bytes(new { Layers, Baseline, History }, Json).Length > 64 * 1024 * 1024) throw new InvalidDataException("Design scene/history exceeds 64 MiB.");
    }
    public void ValidateLayers(List<DesignLayer> layers)
    {
        if (layers is null || layers.Count > 10000) throw new InvalidDataException("Design supports at most 10000 layers.");
        var ids = new Dictionary<string, DesignLayer>(StringComparer.Ordinal);
        foreach (var n in layers)
        {
            if (n is null || !Regex.IsMatch(n.Id, "^[a-zA-Z0-9_-]{1,80}$") || !ids.TryAdd(n.Id, n) || n.Kind is not ("frame" or "group" or "text" or "image" or "ellipse" or "path")) throw new InvalidDataException("Invalid or duplicate design layer.");
            if (!Finite(n.X, -65536, 65536) || !Finite(n.Y, -65536, 65536) || !Finite(n.Width, .01, 32768) || !Finite(n.Height, .01, 32768) || !Finite(n.Rotation, -36000, 36000) || !Finite(n.Opacity, 0, 1) || !Finite(n.Radius, 0, 16384) || !Finite(n.StrokeWidth, 0, 1000) || !Finite(n.FontSize, 1, 1000) || !Finite(n.LineHeight, 1, 2000) || !Finite(n.Gap, 0, 1000)) throw new InvalidDataException("Invalid layer geometry.");
            if (!Color(n.ShadowColor) || !Finite(n.ShadowX, -1000, 1000) || !Finite(n.ShadowY, -1000, 1000) || !Finite(n.ShadowBlur, 0, 1000) || !Finite(n.GradientAngle, -36000, 36000) || n.GradientStops.Count > 20 || n.GradientStops.Any(s => !Color(s.Color) || !Finite(s.Offset, 0, 1))) throw new InvalidDataException("Invalid gradient or shadow.");
            if (!Color(n.Fill) || !Color(n.Stroke) || n.Text.Length > 100000 || n.Name.Length > 500 || n.Intent.Length > 4000 || n.FontFamily.Length > 300 || n.Path.Length > 100000 || !Regex.IsMatch(n.Path, "^[MmZzLlHhVvCcSsQqTtAa0-9eE.,+\\- \\r\\n]*$") || !Regex.IsMatch(n.ViewBox, "^[-+0-9.eE ]{1,100}$") || n.Align is not ("left" or "center" or "right") || n.Layout is not ("free" or "vertical" or "horizontal") || n.Scope is not ("instance" or "matching" or "component" or "token")) throw new InvalidDataException("Invalid layer content or style.");
            if (n.Asset.Length != 0 && (!Assets.ContainsKey(n.Asset) || !Assets[n.Asset].MediaType.StartsWith("image/", StringComparison.Ordinal))) throw new InvalidDataException("Layer image is missing.");
            if (n.Source is null || n.Source.Status is not ("observed" or "user-linked" or "suggested" or "unmapped") || n.Source.Selector.Length > 2000 || n.Source.Key.Length > 200 || n.Source.Name.Length > 1000 || n.Source.Component.Length > 500 || n.Source.File.Length > 2000 || n.Source.File.Length > 0 && (System.IO.Path.IsPathRooted(n.Source.File) || n.Source.File.Contains(':') || n.Source.File.Split('/', '\\').Contains(".."))) throw new InvalidDataException("Invalid source evidence.");
        }
        foreach (var n in layers)
        {
            var depth = 0; var parent = n.Parent;
            while (parent is not null)
            {
                if (++depth > 128 || parent == n.Id || !ids.TryGetValue(parent, out var found)) throw new InvalidDataException("Invalid layer hierarchy.");
                parent = found.Parent;
            }
        }
    }
    public static bool Color(string color) => Regex.IsMatch(color, "^#[a-fA-F0-9]{6}([a-fA-F0-9]{2})?$");
    public static void ValidateEnvelope(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected a design object.");
        long historyChanges = 0;
        foreach (var property in root.EnumerateObject())
        {
            var name = property.Name.ToLowerInvariant(); var value = property.Value;
            var limit = name switch { "layers" or "baseline" or "privateids" => 10000, "history" => 200, "limitations" => 1000, _ => 0 };
            if (limit > 0 && (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > limit)) throw new InvalidDataException("Design collection exceeds its admission limit.");
            if (name == "assets" && (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Take(20001).Count() > 20000)) throw new InvalidDataException("Design asset inventory exceeds its admission limit.");
            if (name == "history") foreach (var transaction in value.EnumerateArray()) foreach (var item in transaction.EnumerateObject())
                if (item.Name.Equals("changes", StringComparison.OrdinalIgnoreCase) && (item.Value.ValueKind != JsonValueKind.Array || (historyChanges += item.Value.GetArrayLength()) > 50000)) throw new InvalidDataException("Design history exceeds its admission limit.");
        }
    }
    public static bool Finite(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;
    private static bool ValidAsset(byte[] b, string type) => type switch
    {
        "image/png" => b.Length > 24 && b.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(16)) is > 0 and <= 16384 && System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(20)) is > 0 and <= 16384 && (long)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(16)) * System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(20)) <= 64_000_000,
        "font/woff2" => b.Length > 48 && b.AsSpan(0, 4).SequenceEqual("wOF2"u8),
        "font/woff" => b.Length > 44 && b.AsSpan(0, 4).SequenceEqual("wOFF"u8),
        _ => false
    };
}

public sealed record DesignLayerChange(string Id, DesignLayer? Before, DesignLayer? After, int BeforeIndex, int AfterIndex);
public sealed record DesignTransaction(string Name, DateTimeOffset Time, List<DesignLayerChange> Changes)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public long BaseRevision { get; init; }
}
