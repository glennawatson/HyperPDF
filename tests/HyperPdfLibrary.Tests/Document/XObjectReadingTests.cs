// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.XObjects;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for image alternates and form XObject references (ISO 32000-2 §8.9.5.4, §8.10.4).</summary>
public sealed class XObjectReadingTests
{
    /// <summary>The object number of the main image.</summary>
    private const int MainNumber = 4;

    /// <summary>The object number of the first alternate image.</summary>
    private const int FirstAlternateNumber = 5;

    /// <summary>The object number of the second alternate image.</summary>
    private const int SecondAlternateNumber = 6;

    /// <summary>The object number of the optional content group.</summary>
    private const int GroupNumber = 7;

    /// <summary>The number of alternates in the first sample.</summary>
    private const int AlternateCount = 2;

    /// <summary>The page index in the reference sample.</summary>
    private const int ReferencePage = 3;

    /// <summary>The number of alternates listed in the long sample.</summary>
    private const int ListedAlternates = 1_000;

    /// <summary>The most alternates the reader keeps.</summary>
    private const int KeptAlternates = 256;

    /// <summary>The entries every sample image has apart from its alternates.</summary>
    private const string ImageEntries = "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8";

    /// <summary>The alternates and printing default of an image are read in array order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AlternatesAreReadWithPrintingDefault()
    {
        using var document = Open(
            "/Alternates [<< /Image 5 0 R >> << /Image 6 0 R /DefaultForPrinting true /OC 7 0 R >>]",
            MiniPdf.Stream(ImageEntries, "a"),
            MiniPdf.Stream(ImageEntries, "b"),
            "<< /Type /OCG /Name (Print) >>");
        var main = Image(document, MainNumber);

        var alternates = PdfDocumentXObjects.GetImageAlternates(document, main);

        await Assert.That(alternates.Length).IsEqualTo(AlternateCount);
        await Assert.That(alternates[0].Image).IsSameReferenceAs(Image(document, FirstAlternateNumber));
        await Assert.That(alternates[0].DefaultForPrinting).IsFalse();
        await Assert.That(alternates[0].OptionalContent).IsNull();
        await Assert.That(alternates[1].Image).IsSameReferenceAs(Image(document, SecondAlternateNumber));
        await Assert.That(alternates[1].DefaultForPrinting).IsTrue();
        await Assert.That(alternates[1].OptionalContent).IsSameReferenceAs(StructureDocuments.Dictionary(document, GroupNumber));
    }

    /// <summary>Screen use keeps the main image; print use takes the printing default when there is one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScreenKeepsMainImageAndPrintTakesTheDefault()
    {
        using var document = Open(
            "/Alternates [<< /Image 5 0 R >> << /Image 6 0 R /DefaultForPrinting true >>]",
            MiniPdf.Stream(ImageEntries, "a"),
            MiniPdf.Stream(ImageEntries, "b"));
        using var withoutDefault = Open("/Alternates [<< /Image 5 0 R >>]", MiniPdf.Stream(ImageEntries, "a"));
        using var plain = Open(string.Empty);
        var main = Image(document, MainNumber);

        await Assert.That(PdfDocumentXObjects.SelectImage(document, main, PdfImageUse.Screen)).IsSameReferenceAs(main);
        await Assert.That(PdfDocumentXObjects.SelectImage(document, main, PdfImageUse.Print)).IsSameReferenceAs(Image(document, SecondAlternateNumber));
        var noDefault = Image(withoutDefault, MainNumber);
        await Assert.That(PdfDocumentXObjects.SelectImage(withoutDefault, noDefault, PdfImageUse.Print)).IsSameReferenceAs(noDefault);
        var alone = Image(plain, MainNumber);
        await Assert.That(PdfDocumentXObjects.SelectImage(plain, alone, PdfImageUse.Print)).IsSameReferenceAs(alone);
        await Assert.That(PdfDocumentXObjects.GetImageAlternates(plain, alone)).IsEmpty();
    }

    /// <summary>Entries that are not images, repeat an image, or name the image itself are skipped, so loops cannot form.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CyclicAndDamagedAlternatesAreSkipped()
    {
        using var document = Open(
            "/Alternates [<< /Image 4 0 R >> << /Image 5 0 R >> << /Image 5 0 R /DefaultForPrinting true >> << /Image 7 0 R >> 42 (text) << >> << /Image 99 0 R >>]",
            MiniPdf.Stream($"{ImageEntries} /Alternates [<< /Image 4 0 R >> << /Image 5 0 R >>]", "a"),
            "<< /Type /Other >>",
            "<< /Type /XObject /Subtype /Form >>");
        var main = Image(document, MainNumber);
        var alternate = Image(document, FirstAlternateNumber);

        var alternates = PdfDocumentXObjects.GetImageAlternates(document, main);

        await Assert.That(alternates.Length).IsEqualTo(1);
        await Assert.That(alternates[0].Image).IsSameReferenceAs(alternate);
        await Assert.That(PdfDocumentXObjects.GetImageAlternates(document, alternate).Single().Image).IsSameReferenceAs(main);
    }

    /// <summary>A very long alternates array is read only up to the limit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LongAlternateListsAreBounded()
    {
        var entries = string.Concat(Enumerable.Repeat("<< /Image 5 0 R >> ", ListedAlternates));
        using var document = Open($"/Alternates [{entries}]", MiniPdf.Stream(ImageEntries, "a"));

        var alternates = PdfDocumentXObjects.GetImageAlternates(document, Image(document, MainNumber));

        await Assert.That(alternates.Length).IsLessThanOrEqualTo(KeptAlternates);
    }

