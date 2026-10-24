// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>A one-page test PDF whose /F1 font is loaded through the library's font loader.</summary>
[DebuggerDisplay("FontTestDocument")]
internal sealed class FontTestDocument : IDisposable
{
    /// <summary>The page width in points.</summary>
    internal const int PageWidth = 200;

    /// <summary>The page height in points.</summary>
    internal const int PageHeight = 100;

    /// <summary>Initializes a new instance of the <see cref="FontTestDocument"/> class.</summary>
    /// <param name="spec">The font.</param>
    /// <exception cref="InvalidOperationException">The font did not load.</exception>
    internal FontTestDocument(FontSpec spec)
        : this(spec, new PdfDrawingServices(new()))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FontTestDocument"/> class. Uses the selected drawing services.</summary>
    /// <param name="spec">The font.</param>
    /// <param name="drawingServices">The services that own font resources.</param>
    /// <exception cref="InvalidOperationException">The font did not load.</exception>
    internal FontTestDocument(FontSpec spec, PdfDrawingServices drawingServices)
    {
        Bytes = Build(spec);
        Document = PdfDocumentReader.OpenWith(Bytes, new PdfOpenOptions { DrawingServices = drawingServices });
        var fonts = PdfDocumentPages.GetPage(Document, 0).Resources?.GetDictionary(KnownName.Font) ?? throw new InvalidOperationException("The page has no fonts.");
        var dictionary = fonts.Get(fonts.GetKeyAt(0)).AsDictionary() ?? throw new InvalidOperationException("The font is not a dictionary.");
        Font = PdfFontLoader.Load(dictionary) ?? throw new InvalidOperationException("The font did not load.");
    }

    /// <summary>Gets the PDF bytes.</summary>
    internal byte[] Bytes { get; }

    /// <summary>Gets the document.</summary>
    internal PdfDocument Document { get; }

    /// <summary>Gets the loaded font.</summary>
    internal PdfFont Font { get; }

    /// <inheritdoc/>
    public void Dispose() => Document.Dispose();

    /// <summary>Writes the PDF of a font spec.</summary>
    /// <param name="spec">The font.</param>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] Build(FontSpec spec)
    {
        var pdf = new RenderTestPdf(PageWidth, PageHeight) { Content = spec.Content };
        var descriptor = AddDescriptor(pdf, spec);
        var toUnicode = spec.ToUnicode is null ? string.Empty : Invariant($"/ToUnicode {pdf.AddStream(string.Empty, spec.ToUnicode)} 0 R");
        int font;
        if (spec.Subtype == "Type0")
        {
            var encoding = spec.CidEncoding.StartsWith('/') ? spec.CidEncoding : Invariant($"{pdf.AddStream("/Type /CMap", spec.CidEncoding)} 0 R");
            var map = spec.CidToGidMap is null ? string.Empty : Invariant($"/CIDToGIDMap {pdf.AddStream(string.Empty, spec.CidToGidMap)} 0 R");
            var systemInfo = Invariant($"/CIDSystemInfo << /Registry (Adobe) /Ordering ({spec.CidOrdering}) /Supplement 0 >>");
            var descendant = pdf.AddObject(Invariant(
                $"<< /Type /Font /Subtype /{spec.CidSubtype} /BaseFont /{spec.BaseFont} {systemInfo} {descriptor} {map} {spec.CidEntries} >>"));
            font = pdf.AddObject(Invariant(
                $"<< /Type /Font /Subtype /Type0 /BaseFont /{spec.BaseFont} /Encoding {encoding} /DescendantFonts [{descendant} 0 R] {toUnicode} {spec.Entries} >>"));
        }
        else
        {
            font = pdf.AddObject(Invariant($"<< /Type /Font /Subtype /{spec.Subtype} /BaseFont /{spec.BaseFont} {descriptor} {toUnicode} {spec.Entries} >>"));
        }

        pdf.Resources = Invariant($"/Font << /F1 {font} 0 R >>");
        return pdf.ToBytes();
    }

    /// <summary>Adds the font descriptor and its program.</summary>
    /// <param name="pdf">The PDF.</param>
    /// <param name="spec">The font.</param>
    /// <returns>The /FontDescriptor entry, or empty when the spec has no descriptor.</returns>
    private static string AddDescriptor(RenderTestPdf pdf, FontSpec spec)
    {
        if (spec.Flags < 0)
        {
            return string.Empty;
        }

        var file = spec.Program is null ? string.Empty : Invariant($"/{spec.FileKey} {pdf.AddStream(spec.FileEntries, spec.Program)} 0 R");
        const string metrics = "/FontBBox [0 -200 1000 900] /ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 700 /StemV 80";
        var descriptor = pdf.AddObject(Invariant(
            $"<< /Type /FontDescriptor /FontName /{spec.BaseFont} /Flags {spec.Flags} {metrics} {file} {spec.DescriptorEntries} >>"));
        return Invariant($"/FontDescriptor {descriptor} 0 R");
    }

    /// <summary>Formats with the invariant culture.</summary>
    /// <param name="text">The interpolated text.</param>
    /// <returns>The text.</returns>
    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
