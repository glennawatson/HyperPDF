// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for transforming page content with a clip, moving annotations with it, and copying viewer preferences.</summary>
public sealed class ContentTransformTests
{
    /// <summary>The page edge in points.</summary>
    private const int Edge = 200;

    /// <summary>The streams on a transformed page: the opening operators, the page's own content and the closing Q.</summary>
    private const int TransformedStreams = 3;

    /// <summary>The distance the content is moved, in points.</summary>
    private const float Shift = 25;

    /// <summary>The scale the content is enlarged by.</summary>
    private const float Enlarge = 2;

    /// <summary>The numbers in the source's print page range.</summary>
    private const int PrintRangeEntries = 2;

    /// <summary>How far a turned size may differ from the exact one, in points.</summary>
    private const float Tolerance = 0.001F;

    /// <summary>The position of the source whose preferences are taken.</summary>
    private const int PreferencePages = 1;

    /// <summary>The annotation's rectangle.</summary>
    private static readonly PdfRectangle SquareRect = new(10, 10, 50, 30);

    /// <summary>The clip used by the tests.</summary>
    private static readonly PdfRectangle Clip = new(20, 20, 180, 180);

    /// <summary>The annotation's rectangle once the content is enlarged and moved.</summary>
    private static readonly PdfRectangle MovedRect = new(45, 45, 125, 85);

    /// <summary>A clip with no width.</summary>
    private static readonly PdfRectangle EmptyClip = new(5, 5, 5, 9);

    /// <summary>Gets the scale and move used by the tests.</summary>
    private static Matrix3x2 ScaleAndMove => Matrix3x2.CreateScale(Enlarge) * Matrix3x2.CreateTranslation(Shift, Shift);

    /// <summary>The content is wrapped in a clip and a matrix, and the page's own streams are kept between.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContentIsWrappedInAClipAndAMatrix()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        PdfDocumentContentTransforms.TransformPages(document, [0], ScaleAndMove, new() { Clip = Clip });
        var streams = OverlayTests.Streams(document, 0);

