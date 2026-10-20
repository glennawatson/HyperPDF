// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Reading;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks the PDF/UA semantics a tagged page gives assistive technology: language changes, empty cells, artifacts, descriptions, form field state, rich text and XMP (ISO 14289-1).</summary>
public sealed class PageSemanticTests
{
    /// <summary>The first page.</summary>
    private const int Page = 0;

    /// <summary>The column of the empty cell, from 1.</summary>
    private const int EmptyColumn = 2;

    /// <summary>The number of form fields on the page.</summary>
    private const int FieldCount = 3;

    /// <summary>The number of annotations with a description.</summary>
    private const int AnnotationCount = 2;

    /// <summary>Gets a page with a password field holding a secret.</summary>
    private static byte[] Password => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] /DA (/Helv 0 Tf 0 g) >> >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>",
        "<< /Type /Annot /Subtype /Widget /FT /Tx /T (pin) /TU (Secret code) /Ff 8192 /V (hunter2) /Rect [72 700 200 720] /F 4 /P 3 0 R >>");

    /// <summary>A paragraph in another language reports it, and the blocks around it do not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsLanguageChanges()
    {
        using var test = new HyperPdfTestDocument(AccessibleTestPdf.CreateRich());
        List<TaggedBlock> blocks = [];

        var tagged = DocumentFeature<ITaggedStructureSource>.Cast(test.Document)!.GetTaggedBlocks(Page, blocks);

        await Assert.That(tagged).IsTrue();
        await Assert.That(blocks.Where(static block => block.Language is not null).Select(static block => block.Language!)).IsEquivalentTo([AccessibleTestPdf.FrenchLanguage]);
        await Assert.That(blocks.Count(static block => block.Language is null)).IsGreaterThan(0);
    }

    /// <summary>A figure is read by its description.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FigureKeepsItsDescription()
    {
        using var test = new HyperPdfTestDocument(AccessibleTestPdf.CreateRich());
        List<TaggedBlock> blocks = [];

        _ = DocumentFeature<ITaggedStructureSource>.Cast(test.Document)!.GetTaggedBlocks(Page, blocks);

        await Assert.That(blocks.Single(static block => block.Kind == ReadingBlockKind.Figure).ReplacementText).IsEqualTo(AccessibleTestPdf.FigureDescription);
    }

    /// <summary>An empty table cell and a running header are listed, so a reader can say a cell is empty and skip the header.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsEmptyCellsAndArtifacts()
    {
        using var test = new HyperPdfTestDocument(AccessibleTestPdf.CreateRich());

        var items = DocumentFeature<IPageSemanticSource>.Cast(test.Document)!.GetItems(Page);
        var cell = items.Single(static item => item.Kind == PageSemanticKind.EmptyCell);
        var artifact = items.Single(static item => item.Kind == PageSemanticKind.Artifact);

        await Assert.That(cell.Row).IsEqualTo(1);
        await Assert.That(cell.Column).IsEqualTo(EmptyColumn);
        await Assert.That(cell.Role).IsEqualTo("table cell");
        await Assert.That(artifact.Name).IsEqualTo(AccessibleTestPdf.ArtifactText);
        await Assert.That(artifact.Bounds.Width).IsGreaterThan(0);
    }

    /// <summary>Form fields give their tooltip, role, value, state and rich text styles.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DescribesFormFields()
    {
        using var test = new HyperPdfTestDocument(AccessibleTestPdf.CreateRich());

        var fields = DocumentFeature<IPageSemanticSource>.Cast(test.Document)!.GetItems(Page).Where(static item => item.Kind == PageSemanticKind.FormField).ToArray();
        var check = fields.Single(static field => field.Name == AccessibleTestPdf.CheckBoxLabel);
        var notes = fields.Single(static field => field.Name == AccessibleTestPdf.RichFieldLabel);

        await Assert.That(fields.Length).IsEqualTo(FieldCount);
        await Assert.That(check.Role).IsEqualTo("check box");
        await Assert.That(check.State).Contains("checked");
        await Assert.That(check.Description).IsEqualTo("Field agree");
        await Assert.That(notes.Role).IsEqualTo("text field");
        await Assert.That(notes.Value).IsEqualTo("Plain");
        await Assert.That(notes.RichText).IsEqualTo("bold, italic");
    }

    /// <summary>A field in a layer that is switched off is marked hidden; the visible ones are not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarksFieldsInHiddenLayers()
    {
        using var test = new HyperPdfTestDocument(AccessibleTestPdf.CreateRich());

        var fields = DocumentFeature<IPageSemanticSource>.Cast(test.Document)!.GetItems(Page).Where(static item => item.Kind == PageSemanticKind.FormField).ToArray();

        await Assert.That(fields.Single(static field => field.Name == AccessibleTestPdf.HiddenFieldLabel).IsHidden).IsTrue();
        await Assert.That(fields.Count(static field => field.IsHidden)).IsEqualTo(1);
    }

    /// <summary>Annotations with a description give it and their role.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DescribesAnnotations()
    {
        using var test = new HyperPdfTestDocument(AccessibleTestPdf.CreateRich());

        var annotations = DocumentFeature<IPageSemanticSource>.Cast(test.Document)!.GetItems(Page).Where(static item => item.Kind == PageSemanticKind.Annotation).ToArray();

        await Assert.That(annotations.Length).IsEqualTo(AnnotationCount);
        await Assert.That(annotations.Single(static item => item.Name == AccessibleTestPdf.NoteText).Role).IsEqualTo("note");
        await Assert.That(annotations.Single(static item => item.Name == AccessibleTestPdf.LinkText).Role).IsEqualTo("link");
    }

    /// <summary>A password field's value is never given.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverGivesAPasswordValue()
    {
        using var test = new HyperPdfTestDocument(Password);

        var field = DocumentFeature<IPageSemanticSource>.Cast(test.Document)!.GetItems(Page).Single(static item => item.Kind == PageSemanticKind.FormField);

        await Assert.That(field.Name).IsEqualTo("Secret code");
        await Assert.That(field.Value).IsNull();
    }

    /// <summary>A page that is out of range has nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutOfRangePagesHaveNothing()
    {
        using var test = new HyperPdfTestDocument(AccessibleTestPdf.CreateRich());
        var source = DocumentFeature<IPageSemanticSource>.Cast(test.Document)!;

        await Assert.That(source.GetItems(-1)).IsEmpty();
        await Assert.That(source.GetItems(test.Document.PageCount)).IsEmpty();
    }

    /// <summary>Every XMP property of the catalog can be listed, with schema prefixes where they are common.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsEveryXmpProperty()
    {
        using var test = new HyperPdfTestDocument(AccessibleTestPdf.CreateRich());

        var properties = DocumentFeature<IDocumentXmpSource>.Cast(test.Document)!.GetProperties().ToDictionary(static property => property.Name, static property => property.Value);

        await Assert.That(properties["dc:title"]).IsEqualTo("Quarterly Report");
        await Assert.That(properties["dc:creator"]).IsEqualTo("Ann; Bob");
        await Assert.That(properties["pdfuaid:part"]).IsEqualTo("1");
        await Assert.That(properties["xmp:CreatorTool"]).IsEqualTo("Tester");
        await Assert.That(properties["{http://example.com/ns/custom/}Department"]).IsEqualTo("Finance");
    }

    /// <summary>A document with no XMP packet lists none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsNoXmpWhenThereIsNone()
    {
        using var test = new HyperPdfTestDocument(1);

        await Assert.That(DocumentFeature<IDocumentXmpSource>.Cast(test.Document)!.GetProperties()).IsEmpty();
    }
}
