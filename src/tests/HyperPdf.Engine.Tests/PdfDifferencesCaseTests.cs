// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using TUnit.Assertions.Enums;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Runs the PDF Association differences collection (pdf-association/pdf-differences, CC BY 4.0) through HyperPDF: every case opens
/// and draws, and the cases that say a result in words are checked against it. How each drawing compares with the case's own
/// picture was judged by eye and is recorded in docs/standards/pdf-differences.md. The files come from src/tests/reference-pdfs/manifest.json.
/// </summary>
public sealed partial class PdfDifferencesCaseTests
{
    /// <summary>The bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The most pages drawn for one file.</summary>
    private const int MaxPages = 3;

    /// <summary>The pages of the page labels file.</summary>
    private const int LabelPages = 16;

    /// <summary>The code point of the Eastern Arabic-Indic digit zero.</summary>
    private const char EasternArabicZero = '٠';

    /// <summary>The case whose image uses an unknown filter.</summary>
    private const string ImageCase = "pdfdiff-unknownfilter-imagexobject";

    /// <summary>The case whose form XObject uses an unknown filter.</summary>
    private const string FormCase = "pdfdiff-unknownfilter-formxobject";

    /// <summary>The case whose font program uses an unknown filter.</summary>
    private const string FontCase = "pdfdiff-unknownfilter-font";

    /// <summary>The ids of the processable unknown filter cases, which still show their text.</summary>
    private static readonly string[] ProcessableUnknownFilters =
    [
        FontCase, FormCase, "pdfdiff-unknownfilter-icc", ImageCase,
        "pdfdiff-unknownfilter-linearized", "pdfdiff-unknownfilter-outlineobjstm", "pdfdiff-unknownfilter-xrefstm",
    ];

    /// <summary>Gets the ids of every case of the collection, for <c>[MethodDataSource]</c>.</summary>
    /// <returns>The ids.</returns>
    public static IEnumerable<string> CaseIds() =>
    [
        "pdfdiff-fillstrokeordering", "pdfdiff-selfintersecting-opaque", "pdfdiff-selfintersecting-transparency", "pdfdiff-colorburn", "pdfdiff-colordodge",
        "pdfdiff-degeneratedashing", "pdfdiff-dashing-endbeforebend", "pdfdiff-dashing-endbeforebend2", "pdfdiff-defaultrgbcolourspaces-inherit",
        "pdfdiff-defaultrgbcolourspaces", "pdfdiff-indexedcs-negative-and-high", "pdfdiff-inlineabbreviations", "pdfdiff-largemitrelimit-beziers",
        "pdfdiff-largemitrelimit", "pdfdiff-linecap-degenerate", "pdfdiff-cuthereexample", "pdfdiff-negative-dashphase", "pdfdiff-negativefontsize",
        "pdfdiff-overlappingglyphclipping", "pdfdiff-pdf-versions1", "pdfdiff-pdf-versions2", "pdfdiff-pdf-versions3", "pdfdiff-pagelabelstest",
        "pdfdiff-textclippingmodechanges", "pdfdiff-type3test", FontCase, FormCase, "pdfdiff-unknownfilter-icc",
        ImageCase, "pdfdiff-unknownfilter-linearized", "pdfdiff-unknownfilter-outlineobjstm",
        "pdfdiff-unknownfilter-pagecontentstream", "pdfdiff-unknownfilter-objstm", "pdfdiff-unknownfilter-xrefstm", "pdfdiff-verticaltext",
        "pdfdiff-real-world-1", "pdfdiff-real-world-2",
    ];

    /// <summary>Gets the ids of the cases that say a PDF 1.6 file, for <c>[MethodDataSource]</c>.</summary>
    /// <returns>The ids.</returns>
    public static IEnumerable<string> VersionIds() => ["pdfdiff-pdf-versions1", "pdfdiff-pdf-versions2", "pdfdiff-pdf-versions3"];

    /// <summary>Gets the ids of the cases whose page draws an image, form, font or content stream with a filter nobody knows, for <c>[MethodDataSource]</c>.</summary>
    /// <returns>The ids.</returns>
    public static IEnumerable<string> UnknownFilterIds() =>
        [ImageCase, FormCase, FontCase, "pdfdiff-unknownfilter-pagecontentstream"];

    /// <summary>Gets the ids of the cases that keep their text although a stream uses an unknown filter, for <c>[MethodDataSource]</c>.</summary>
    /// <returns>The ids.</returns>
    public static IEnumerable<string> TextIds() => ProcessableUnknownFilters;

    /// <summary>Every case opens and draws its pages without failing, including those whose streams use a filter nobody knows.</summary>
    /// <param name="id">The case.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(CaseIds))]
    public async Task EveryCaseOpensAndDraws(string id)
    {
        using var document = new HyperPdfEngine().Open(ReferencePdfFile.RequirePaths(id)[0], null);

        var drawn = 0;
        for (var page = 0; page < Math.Min(document.PageCount, MaxPages); page++)
        {
            var size = document.GetPageSizes()[page];
            var width = (int)Math.Round(size.Width);
            var height = (int)Math.Round(size.Height);
            var pixels = new byte[width * height * BytesPerPixel];
            drawn += document.Render(new(page, 1, PageRotation.None, 0, 0, RenderFlags.Annotations), new(pixels, width, height, width * BytesPerPixel)) ? 1 : 0;
        }

        await Assert.That(document.PageCount).IsGreaterThan(0);
        await Assert.That(drawn).IsEqualTo(Math.Min(document.PageCount, MaxPages));
    }

