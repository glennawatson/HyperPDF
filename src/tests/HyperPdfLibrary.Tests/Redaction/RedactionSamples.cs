// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Redaction;
using HyperPdfLibrary.Tests.PageObjects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Redaction;

/// <summary>Builds pages with something to redact and runs redaction on them.</summary>
internal static class RedactionSamples
{
    /// <summary>The secret the pages hide.</summary>
    internal const string Secret = "SECRET-ALPHA";

    /// <summary>The first word of the secret.</summary>
    internal const string SecretWord = "SECRET";

    /// <summary>The text that stays on the page.</summary>
    internal const string Public = "Public text";

    /// <summary>The page content with a public line, the secret and another public line.</summary>
    internal const string ThreeLines = "BT /F1 20 Tf 20 150 Td (Public text) Tj 0 -30 Td (SECRET-ALPHA) Tj 0 -30 Td (More public) Tj ET";

    /// <summary>The left edge of the secret line's area.</summary>
    private const float LineLeft = 18;

    /// <summary>The right edge of the areas.</summary>
    private const float LineRight = 190;

    /// <summary>The bottom of the areas: above the line below the secret.</summary>
    private const float BandBottom = 108;

    /// <summary>The top of the areas: below the line above the secret.</summary>
    private const float BandTop = 138;

    /// <summary>Where "ALPHA" starts after "SECRET-" at 20 points.</summary>
    private const float AlphaLeft = 108;

    /// <summary>Gets an area around the secret line of <see cref="ThreeLines"/>.</summary>
    internal static PdfRectangle SecretLine { get; } = new(LineLeft, BandBottom, LineRight, BandTop);

    /// <summary>Gets an area around "ALPHA" in the secret line.</summary>
    internal static PdfRectangle AlphaArea { get; } = new(AlphaLeft, BandBottom, LineRight, BandTop);

    /// <summary>Gets an area around "SECRET-" in the secret line.</summary>
    internal static PdfRectangle PrefixArea { get; } = new(LineLeft, BandBottom, AlphaLeft, BandTop);

    /// <summary>Marks areas of the first page with black bars.</summary>
    /// <param name="document">The document.</param>
    /// <param name="regions">The areas.</param>
    internal static void Mark(PdfDocument document, params PdfRectangle[] regions) => _ = PdfRedactions.Add(document, 0, regions, PdfRedactionAppearance.Black);

    /// <summary>Marks the first page, applies the redactions and returns the saved file.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="options">The options.</param>
    /// <param name="regions">The areas.</param>
    /// <returns>The saved bytes.</returns>
    internal static byte[] Redact(byte[] pdf, PdfRedactionOptions options, params PdfRectangle[] regions)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        Mark(document, regions);
        using var output = new MemoryStream();
        _ = PdfRedactor.ApplyAndSave(document, output, options);
        return output.ToArray();
    }

    /// <summary>Compresses a page's content stream, so its text cannot be found by searching the file.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <returns>The same page with Flate-compressed content.</returns>
    internal static byte[] Compress(byte[] pdf) => PageObjectSamples.Edit(
        pdf,
        static _ =>
    {
    });

    /// <summary>Determines whether a file holds some text, in its bytes or in any stream once decoded.</summary>
    /// <param name="pdf">The file.</param>
    /// <param name="needle">The ASCII text.</param>
    /// <returns><see langword="true"/> when found.</returns>
    internal static bool Contains(byte[] pdf, string needle)
    {
        var bytes = Encoding.ASCII.GetBytes(needle);
        if (pdf.AsSpan().IndexOf(bytes) >= 0)
        {
            return true;
        }

        using var document = PdfDocumentReader.Open(pdf, null);
        for (var number = 1; number < document.Objects.Size; number++)
        {
            if (StoreReading.GetObject(document.Objects, new(number, 0)).AsStream() is { } stream && stream.DecodeToArray().AsSpan().IndexOf(bytes) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the first page's extracted text.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <returns>The text.</returns>
    internal static string TextOf(byte[] pdf)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        return PdfDocumentText.GetTextPage(document, 0).Text;
    }

    /// <summary>Compacts a document as the redactor does, for tests that compare against an unredacted save.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] Save(PdfDocument document) => PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);
}
