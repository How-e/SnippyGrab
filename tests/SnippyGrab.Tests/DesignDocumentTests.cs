using System.Text.Json;
using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class DesignDocumentTests
{
    [Fact]
    public void ImportedTextStylesRoundTripWithoutChangingLegacyBaselineSerialization()
    {
        var d = Fixture(); var original = JsonSerializer.Serialize(d.Baseline, DesignDocument.Json);
        Assert.DoesNotContain("noWrap", original); Assert.DoesNotContain("fontStyle", original); Assert.DoesNotContain("letterSpacing", original); Assert.DoesNotContain("textDecoration", original);
        var legacy = JsonSerializer.Deserialize<DesignDocument>(JsonSerializer.Serialize(d, DesignDocument.Json), DesignDocument.Json)!; legacy.Validate();
        var session = new DesignSession(d); session.Apply("Typography", layers => { layers[1].NoWrap = true; layers[1].LetterSpacing = 1.5; layers[1].FontStyle = "italic"; layers[1].TextDecoration = "underline"; });
        var copy = DesignDocument.Copy(d); copy.Validate(); Assert.True(copy.Layers[1].NoWrap); Assert.Equal(1.5, copy.Layers[1].LetterSpacing); Assert.Equal("italic", copy.Layers[1].FontStyle);
        Assert.Equal(4, session.NetChanges().Count);
        session.Undo(); Assert.Equal(original, JsonSerializer.Serialize(d.Layers, DesignDocument.Json));
        Assert.Throws<InvalidDataException>(() => session.Apply("Invalid style", layers => layers[1].FontStyle = "url(secret)"));
    }
    public static DesignDocument Fixture()
    {
        var document = new DesignDocument { Layers = [new() { Id = "frame", Width = 400, Height = 300 }, new() { Id = "title", Kind = "text", Parent = "frame", Text = "Before" }, new() { Id = "body", Parent = "frame", Y = 60 }] }; document.SealBaseline(); return document;
    }
    [Fact]
    public void LayerOrganizationMetadataDoesNotBecomeAnImplementationRequest()
    {
        var s = new DesignSession(Fixture()); s.Apply("organize", nodes => { nodes[1].Name = "Heading layer"; nodes[1].Scope = "component"; nodes[1].Locked = true; }); Assert.Empty(s.NetChanges());
        s.Apply("intent", nodes => nodes[1].Intent = "Support narrow screens."); Assert.Single(s.NetChanges()); Assert.Equal("component", s.NetChanges()[0].Scope);
    }
    [Fact]
    public void ConcurrentDesignSavesRejectStaleAndLateFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".snippy");
        try
        {
            var d = Fixture(); DesignStore.Save(path, d); var hash = DesignStore.Fingerprint(path); Assert.Equal(hash, DesignStore.Load(path).LoadedFingerprint); var s = new DesignSession(d); s.Apply("move", n => n[0].X = 12); DesignStore.Save(path, d, hash);
            Assert.Throws<IOException>(() => DesignStore.Save(path, Fixture(), hash)); Assert.Throws<IOException>(() => DesignStore.Save(path, Fixture(), "")); Assert.Equal(12, DesignStore.Load(path).Layers[0].X);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void ReparentPreservesPositionAndRejectsCycles()
    {
        var d = Fixture(); d.Layers[0].X = 20; d.Layers[1].X = 10; d.SealBaseline(); var s = new DesignSession(d);
        s.Reparent("title", null); Assert.Null(d.Layers.Single(n => n.Id == "title").Parent); Assert.Equal(30, d.Layers.Single(n => n.Id == "title").X); s.Undo(); Assert.Equal(10, d.Layers.Single(n => n.Id == "title").X);
        Assert.Throws<InvalidOperationException>(() => s.Reparent("frame", "body"));
    }
    [Fact]
    public void PrivateSelectionUndoAndDeletionKeepConsistentHandoffScope()
    {
        var d = Fixture(); var s = new DesignSession(d); s.Apply("private", n => n[1].Private = true); Assert.Contains("title", d.PrivateIds); s.Undo(); Assert.Empty(d.PrivateIds); s.Redo(); Assert.Contains("title", d.PrivateIds); s.Delete(["title"]); Assert.DoesNotContain(s.NetChanges(), c => c.LayerId == "title");
    }
    [Fact]
    public void ADeletedParentCannotBeExcludedWhileLeavingOrphanChildren()
    {
        var d = Fixture(); var s = new DesignSession(d); s.Delete(["frame"]); var reviewed = DesignStore.Review(d, ["frame"]); Assert.Equal(3, reviewed.Layers.Count); Assert.Empty(new DesignSession(reviewed).NetChanges());
    }
    [Fact]
    public void ImportedFractionalGeometryDoesNotCreatePhantomChanges()
    {
        var d = Fixture(); d.Layers[0].X = 10.333333; d.Layers[0].Fill = "#ABCDEF"; d.SealBaseline();
        var s = new DesignSession(d); s.Apply("text", n => n[1].Text = "After"); Assert.Single(s.NetChanges());
        Assert.Empty(new DesignSession(DesignStore.Review(d, ["title"])).NetChanges());
    }
    [Fact]
    public void NetChangesRespectUndoBranchAndBaseline()
    {
        var session = new DesignSession(Fixture()); session.Apply("text", n => n[1].Text = "After"); session.Apply("text", n => n[1].Text = "Final");
        Assert.Single(session.NetChanges()); Assert.Equal("Final", session.NetChanges()[0].After.GetString());
        session.Undo(); Assert.Equal("After", session.Document.Layers[1].Text); session.Redo(); Assert.Equal("Final", session.Document.Layers[1].Text);
        session.Undo(); session.Apply("revert", n => n[1].Text = "Before"); Assert.False(session.CanRedo); Assert.Empty(session.NetChanges());
    }
    [Fact]
    public void SubtreeDeleteRestoreGroupAndDuplicateAreTransactional()
    {
        var s = new DesignSession(Fixture()); s.Delete(["frame"]); Assert.Empty(s.Document.Layers); Assert.Single(s.NetChanges()); s.Undo(); Assert.Equal(3, s.Document.Layers.Count);
        s.Group(["title", "body"]); Assert.Equal(4, s.Document.Layers.Count); s.Undo(); Assert.Equal("frame", s.Document.Layers[1].Parent);
        s.Duplicate(["frame"]); Assert.Equal(6, s.Document.Layers.Count); s.Undo(); Assert.Equal(3, s.Document.Layers.Count);
        s.Delete(["frame"]); s.Revert("frame"); Assert.Empty(s.NetChanges());
    }
    [Fact]
    public void InvalidEditsAreAtomicAndParentMovementDoesNotMoveChildren()
    {
        var s = new DesignSession(Fixture()); Assert.Throws<InvalidDataException>(() => s.Apply("cycle", n => n[0].Parent = "title")); Assert.False(s.CanUndo);
        s.Apply("move", n => n[0].X = 70); Assert.Single(s.NetChanges()); Assert.Equal("frame", s.NetChanges()[0].LayerId);
        Assert.Throws<InvalidDataException>(() => s.Apply("NaN", n => n[1].X = double.NaN));
    }
    [Fact]
    public void ReviewExclusionsAndReloadPreserveBaseline()
    {
        var d = Fixture(); var s = new DesignSession(d); s.Apply("text", n => n[1].Text = "After"); s.Apply("move", n => n[2].Y = 90);
        var reviewed = DesignStore.Review(d, ["title"]); Assert.Single(new DesignSession(reviewed).NetChanges()); Assert.Equal("After", d.Layers[1].Text);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".snippy");
        try { DesignStore.Save(path, d); var loaded = DesignStore.Load(path); Assert.Equal(d.BaselineHash, loaded.BaselineHash); new DesignSession(loaded).Undo(); Assert.Equal(60, loaded.Layers[2].Y); }
        finally { File.Delete(path); }
    }
    [Theory]
    [InlineData("https://user:pass@example.org/page?secret=1#token", "https://example.org/page")]
    [InlineData("file:///C:/private.txt", "")]
    [InlineData("javascript:alert(1)", "")]
    public void SourceUrlsDoNotPersistCredentialsOrQueries(string source, string expected) => Assert.Equal(expected, DesignDocument.SafeUrl(source));
    [Fact]
    public void CorruptBaselineSourcePathsAndUnsupportedVersionsFailClosed()
    {
        using var oversized = JsonDocument.Parse("{\"LAYERS\":[" + string.Join(',', Enumerable.Repeat("{}", 10001)) + "]}"); Assert.Throws<InvalidDataException>(() => DesignDocument.ValidateEnvelope(oversized.RootElement));
        var d = Fixture(); d.Baseline[0].X = 20; Assert.Throws<InvalidDataException>(d.Validate);
        d = Fixture(); d.Layers[0].Source.File = "../secret"; Assert.Throws<InvalidDataException>(d.Validate);
        d = Fixture(); d.SchemaVersion = 2; Assert.Throws<InvalidDataException>(d.Validate);
    }
    [Fact]
    public void InsertThenDeleteAndPrivateEditsDoNotEnterBrief()
    {
        var s = new DesignSession(Fixture()); s.Duplicate(["title"]); s.Delete(s.Document.Layers.Where(n => !s.Document.Baseline.Any(b => b.Id == n.Id)).Select(n => n.Id).ToArray()); Assert.Empty(s.NetChanges());
        s.Apply("private", n => { n[1].Private = true; n[1].Text = "secret"; }); Assert.Empty(s.NetChanges()); Assert.DoesNotContain("secret", DesignStore.Brief(s.Document));
    }
}