    /// <summary>An <c>/Alternates</c> value that is not an array gives no alternates.</summary>
    /// <param name="value">The value text.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("42")]
    [Arguments("(text)")]
    [Arguments("<< /Image 5 0 R >>")]
    [Arguments("null")]
    public async Task NonArrayAlternatesGiveNone(string value)
    {
        using var document = Open($"/Alternates {value}", MiniPdf.Stream(ImageEntries, "a"));

        await Assert.That(PdfDocumentXObjects.GetImageAlternates(document, Image(document, MainNumber))).IsEmpty();
    }

    /// <summary>A form's <c>/Ref</c> dictionary gives its file, page, identifiers and proxy.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReferenceIsReadWithoutOpeningTheFile()
    {
        using var document = StructureDocuments.Open(
            string.Empty,
            MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Ref << /F (/definitely/not/a/real/file.pdf) /Page 3 /ID [(AAAA) (BB)] >>", "0 g 0 0 10 10 re f"));
        var form = Image(document, MainNumber);

        var reference = PdfDocumentXObjects.GetReference(document, form)!;

        await Assert.That(reference.FileName).IsEqualTo("/definitely/not/a/real/file.pdf");
        await Assert.That(reference.IsUrl).IsFalse();
        await Assert.That(reference.PageIndex).IsEqualTo(ReferencePage);
        await Assert.That(reference.PageLabel).IsNull();
        await Assert.That(Encoding.ASCII.GetString(reference.PermanentId!)).IsEqualTo("AAAA");
        await Assert.That(Encoding.ASCII.GetString(reference.ChangingId!)).IsEqualTo("BB");
        await Assert.That(reference.Proxy).IsSameReferenceAs(form);
        await Assert.That(File.Exists("/definitely/not/a/real/file.pdf")).IsFalse();
    }

    /// <summary>A page label, a URL specification and damaged members are read as data.</summary>
    /// <param name="reference">The <c>/Ref</c> value.</param>
    /// <param name="fileName">The expected file name.</param>
    /// <param name="isUrl">The expected URL flag.</param>
    /// <param name="label">The expected page label.</param>
    /// <param name="page">The expected page index, or -1 for none.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("<< /F << /FS /URL /F (https://example.test/a.pdf) >> /Page (iv) /ID [(A)] >>", "https://example.test/a.pdf", true, "iv", -1)]
    [Arguments("<< /F (other.pdf) /Page -1 >>", "other.pdf", false, null, -1)]
    [Arguments("<< /F 42 /Page [1] /ID (x) >>", "", false, null, -1)]
    [Arguments("<< >>", "", false, null, -1)]
    public async Task ReferenceMembersAreReadAsData(string reference, string fileName, bool isUrl, string? label, int page)
    {
        using var document = StructureDocuments.Open(string.Empty, MiniPdf.Stream($"/Type /XObject /Subtype /Form /BBox [0 0 1 1] /Ref {reference}", string.Empty));

        var read = PdfDocumentXObjects.GetReference(document, Image(document, MainNumber))!;

        await Assert.That(read.FileName).IsEqualTo(fileName);
        await Assert.That(read.IsUrl).IsEqualTo(isUrl);
        await Assert.That(read.PageLabel).IsEqualTo(label);
        await Assert.That(read.PageIndex ?? -1).IsEqualTo(page);
        await Assert.That(read.PermanentId).IsNull();
        await Assert.That(read.ChangingId).IsNull();
    }

    /// <summary>A form without <c>/Ref</c>, or with a <c>/Ref</c> that is not a dictionary, has no reference.</summary>
    /// <param name="entries">The extra form entries.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("")]
    [Arguments("/Ref 42")]
    [Arguments("/Ref (text)")]
    public async Task FormWithoutReferenceGivesNull(string entries)
    {
        using var document = StructureDocuments.Open(string.Empty, MiniPdf.Stream($"/Type /XObject /Subtype /Form /BBox [0 0 1 1] {entries}", string.Empty));

        await Assert.That(PdfDocumentXObjects.GetReference(document, Image(document, MainNumber))).IsNull();
    }

    /// <summary>A form whose <c>/Ref</c> points at the form's own dictionary reads once and stops.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SelfReferencingReferenceStops()
    {
        using var document = StructureDocuments.Open(
            string.Empty,
            MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 1 1] /Ref << /F 4 0 R /Page 0 >>", string.Empty));

        var reference = PdfDocumentXObjects.GetReference(document, Image(document, MainNumber))!;

        await Assert.That(reference.PageIndex).IsEqualTo(0);
        await Assert.That(reference.FileName).IsEmpty();
    }

    /// <summary>Opens a document whose object 4 is an image with extra entries, followed by more objects.</summary>
    /// <param name="mainEntries">Extra entries of the main image.</param>
    /// <param name="more">The objects numbered from 5.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string mainEntries, params string[] more) =>
        StructureDocuments.Open(string.Empty, [MiniPdf.Stream($"{ImageEntries} {mainEntries}", "m"), .. more]);

    /// <summary>Gets an indirect stream object.</summary>
    /// <param name="document">The document.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The stream.</returns>
    private static PdfStream Image(PdfDocument document, int number) => StoreReading.GetObject(document.Objects, new(number, 0)).AsStream()!;
}
