// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Layers;
using HyperPdfLibrary.Tests.Conformance;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for <see cref="PdfDocumentLayerEditing"/>.</summary>
public sealed class LayerEditTests
{
    /// <summary>The name of the layer the tests add.</summary>
    private const string ExtraLayer = "Extra";

    /// <summary>The label that groups layers in the order.</summary>
    private const string GroupLabel = "Group";

    /// <summary>The edits the property test makes and then undoes.</summary>
    private const int EditsMade = 4;

    /// <summary>The layers in the sample document.</summary>
    private const int SampleLayers = 2;

    /// <summary>An added layer reopens with its state, lock, usage and a place in the order, undo removes it, and the files pass qpdf.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddedLayerReopensAndUndoes(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateWithLayers(), null);

        var id = await PdfDocumentLayerEditing.AddLayerAsync(
            document,
            new(ExtraLayer) { IsVisible = false, IsLocked = true, Usage = new() { PrintState = false, Creator = "Test suite", CreatorSubtype = "Technical" } },
            cancellationToken);

        await Assert.That(PdfDocumentLayers.GetOptionalContent(document).Layers.Single(static layer => layer.Name == ExtraLayer).Id).IsEqualTo(id.Number);
        var incremental = EditChecks.SaveIncremental(document);
        var compact = EditChecks.SaveCompact(document);
        foreach (var file in new[] { incremental, compact })
        {
            using var reopened = PdfDocumentReader.Open(file, null);
            var content = PdfDocumentLayers.GetOptionalContent(reopened);
            await Assert.That(content.Layers.Count).IsEqualTo(SampleLayers + 1);
            var layer = content.Layers.Single(static layer => layer.Name == ExtraLayer);
            await Assert.That(layer.IsVisible).IsFalse();
            await Assert.That(layer.IsLocked).IsTrue();
            await Assert.That(layer.IsPrinted).IsFalse();
            await Assert.That(content.Tree.Select(static node => node.Name)).IsEquivalentTo([TestPdf.DrawingLayer, TestPdf.NotesLayer, ExtraLayer]);
        }

