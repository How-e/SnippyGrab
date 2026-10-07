using System.Text.Json;
using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class SettingsCoverageTests
{
    [Fact]
    public void EverySettingRoundTripsIncludingEdgesAndNondefaultValues()
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-settings-" + Guid.NewGuid().ToString("N"));
        var service = new SettingsService(Path.Combine(root, "settings.json"));
        try
        {
            var settings = new Settings();
            foreach (var property in typeof(Settings).GetProperties())
            {
                var type = property.PropertyType;
                if (type == typeof(bool)) property.SetValue(settings, !(bool)property.GetValue(settings)!);
                else if (type.IsEnum) property.SetValue(settings, Enum.GetValues(type).GetValue(Enum.GetValues(type).Length - 1));
                else if (type == typeof(Hotkey)) property.SetValue(settings, new Hotkey(120, 6));
            }
            settings.CachePath = Path.Combine(root, "cache"); settings.DockMonitorIdentity = "saved-device";
            settings.RetentionHours = -1; settings.ThumbnailSize = 320; settings.DockOpacity = .5;
            service.Save(settings);
            Assert.Equal(JsonSerializer.Serialize(settings), JsonSerializer.Serialize(service.Load()));
            Assert.False(service.Recovered);
        }
        finally { Directory.Delete(root, true); }
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"SchemaVersion\":0}")]
    public void MissingFieldsAndLegacySchemaKeepDefaults(string json)
    {
        var settings = JsonSerializer.Deserialize<Settings>(json)!; settings.Validate();
        Assert.Equal(1, settings.SchemaVersion); Assert.Equal(24, settings.RetentionHours);
        Assert.Equal(CaptureMode.Region, settings.DefaultCaptureMode); Assert.True(settings.StartMinimized);
        Assert.Equal(PreviewQuality.Sharp, settings.PreviewQuality); Assert.True(settings.OcrEnhanceSmallText); Assert.Equal(OcrLayout.Auto, settings.OcrLayout);
    }
    [Fact]
    public void InvalidEnumerationsAndNonfiniteNumbersRecoverPredictably()
    {
        var settings = new Settings { Corner = (DockCorner)99, Theme = (AppTheme)99, PreviewQuality = (PreviewQuality)99, OcrLayout = (OcrLayout)99, DockOpacity = double.NaN, TextSize = double.PositiveInfinity };
        settings.Validate(); Assert.Equal(DockCorner.BottomRight, settings.Corner); Assert.Equal(.96, settings.DockOpacity); Assert.Equal(24, settings.TextSize);
        Assert.Equal(PreviewQuality.Sharp, settings.PreviewQuality); Assert.Equal(OcrLayout.Auto, settings.OcrLayout);
        Assert.Throws<InvalidDataException>(() => new Settings { SchemaVersion = 2 }.Validate());
    }
}
