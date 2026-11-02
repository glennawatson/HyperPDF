// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Builds the small PDF/X files the report tests read.</summary>
public static class PdfXFixtures
{
    /// <summary>An Info dictionary that makes a complete PDF/X-4 claim.</summary>
    internal const string GoodInfo = "<< /GTS_PDFXVersion (PDF/X-4) /Title (Job) /Trapped /False >>";

    /// <summary>A page with a trim box.</summary>
    internal const string TrimPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /TrimBox [5 5 95 95] >>";

    /// <summary>An output intent with an identifier and a registry but no profile.</summary>
    internal const string RegistryIntent = "/OutputIntents [<< /Type /OutputIntent /S /GTS_PDFX /OutputConditionIdentifier (FOGRA39) /RegistryName (http://www.color.org) >>]";

    /// <summary>The header minor digit PDF/X-4 allows.</summary>
    internal const char Pdf16 = '6';

    /// <summary>The header minor digit that is too new for PDF/X-4.</summary>
    internal const char Pdf17 = '7';

    /// <summary>The offset of the header's minor version digit in <c>%PDF-1.7</c>.</summary>
    private const int HeaderMinorOffset = 7;

    /// <summary>The object number of the second object after the Info dictionary.</summary>
    private const int SecondExtra = 6;

    /// <summary>The object number of the third object after the Info dictionary.</summary>
    private const int ThirdExtra = 7;

    /// <summary>The BleedBox key.</summary>
    private const string BleedKey = "/BleedBox";

    /// <summary>The graphics state resource entry.</summary>
    private const string StateResources = "/ExtGState << /G0 5 0 R >>";

    /// <summary>The image dictionary entries.</summary>
    private const string ImageEntries = "/Type /XObject /Subtype /Image /Width 1 /Height 1 /BitsPerComponent 8 /ColorSpace /DeviceGray";

    /// <summary>Gets the content that PDF/X forbids with the rule each breaks.</summary>
    /// <returns>The data rows.</returns>
    public static IEnumerable<Func<ForbiddenCase>> ForbiddenContent()
    {
        yield return static () => new("Fonts", string.Empty, ContentPage("/Font << /F1 5 0 R >>", SecondExtra), UnembeddedFontObjects());
        yield return static () => new("LzwFilter", string.Empty, TrimPage, [MiniPdf.Stream("/Filter /LZWDecode", "x")]);
        yield return static () => new("JavaScript", string.Empty, TrimPage, ["<< /S /JavaScript /JS (app.alert\\(1\\)) >>"]);
        yield return static () => new("ExternalContent", string.Empty, TrimPage, [MiniPdf.Stream("/F (ext.bin)", "x")]);
        yield return static () => new("EmbeddedFiles", "/Names << /EmbeddedFiles << /Names [(a.txt) 5 0 R] >> >>", TrimPage, AttachmentObjects());
        yield return static () => new("TransferCurves", string.Empty, StatePage(), ["<< /Type /ExtGState /TR /Identity >>", UseState()]);
        yield return static () => new("TransferCurves", string.Empty, StatePage(), ["<< /Type /ExtGState /TR2 /Other >>", UseState()]);
        yield return static () => new("AlternateImages", string.Empty, ContentPage("/XObject << /Im 5 0 R >>", ThirdExtra), AlternateImageObjects());
    }

    /// <summary>Builds a trim-box page whose resources and content stream are given.</summary>
    /// <param name="resources">The resource entries.</param>
    /// <param name="contents">The content stream's object number.</param>
    /// <returns>The page object text.</returns>
    public static string ContentPage(string resources, int contents) =>
        $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /TrimBox [5 5 95 95] /Resources << {resources} >> /Contents {contents} 0 R >>";

    /// <summary>Builds a page with a trim box and the given bleed box.</summary>
    /// <param name="bleed">The bleed box array text.</param>
    /// <returns>The page object text.</returns>
    public static string BleedPage(string bleed) =>
        $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /TrimBox [5 5 95 95] {BleedKey} {bleed} >>";

    /// <summary>Builds the graphics state resources page that draws with the state in object five.</summary>
    /// <returns>The page object text.</returns>
    public static string StatePage() => ContentPage(StateResources, SecondExtra);

    /// <summary>Builds the content stream that applies the graphics state named G0.</summary>
    /// <returns>The stream object text.</returns>
    public static string UseState() => MiniPdf.Stream(string.Empty, "/G0 gs");

    /// <summary>Builds the bytes of a one-page file.</summary>
    /// <param name="info">The Info dictionary.</param>
    /// <param name="catalog">Extra catalog entries.</param>
    /// <param name="page">The page object.</param>
    /// <param name="minor">The header's minor version digit.</param>
    /// <param name="extras">The objects from number 5.</param>
    /// <returns>The file bytes.</returns>
    public static byte[] Build(string info, string catalog, string page, char minor, params string[] extras)
    {
        string[] objects =
        [
            $"<< /Type /Catalog /Pages 2 0 R {catalog} >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            page,
            info,
            .. extras,
        ];
        var bytes = MiniPdf.BuildWithTrailer("/Info 4 0 R", objects);

        // The builder writes a PDF 1.7 header; PDF/X-4 is based on PDF 1.6, so change the header's minor digit.
        bytes[HeaderMinorOffset] = (byte)minor;
        return bytes;
    }

    /// <summary>Builds a font without an embedded program and the content stream that shows text with it.</summary>
    /// <returns>The objects from number 5.</returns>
    private static string[] UnembeddedFontObjects() =>
    [
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        MiniPdf.Stream(string.Empty, "BT /F1 12 Tf (hi) Tj ET"),
    ];

    /// <summary>Builds a file specification with an embedded file stream.</summary>
    /// <returns>The objects from number 5.</returns>
    private static string[] AttachmentObjects() =>
    [
        $"<< /Type /Filespec /F (a.txt) /EF << /F {SecondExtra} 0 R >> >>",
        MiniPdf.Stream("/Type /EmbeddedFile", "x"),
    ];

    /// <summary>Builds an image with alternates, the alternate and the content stream that draws the image.</summary>
    /// <returns>The objects from number 5.</returns>
    private static string[] AlternateImageObjects() =>
    [
        MiniPdf.Stream($"{ImageEntries} /Alternates [<< /Image {SecondExtra} 0 R >>]", "x"),
        MiniPdf.Stream(ImageEntries, "y"),
        MiniPdf.Stream(string.Empty, "q 1 0 0 1 0 0 cm /Im Do Q"),
    ];

    /// <summary>Forbidden content and the rule it breaks.</summary>
    /// <param name="Rule">The rule that should fail.</param>
    /// <param name="Catalog">Extra catalog entries.</param>
    /// <param name="Page">The page object.</param>
    /// <param name="Extras">The objects from number 5.</param>
    public sealed record ForbiddenCase(string Rule, string Catalog, string Page, string[] Extras);
}
