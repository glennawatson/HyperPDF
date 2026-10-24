// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;

namespace HyperPdf.Core.Tests.Documents;

/// <summary>Checks layer visibility in saved incremental revisions.</summary>
public sealed class PdfLayersTests
{
    /// <summary>The first optional content group's object number.</summary>
    private const int DrawingId = 4;

    /// <summary>The second optional content group's object number.</summary>
    private const int NotesId = 5;

    /// <summary>The page tree shared by the layer samples.</summary>
    private const string Pages = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";

    /// <summary>The empty page shared by the layer samples.</summary>
    private const string Page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> >>";

    /// <summary>The selection that hides the drawing layer.</summary>
    private static readonly int[] HiddenDrawing = [DrawingId];

    /// <summary>Reads names and the default hidden group without changing the source.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Read_DefaultConfiguration_ReturnsNamedVisibility()
    {
        var layers = PdfLayers.Read(TestPdf.CreateWithLayers());

        await Assert.That(layers).IsEquivalentTo(new DocumentLayer[]
        {
            new(DrawingId, TestPdf.DrawingLayer, true),
            new(NotesId, TestPdf.NotesLayer, false),
        });
    }

    /// <summary>Replaces prior visibility overrides and preserves every original byte.</summary>
    /// <param name="layout">Where the properties and configuration dictionaries are stored.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("inline")]
    [Arguments("properties")]
    [Arguments("configuration")]
    [Arguments("missing")]
    public async Task WithHidden_StorageLayouts_WritesVisibleRevision(string layout)
    {
        var file = Create(layout, "/BaseState /OFF /ON [4 0 R] /OFF [5 0 R]");
        var copy = PdfLayers.WithHidden(file, HiddenDrawing);

        await Assert.That(copy.AsSpan(0, file.Length).SequenceEqual(file)).IsTrue();
        await Assert.That(PdfLayers.Read(copy)).IsEquivalentTo(new DocumentLayer[]
        {
            new(DrawingId, "Drawing", false),
            new(NotesId, "Notes", true),
        });
        await Assert.That(PdfLayers.Read(file).Single(static layer => layer.Id == DrawingId).IsVisible).IsTrue();

        var shown = PdfLayers.WithHidden(copy, []);
        await Assert.That(PdfLayers.Read(shown).All(static layer => layer.IsVisible)).IsTrue();
    }

    /// <summary>Applies the base state and falls back to a numbered name for an unnamed group.</summary>
    /// <param name="configuration">The default configuration entries.</param>
    /// <param name="drawingVisible">Whether the first group is shown.</param>
    /// <param name="notesVisible">Whether the unnamed group is shown.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("/BaseState /OFF /ON [4 0 R]", true, false)]
    [Arguments("/BaseState /ON /OFF [4 0 R]", false, true)]
    [Arguments("/BaseState /Unchanged", true, true)]
    [Arguments("/BaseState (OFF)", true, true)]
    [Arguments("", true, true)]
    public async Task Read_BaseState_ResolvesOverrides(string configuration, bool drawingVisible, bool notesVisible)
    {
        var file = MiniPdf.Build(
            $"<< /Type /Catalog /Pages 2 0 R /OCProperties << /OCGs [4 0 R 5 0 R] /D << {configuration} >> >> >>",
            Pages,
            Page,
            "<< /Type /OCG /Name (Drawing) >>",
            "<< /Type /OCG >>");

        await Assert.That(PdfLayers.Read(file)).IsEquivalentTo(new DocumentLayer[]
        {
            new(DrawingId, "Drawing", drawingVisible),
            new(NotesId, "Layer 2", notesVisible),
        });
    }

    /// <summary>Returns no layers when the catalog has no group list.</summary>
    /// <param name="properties">The optional content catalog entry.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("/OCProperties << >>")]
    [Arguments("/OCProperties << /OCGs [] >>")]
    public async Task Read_NoGroups_ReturnsEmpty(string properties)
    {
        var file = MiniPdf.Build(
            $"<< /Type /Catalog /Pages 2 0 R {properties} >>",
            Pages,
            Page);

        await Assert.That(PdfLayers.Read(file)).IsEmpty();
    }

    /// <summary>A file without a layer API cannot be rewritten as a layer selection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WithHidden_NoLayers_RejectsDocument()
    {
        await Assert.That(static () => PdfLayers.WithHidden(TestPdf.Create(1), [])).Throws<InvalidDataException>();
        await Assert.That(PdfLayers.Read("not a PDF"u8.ToArray())).IsEmpty();
    }

    /// <summary>Rejects missing caller inputs before reading the file.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NullInputs_ThrowArgumentNullException()
    {
        await Assert.That(static () => PdfLayers.Read(null!)).Throws<ArgumentNullException>();
        await Assert.That(static () => PdfLayers.WithHidden(null!, [])).Throws<ArgumentNullException>();
        await Assert.That(static () => PdfLayers.WithHidden(TestPdf.CreateWithLayers(), null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Creates a layer document with inline or referenced configuration storage.</summary>
    /// <param name="layout">The dictionary storage layout.</param>
    /// <param name="configuration">The initial visibility entries.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Create(string layout, string configuration)
    {
        var properties = layout switch
        {
            "properties" => "6 0 R",
            "configuration" => "<< /OCGs [4 0 R 5 0 R] /D 6 0 R >>",
            "missing" => "<< /OCGs [4 0 R 5 0 R] >>",
            _ => $"<< /OCGs [4 0 R 5 0 R] /D << {configuration} >> >>",
        };
        var referenced = layout == "properties" ? $"<< /OCGs [4 0 R 5 0 R] /D << {configuration} >> >>" : $"<< {configuration} >>";
        return MiniPdf.Build(
            $"<< /Type /Catalog /Pages 2 0 R /OCProperties {properties} >>",
            Pages,
            Page,
            "<< /Type /OCG /Name (Drawing) >>",
            "<< /Type /OCG /Name (Notes) >>",
            referenced);
    }
}
