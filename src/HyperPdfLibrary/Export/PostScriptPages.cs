// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Export;

/// <summary>Writes the structure of a PostScript document around the pages the content interpreter draws on a <see cref="PostScriptContentDevice"/>.</summary>
internal static class PostScriptPages
{
    /// <summary>Writes pages as one PostScript or Encapsulated PostScript document.</summary>
    /// <param name="cache">The caches the pages are interpreted with.</param>
    /// <param name="pages">The pages, in the order they are written.</param>
    /// <param name="printing">Whether optional content follows print usage.</param>
    /// <param name="annotations">Whether annotation appearances are drawn.</param>
    /// <param name="options">The options.</param>
    /// <param name="output">The stream the document is written to.</param>
    /// <param name="cancellationToken">Cancels the interpreting between operators.</param>
    /// <returns>What was left out or drawn differently.</returns>
    internal static PdfPostScriptReport Write(
        PdfRenderCache cache,
        IReadOnlyList<PdfPage> pages,
        bool printing,
        bool annotations,
        PdfPostScriptOptions options,
        Stream output,
        CancellationToken cancellationToken)
    {
        using var text = new StreamWriter(output, new ASCIIEncoding(), -1, true) { NewLine = "\n" };
        var writer = new PostScriptWriter(text);
        var device = new PostScriptContentDevice(writer);
        WriteHeader(writer, pages, options);
        for (var i = 0; i < pages.Count; i++)
        {
            WritePage(writer, device, cache, pages[i], new(i + 1, pages.Count, printing, annotations, options.Encapsulated), cancellationToken);
        }

        writer.Line("%%Trailer");
        writer.Line("%%EOF");
        text.Flush();
        return new(pages.Count, device.Skipped, device.Approximated);
    }

    /// <summary>Writes the header comments and prolog.</summary>
    /// <param name="writer">The PostScript writer.</param>
    /// <param name="pages">The pages.</param>
    /// <param name="options">The options.</param>
    private static void WriteHeader(PostScriptWriter writer, IReadOnlyList<PdfPage> pages, PdfPostScriptOptions options)
    {
        var width = 0F;
        var height = 0F;
        foreach (var page in pages)
        {
            width = Math.Max(width, page.Width);
            height = Math.Max(height, page.Height);
        }

        writer.Line(options.Encapsulated ? "%!PS-Adobe-3.0 EPSF-3.0" : "%!PS-Adobe-3.0");
        writer.Line("%%Creator: HyperPDF");
        writer.Line(string.Create(CultureInfo.InvariantCulture, $"%%BoundingBox: 0 0 {(int)MathF.Ceiling(width)} {(int)MathF.Ceiling(height)}"));
        writer.Line($"%%HiResBoundingBox: 0 0 {PostScriptWriter.Format(width)} {PostScriptWriter.Format(height)}");
        writer.Line("%%LanguageLevel: 3");
        writer.Line("%%DocumentData: Clean7Bit");
        writer.Line(string.Create(CultureInfo.InvariantCulture, $"%%Pages: {pages.Count}"));
        writer.Line("%%EndComments");
    }

    /// <summary>Writes one page.</summary>
    /// <param name="writer">The PostScript writer.</param>
    /// <param name="device">The device the interpreter draws on.</param>
    /// <param name="cache">The caches the page is interpreted with.</param>
    /// <param name="page">The page.</param>
    /// <param name="position">Which page of the document this is, and what to draw.</param>
    /// <param name="cancellationToken">Cancels the interpreting.</param>
    private static void WritePage(PostScriptWriter writer, PostScriptContentDevice device, PdfRenderCache cache, PdfPage page, in PagePosition position, CancellationToken cancellationToken)
    {
        writer.Line(string.Create(CultureInfo.InvariantCulture, $"%%Page: {position.Number} {position.Count}"));
        if (!position.Encapsulated)
        {
            writer.Word("/setpagedevice where { pop << /PageSize [");
            writer.Number(page.Width);
            writer.Number(page.Height);
            writer.Line("] >> setpagedevice } if");
        }

        // The page is drawn upright by turning the PostScript page over, because the interpreter works in viewer space, which runs down the page.
        writer.Line("gsave");
        writer.Number(0);
        writer.Number(page.Height);
        writer.Word("translate 1 -1 scale");
        writer.Line("/BaseMatrix matrix currentmatrix def");
        writer.Rectangle(0, 0, page.Width, page.Height);
        writer.Line("clip newpath");
        PageRecorder.PlayContent(cache, page, position.Printing, device, cancellationToken);
        if (position.Annotations)
        {
            PageRecorder.PlayAnnotations(cache, page, position.Printing, device, cancellationToken);
        }

        writer.Line("grestore");
        if (!position.Encapsulated)
        {
            writer.Line("showpage");
        }
    }

    /// <summary>Where a page sits in the document and what is drawn on it.</summary>
    /// <param name="Number">The page number from 1.</param>
    /// <param name="Count">The pages in the document.</param>
    /// <param name="Printing">Whether optional content follows print usage.</param>
    /// <param name="Annotations">Whether annotation appearances are drawn.</param>
    /// <param name="Encapsulated">Whether the document is encapsulated.</param>
    private readonly record struct PagePosition(int Number, int Count, bool Printing, bool Annotations, bool Encapsulated);
}