    /// <summary>The version of each file is 1.6: the header's 1.4, upgraded by the catalog's /Version and then by an update's catalog (ISO 32000-2 7.5.2).</summary>
    /// <param name="id">The case.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(VersionIds))]
    public async Task ReportsTheUpgradedVersion(string id)
    {
        using var document = new HyperPdfEngine().Open(ReferencePdfFile.RequirePaths(id)[0], null);

        await Assert.That(document.GetMetadata()?.FormatVersion).IsEqualTo("1.6");
    }

    /// <summary>Each page's label is the one its own text names between quotes (ISO 32000-2 12.4.2).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageLabelsMatchTheLabelsEachPageNames()
    {
        using var document = new HyperPdfEngine().Open(ReferencePdfFile.RequirePaths("pdfdiff-pagelabelstest")[0], null);

        var mismatched = new List<string>();
        for (var page = 0; page < document.PageCount; page++)
        {
            var text = document.GetText(page, 0, document.GetCharacterCount(page));
            var expected = QuotedLabel().Match(text) is { Success: true } named ? named.Groups[1].Value : EasternArabicLabel(text);
            if (expected != document.GetPageLabel(page))
            {
                mismatched.Add($"{page}: {document.GetPageLabel(page)} vs {text}");
            }
        }

        await Assert.That(document.PageCount).IsEqualTo(LabelPages);
        await Assert.That(mismatched).IsEmpty();
    }

    /// <summary>A file whose unknown filter is on a stream the page can do without still shows the page's text.</summary>
    /// <param name="id">The case.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(TextIds))]
    public async Task KeepsTheTextWhenAnOptionalStreamUsesAnUnknownFilter(string id)
    {
        using var document = new HyperPdfEngine().Open(ReferencePdfFile.RequirePaths(id)[0], null);

        await Assert.That(document.GetText(0, 0, document.GetCharacterCount(0))).Contains("Hello!");
    }

    /// <summary>An image whose filter is unknown is left out instead of drawn as noise on black, and the reader is told in words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LeavesOutAnImageWithAnUnknownFilterAndSaysSo()
    {
        using var document = new HyperPdfEngine().Open(ReferencePdfFile.RequirePaths(ImageCase)[0], null);

        var content = DocumentFeature<IContentCheck>.Cast(document)!.CheckPage(0);
        var warning = ContentWarnings.Describe(content);

        await Assert.That(content & UnsupportedContent.UnreadableImages).IsEqualTo(UnsupportedContent.UnreadableImages);
        await Assert.That(warning).IsNotNull().And.Contains("images in an unknown format");
    }

    /// <summary>Every case that names an unknown filter on a stream the page can do without tells the reader part of the page was left out.</summary>
    /// <param name="id">The case.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(UnknownFilterIds))]
    public async Task SaysWhenAPageUsesAFilterTheViewerCannotRead(string id)
    {
        using var document = new HyperPdfEngine().Open(ReferencePdfFile.RequirePaths(id)[0], null);

        var content = DocumentFeature<IContentCheck>.Cast(document)!.CheckPage(0);

        await Assert.That(content & (UnsupportedContent.UnreadableImages | UnsupportedContent.UnreadableStreams)).IsNotEqualTo(UnsupportedContent.None);
        await Assert.That(ContentWarnings.Describe(content)).IsNotNull().And.Contains("cannot show");
    }

    /// <summary>Text drawn with a negative or a zero font size is read line by line in the order it is drawn, never mixed with the line it overlaps.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsEachLineTogetherWhenTheTextIsMirrored()
    {
        using var document = new HyperPdfEngine().Open(ReferencePdfFile.RequirePaths("pdfdiff-negativefontsize")[0], null);

        var lines = document.GetText(0, 0, document.GetCharacterCount(0)).Split('\n', StringSplitOptions.TrimEntries);
        var expected = new List<string>();
        foreach (var how in new[] { "Tf", "gs" })
        {
            foreach (var font in new[] { "Helvetica", "Times-Roman" })
            {
                expected.AddRange(new[] { "+20", "-20", "zero" }.Select(size => $"{font} {size} font size via {how} operator"));
            }
        }

        await Assert.That(lines).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    /// <summary>Gets the label of a page that names its label in words, as in "index 8 = Eastern Arabic 4", which is written with Eastern Arabic-Indic digits.</summary>
    /// <param name="text">The page's text.</param>
    /// <returns>The label, or an empty string when the text does not name one.</returns>
    private static string EasternArabicLabel(string text) =>
        text.TrimEnd() is { Length: > 0 } trimmed && text.Contains("Eastern Arabic ", StringComparison.Ordinal) && char.IsAsciiDigit(trimmed[^1])
            ? ((char)(EasternArabicZero + (trimmed[^1] - '0'))).ToString()
            : string.Empty;

    /// <summary>Finds the label a page of the page labels file names in its text, as in <c>index 2 = "iii"</c>.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("= \"(.*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedLabel();
}
