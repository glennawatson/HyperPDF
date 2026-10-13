// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Fonts.Embedding;

/// <summary>Builds one-page documents that show text in an embedded font.</summary>
internal static class EmbeddedFontPdf
{
    /// <summary>The environment variable naming a folder where the generated documents are kept for outside checks.</summary>
    internal const string OutputVariable = "HYPERPDF_FONT_OUTPUT";

    /// <summary>The page width.</summary>
    internal const int PageWidth = 300;

    /// <summary>The page height.</summary>
    internal const int PageHeight = 200;

    /// <summary>The font size of every line.</summary>
    internal const float FontSize = 12F;

    /// <summary>The left edge of every line.</summary>
    private const float Left = 20F;

    /// <summary>The distance of the first baseline from the bottom.</summary>
    private const float Top = 150F;

    /// <summary>The distance between baselines.</summary>
    private const float LineHeight = 20F;

    /// <summary>Gets the name the font has in the page resources.</summary>
    private static ReadOnlySpan<byte> ResourceName => "F1"u8;

    /// <summary>Opens a face and fails the test when it is refused.</summary>
    /// <param name="file">The font file.</param>
    /// <param name="face">The face's index.</param>
    /// <returns>The font.</returns>
    /// <exception cref="InvalidOperationException">The font is refused; the message is the reason.</exception>
    internal static PdfFontSource Open(byte[] file, int face)
    {
        var result = PdfFontSource.Open(file, face);
        return result.Source ?? throw new InvalidOperationException(result.Refusal);
    }

    /// <summary>Embeds a font in a new document that shows each line of text on a line of the page.</summary>
    /// <param name="source">The font.</param>
    /// <param name="options">How the font is embedded.</param>
    /// <param name="lines">The lines of text.</param>
    /// <returns>The PDF file.</returns>
    internal static byte[] Lines(PdfFontSource source, PdfEmbeddedFontOptions options, params string[] lines)
    {
        var builder = new PdfDocumentBuilder();
        var font = PdfEmbeddedFont.Create(builder, source, options);
        return Build(builder, font, lines);
    }

    /// <summary>Writes a document that shows each line of text in a font that already belongs to the builder.</summary>
    /// <param name="builder">The document.</param>
    /// <param name="font">The font, created in the builder.</param>
    /// <param name="lines">The lines of text.</param>
    /// <returns>The PDF file bytes.</returns>
    internal static byte[] Build(PdfDocumentBuilder builder, PdfEmbeddedFont font, string[] lines)
    {
        var content = default(PdfContentBuilder);
        try
        {
            content.BeginText();
            content.SetFont(ResourceName, FontSize);
            content.MoveText(Left, Top);
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    content.MoveText(0, -LineHeight);
                }

                font.ShowText(ref content, lines[i], FontSize);
            }

            content.EndText();
            return Finish(builder, font, content.ToArray());
        }
        finally
        {
            content.Dispose();
        }
    }

    /// <summary>Finishes the font, puts the content on a page and writes the document.</summary>
    /// <param name="builder">The document.</param>
    /// <param name="font">The font.</param>
    /// <param name="content">The page content.</param>
    /// <returns>The finished PDF file.</returns>
    internal static byte[] Finish(PdfDocumentBuilder builder, PdfEmbeddedFont font, byte[] content)
    {
        font.Finish();
        var fonts = new PdfDictionary(null);
        fonts.Set(builder.Names.Intern(ResourceName), font.Resource);
        var resources = new PdfDictionary(null);
        resources.Set(KnownName.Font, PdfValue.FromDictionary(fonts));
        var page = new PdfDictionary(null);
        page.Set(KnownName.MediaBox, PdfValue.FromArray(PdfArray.FromNumbers(null, [0, 0, PageWidth, PageHeight])));
        page.Set(KnownName.Resources, PdfValue.FromDictionary(resources));
        var stream = new PdfStream(new PdfDictionary(null), content);
        page.Set(KnownName.Contents, PdfValue.FromReference(builder.Add(PdfValue.FromStream(stream))));
        _ = builder.AddPage(page);
        return builder.ToArray();
    }

    /// <summary>Keeps a document for outside checks such as <c>qpdf --check</c> when the output variable names a folder.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="pdf">The document.</param>
    internal static void Keep(string name, byte[] pdf)
    {
        if (Environment.GetEnvironmentVariable(OutputVariable) is not { Length: > 0 } folder)
        {
            return;
        }

        _ = Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name), pdf);
    }
}