        await Assert.That(streams.Length).IsEqualTo(TransformedStreams);
        await Assert.That(streams[0]).Contains("20 20 160 160 re");
        await Assert.That(streams[0]).Contains("W");
        await Assert.That(streams[0]).Contains("2 0 0 2 25 25 cm");
        await Assert.That(streams[1]).Contains("(Page 1)");
        await Assert.That(streams[^1]).IsEqualTo("Q\n");
    }

    /// <summary>The annotation's rectangle follows the matrix and its appearance is drawn through a form that holds the original.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationsMoveWithTheContent()
    {
        using var document = PdfDocumentReader.Open(WithAnnotation(), null);
        var appearance = PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(0)!.GetDictionary(KnownName.AP)!.GetRaw(KnownName.N);
        PdfDocumentContentTransforms.TransformPages(document, [0], ScaleAndMove, PdfContentTransformOptions.Default);
        var annotation = PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(0)!;
        var wrapper = annotation.GetDictionary(KnownName.AP)!.GetStream(KnownName.N)!;

        await Assert.That(annotation.TryGetRectangle(KnownName.Rect, out var rect)).IsTrue();
        await Assert.That(rect).IsEqualTo(MovedRect);
        await Assert.That(annotation.GetDictionary(KnownName.AP)!.GetRaw(KnownName.N).AsReference()).IsNotEqualTo(appearance.AsReference());
        await Assert.That(wrapper.Dictionary.TryGetRectangle(KnownName.BBox, out var box)).IsTrue();
        await Assert.That(box.Width).IsEqualTo(SquareRect.Width * Enlarge);
        await Assert.That(System.Text.Encoding.Latin1.GetString(wrapper.DecodeToArray())).Contains("Do");
    }

    /// <summary>A turn by a quarter swaps the sides of the rectangle and of the appearance.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AQuarterTurnSwapsTheSides()
    {
        using var document = PdfDocumentReader.Open(WithAnnotation(), null);
        PdfDocumentContentTransforms.TransformPages(document, [0], Matrix3x2.CreateRotation(MathF.PI / Enlarge), PdfContentTransformOptions.Default);
        var annotation = PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(0)!;

        await Assert.That(annotation.TryGetRectangle(KnownName.Rect, out var rect)).IsTrue();
        await Assert.That(rect.Width).IsEqualTo(SquareRect.Height).Within(Tolerance);
        await Assert.That(rect.Height).IsEqualTo(SquareRect.Width).Within(Tolerance);
        await Assert.That(annotation.GetDictionary(KnownName.AP)!.GetStream(KnownName.N)!.Dictionary.TryGetRectangle(KnownName.BBox, out var box)).IsTrue();
        await Assert.That(box.Width).IsEqualTo(SquareRect.Height).Within(Tolerance);
    }

    /// <summary>Annotations stay put when the options say so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationsCanStayWhereTheyAre()
    {
        using var document = PdfDocumentReader.Open(WithAnnotation(), null);
        PdfDocumentContentTransforms.TransformPages(document, [0], ScaleAndMove, new() { TransformAnnotations = false });
        var annotation = PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(0)!;

        await Assert.That(annotation.TryGetRectangle(KnownName.Rect, out var rect)).IsTrue();
        await Assert.That(rect).IsEqualTo(SquareRect);
    }

    /// <summary>The transform survives an incremental save and a compact save.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TransformSurvivesBothWriters()
    {
        using var document = PdfDocumentReader.Open(WithAnnotation(), null);
        PdfDocumentContentTransforms.TransformPages(document, [0], ScaleAndMove, new() { Clip = Clip });
        using var incremental = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);
        using var compact = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), null);
        foreach (var saved in (PdfDocument[])[incremental, compact])
        {
            var annotation = PdfDocumentPages.GetPage(saved, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(0)!;

            await Assert.That(OverlayTests.Streams(saved, 0).Length).IsEqualTo(TransformedStreams);
            await Assert.That(annotation.TryGetRectangle(KnownName.Rect, out var rect)).IsTrue();
            await Assert.That(rect).IsEqualTo(MovedRect);
        }
    }

    /// <summary>A matrix that cannot be undone, one that is not finite and an empty clip are refused, and nothing changes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnusableArgumentsAreRefused()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);

        await Assert.That(() => PdfDocumentContentTransforms.TransformPages(document, [0], Matrix3x2.CreateScale(0), PdfContentTransformOptions.Default)).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentContentTransforms.TransformPages(document, [0], new(float.NaN, 0, 0, 1, 0, 0), PdfContentTransformOptions.Default)).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentContentTransforms.TransformPages(document, [0], Matrix3x2.Identity, new() { Clip = EmptyClip })).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentContentTransforms.TransformPages(document, [1], Matrix3x2.Identity, PdfContentTransformOptions.Default)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(StoreEditing.HasEdits(document.Objects)).IsFalse();
    }

    /// <summary>Viewer preferences are copied without the print page range, which names pages of the source.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ViewerPreferencesAreCopiedWithoutThePrintRange()
    {
        var withPreferences = new RenderTestPdf(Edge, Edge) { CatalogEntries = "/ViewerPreferences << /HideToolbar true /FitWindow true /PrintPageRange [0 1] >>" };
        using var source = PdfDocumentReader.Open(withPreferences.ToBytes(), null);
        using var target = PdfDocumentReader.Open(new RenderTestPdf(Edge, Edge).ToBytes(), null);
        using var empty = PdfDocumentReader.Open(new RenderTestPdf(Edge, Edge).ToBytes(), null);

        await Assert.That(PdfDocumentViewerPreferences.CopyViewerPreferences(target, empty, CancellationToken.None)).IsFalse();
        await Assert.That(PdfDocumentViewerPreferences.CopyViewerPreferences(target, source, CancellationToken.None)).IsTrue();
        var copied = PdfDocumentViewerPreferences.GetViewerPreferences(target);

        await Assert.That(copied.HideToolbar).IsTrue();
        await Assert.That(copied.FitWindow).IsTrue();
        await Assert.That(copied.PrintPageRange.Length).IsEqualTo(0);
        await Assert.That(PdfDocumentViewerPreferences.GetViewerPreferences(source).PrintPageRange.Length).IsEqualTo(PrintRangeEntries);
    }

    /// <summary>An assembled document takes the preferences of the source it is told to.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AssembledDocumentTakesChosenPreferences()
    {
        using var plain = PdfDocumentReader.Open(new RenderTestPdf(Edge, Edge).ToBytes(), null);
        using var withPreferences = PdfDocumentReader.Open(new RenderTestPdf(Edge, Edge) { CatalogEntries = "/ViewerPreferences << /HideMenubar true >>" }.ToBytes(), null);
        PdfPageReference[] pages = [new(0, 0), new(1, 0)];
        var without = PdfDocumentAssembly.Assemble([plain, withPreferences], pages, CancellationToken.None);
        var taken = PdfDocumentAssembly.Assemble([plain, withPreferences], pages, new() { ViewerPreferencesSource = PreferencePages }, CancellationToken.None);
        using var withoutResult = PdfDocumentReader.Open(without, null);
        using var takenResult = PdfDocumentReader.Open(taken, null);

        await Assert.That(PdfDocumentViewerPreferences.GetViewerPreferences(withoutResult).HideMenubar).IsFalse();
        await Assert.That(PdfDocumentViewerPreferences.GetViewerPreferences(takenResult).HideMenubar).IsTrue();
        await Assert.That(() => PdfDocumentAssembly.Assemble([plain], [new(0, 0)], new() { ViewerPreferencesSource = PreferencePages }, CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Builds a page with a square annotation whose appearance fills its rectangle.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] WithAnnotation()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Content = "1 0 0 rg 0 0 200 200 re f" };
        var appearance = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 40 20]", "0 0 1 rg 0 0 40 20 re f");
        pdf.PageEntries = $"/Annots [<< /Type /Annot /Subtype /Square /Rect [10 10 50 30] /F 4 /AP << /N {appearance} 0 R >> >>]";
        return pdf.ToBytes();
    }
}
