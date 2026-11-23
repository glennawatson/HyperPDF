// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for <see cref="PdfBlankPages"/>.</summary>
public sealed class PdfBlankPagesTests
{
    /// <summary>The pages of the starting document.</summary>
    private const int StartPages = 3;

    /// <summary>The width of the blank page in points.</summary>
    private const float Width = 420;

    /// <summary>The height of the blank page in points.</summary>
    private const float Height = 595;

    /// <summary>A blank page goes where it is asked to, at the size asked for, and is still there after the file is saved and read again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InsertPage_AddsAPageOfTheSizeAskedFor()
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(StartPages), null);

        PdfBlankPages.InsertPage(document, 1, Width, Height);
        var added = PdfDocumentPages.GetPage(document, 1).MediaBox;
        using var reopened = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), null);
        var saved = PdfDocumentPages.GetPage(reopened, 1).MediaBox;

        await Assert.That(document.PageCount).IsEqualTo(StartPages + 1);
        await Assert.That(added.Width).IsEqualTo(Width);
        await Assert.That(added.Height).IsEqualTo(Height);
        await Assert.That(reopened.PageCount).IsEqualTo(StartPages + 1);
        await Assert.That(saved.Width).IsEqualTo(Width);
        await Assert.That(saved.Height).IsEqualTo(Height);
    }

    /// <summary>A blank page can go after the last page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InsertPage_AtThePageCount_AddsAtTheEnd()
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(StartPages), null);

        PdfBlankPages.InsertPage(document, StartPages, Width, Height);

        await Assert.That(document.PageCount).IsEqualTo(StartPages + 1);
        await Assert.That(PdfDocumentPages.GetPage(document, StartPages).MediaBox.Width).IsEqualTo(Width);
    }

    /// <summary>An index outside the document and a side that is not a length above zero are refused, and nothing is added.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InsertPage_BadIndexOrSide_Throws()
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(StartPages), null);

        await Assert.That(() => PdfBlankPages.InsertPage(document, StartPages + 1, Width, Height)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfBlankPages.InsertPage(document, -1, Width, Height)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfBlankPages.InsertPage(document, 0, 0, Height)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfBlankPages.InsertPage(document, 0, Width, float.NaN)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(document.PageCount).IsEqualTo(StartPages);
    }
}
