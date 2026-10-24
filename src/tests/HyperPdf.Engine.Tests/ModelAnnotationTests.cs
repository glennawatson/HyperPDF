// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Reading;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Media;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks how a 3D annotation with U3D data is described to assistive technology, and that saving keeps its data exactly.</summary>
public sealed class ModelAnnotationTests
{
    /// <summary>The page the annotation is on.</summary>
    private const int Page = 0;

    /// <summary>Where the unrelated note goes.</summary>
    private static readonly PagePoint NoteAt = new(100, 100);

    /// <summary>The page item for the model says what it is, uses the annotation's own description, and tells what the model declares.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DescribesTheModelForAssistiveTechnology()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateModelDocument(U3dBuilder.SampleModel()));

        var item = DocumentFeature<IPageSemanticSource>.Cast(test.Document)!.GetItems(Page).Single(static candidate => candidate.Role == "3D model");

        await Assert.That(item.Kind).IsEqualTo(PageSemanticKind.Annotation);
        await Assert.That(item.Name).IsEqualTo("Metal box");
        await Assert.That(item.Description).StartsWith("3D model with 1 shape, 4 scene nodes, 1 view, 1 material and 1 texture.");
        await Assert.That(item.Description).Contains("not drawn");
    }

    /// <summary>A model that cannot be read in full still gets a description that says so, and never throws.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DescribesAnUnreadableModel()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateModelDocument("not a model at all"u8.ToArray()));

        var item = DocumentFeature<IPageSemanticSource>.Cast(test.Document)!.GetItems(Page).Single(static candidate => candidate.Role == "3D model");

        await Assert.That(item.Description).Contains("not a U3D file");
    }

    /// <summary>Saving an unrelated change leaves the model's bytes and its annotation as they were.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavingUnrelatedEditsKeepsTheModelBytes()
    {
        var u3d = U3dBuilder.SampleModel();
        using var test = new HyperPdfTestDocument(TestPdf.CreateModelDocument(u3d));

        _ = HyperPdfAnnotationShapes.AddNote(test.Editor, Page, NoteAt, "Unrelated", AnnotationColors.Sand);
        var saved = HyperPdfTestDocument.Save(test.Editor);
        var reopened = PdfDocumentReader.Open(saved, null);
        var model = PdfDocumentMedia.GetMultimediaAnnotations(reopened).Single(static annotation => annotation.Kind == PdfMultimediaKind.ThreeD).ThreeD!.Stream!;

        await Assert.That(model.Subtype).IsEqualTo("U3D");
        await Assert.That(model.Data.DecodeToArray().AsSpan().SequenceEqual(u3d)).IsTrue();
    }
}