        await EditChecks.AssertQpdfCleanAsync("layers-incremental", incremental, cancellationToken);
        await EditChecks.AssertQpdfCleanAsync("layers-compact", compact, cancellationToken);
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentLayers.GetOptionalContent(document).Layers.Count).IsEqualTo(SampleLayers);
    }

    /// <summary>A label with sublayers and radio groups reopen as written, and turning a layer on turns the others of its group off.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrderLabelsAndRadioGroupsReopen(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateWithLayers(), null);
        var extra = (await PdfDocumentLayerEditing.AddLayerAsync(document, new(ExtraLayer), cancellationToken)).Number;
        var layers = PdfDocumentLayers.GetOptionalContent(document).Layers;
        var drawing = layers.Single(static layer => layer.Name == TestPdf.DrawingLayer).Id;
        var notes = layers.Single(static layer => layer.Name == TestPdf.NotesLayer).Id;

        await PdfDocumentLayerEditing.SetLayerOrderAsync(
            document,
            [
                new(GroupLabel, 0, [new(TestPdf.DrawingLayer, drawing, [new(ExtraLayer, extra, [])]), new(TestPdf.NotesLayer, notes, [])]),
            ],
            cancellationToken);
        await PdfDocumentLayerEditing.SetRadioGroupsAsync(document, [[drawing, notes]], cancellationToken);
        await PdfDocumentLayerEditing.SetLayerVisibleAsync(document, notes, true, cancellationToken);

        var file = EditChecks.SaveCompact(document);
        using (var reopened = PdfDocumentReader.Open(file, null))
        {
            var content = PdfDocumentLayers.GetOptionalContent(reopened);
            var group = content.Tree.Single();
            await Assert.That(group.IsLabel).IsTrue();
            await Assert.That(group.Name).IsEqualTo(GroupLabel);
            await Assert.That(group.Children.Select(static node => node.Name)).IsEquivalentTo([TestPdf.DrawingLayer, TestPdf.NotesLayer]);
            await Assert.That(group.Children[0].Children.Single().Name).IsEqualTo(ExtraLayer);
            await Assert.That(content.Layers.Single(layer => layer.Id == notes).IsVisible).IsTrue();
            await Assert.That(content.Layers.Single(layer => layer.Id == drawing).IsVisible).IsFalse();
        }

        await EditChecks.AssertQpdfCleanAsync("layers-order", file, cancellationToken);
        await Assert.That(() => PdfDocumentLayerEditing.SetLayerOrder(document, [new("Missing", int.MaxValue, [])])).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentLayerEditing.SetRadioGroups(document, [[drawing]])).Throws<ArgumentException>();
    }

    /// <summary>Several layers that start on in a radio group are cut to the first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RadioGroupKeepsOneLayerOn()
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateWithLayers(), null);
        var extra = PdfDocumentLayerEditing.AddLayer(document, new(ExtraLayer)).Number;
        var drawing = PdfDocumentLayers.GetOptionalContent(document).Layers.Single(static layer => layer.Name == TestPdf.DrawingLayer).Id;

        PdfDocumentLayerEditing.SetRadioGroups(document, [[drawing, extra]]);

        var layers = PdfDocumentLayers.GetOptionalContent(document).Layers;
        await Assert.That(layers.Single(layer => layer.Id == drawing).IsVisible).IsTrue();
        await Assert.That(layers.Single(layer => layer.Id == extra).IsVisible).IsFalse();
    }

    /// <summary>A layer can be renamed, locked and unlocked, and given usage; each change undoes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayerPropertiesChangeAndUndo()
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateWithLayers(), null);
        var notes = PdfDocumentLayers.GetOptionalContent(document).Layers.Single(static layer => layer.Name == TestPdf.NotesLayer).Id;

        PdfDocumentLayerEditing.RenameLayer(document, notes, "Comments");
        PdfDocumentLayerEditing.SetLayerLocked(document, notes, true);
        PdfDocumentLayerEditing.SetLayerUsage(document, notes, new() { ViewState = false, ExportState = false });

        var layer = PdfDocumentLayers.GetOptionalContent(document).Layers.Single(candidate => candidate.Id == notes);
        await Assert.That(layer.Name).IsEqualTo("Comments");
        await Assert.That(layer.IsLocked).IsTrue();
        await Assert.That(layer.IsExported).IsFalse();

        PdfDocumentLayerEditing.SetLayerLocked(document, notes, false);
        await Assert.That(PdfDocumentLayers.GetOptionalContent(document).IsLocked(notes)).IsFalse();
        for (var i = 0; i < EditsMade; i++)
        {
            await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        }

        await Assert.That(PdfDocumentLayers.GetOptionalContent(document).Layers.Single(candidate => candidate.Id == notes).Name).IsEqualTo(TestPdf.NotesLayer);
        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(0);
    }

    /// <summary>A layer nothing uses is removed from every list; a layer a page draws with is kept and the reason given.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingALayerChecksItIsUnused(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateWithLayers(), null);
        var extra = (await PdfDocumentLayerEditing.AddLayerAsync(document, new(ExtraLayer) { IsLocked = true }, cancellationToken)).Number;
        var drawing = PdfDocumentLayers.GetOptionalContent(document).Layers.Single(static layer => layer.Name == TestPdf.DrawingLayer).Id;

        var thrown = await Assert.That(() => PdfDocumentLayerEditing.RemoveLayer(document, drawing)).Throws<InvalidOperationException>();
        await Assert.That(thrown!.Message).Contains("Page 1");
        await Assert.That(await PdfDocumentLayerEditing.RemoveLayerAsync(document, extra, cancellationToken)).IsTrue();
        await Assert.That(await PdfDocumentLayerEditing.RemoveLayerAsync(document, extra, cancellationToken)).IsFalse();

        var file = EditChecks.SaveCompact(document);
        await EditChecks.AssertQpdfCleanAsync("layers-removed", file, cancellationToken);
        using var reopened = PdfDocumentReader.Open(file, null);
        var content = PdfDocumentLayers.GetOptionalContent(reopened);
        await Assert.That(content.Layers.Count).IsEqualTo(SampleLayers);
        await Assert.That(Encoding.Latin1.GetString(EditChecks.SaveReadable(reopened))).DoesNotContain("/Locked");
    }

    /// <summary>A certification refuses layer edits, and a PDF/A-1 claim refuses layers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClaimsLimitLayers()
    {
        using var certified = PdfDocumentReader.Open(EditGuardSamples.Certified(1), null);
        using var a1 = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A1B), null);

        await Assert.That(() => PdfDocumentLayerEditing.AddLayer(certified, new(ExtraLayer))).Throws<InvalidOperationException>();
        var thrown = await Assert.That(() => PdfDocumentLayerEditing.AddLayer(a1, new(ExtraLayer))).Throws<InvalidOperationException>();
        await Assert.That(thrown!.Message).Contains("PDF/A");
    }

    /// <summary>A layer needs a name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamelessLayerIsRefused()
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateWithLayers(), null);

        await Assert.That(() => PdfDocumentLayerEditing.AddLayer(document, new(" "))).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentLayerEditing.AddLayer(document, new(ExtraLayer) { Intent = "Print" })).Throws<ArgumentException>();
    }

    /// <summary>The key of a missing layer is not a layer of the document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownLayerIsRefused()
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateWithLayers(), null);

        await Assert.That(() => PdfDocumentLayerEditing.SetLayerVisible(document, int.MaxValue, true)).Throws<ArgumentException>();
        await Assert.That(PdfDocumentLayerEditing.RemoveLayer(document, int.MaxValue)).IsFalse();
    }
}
