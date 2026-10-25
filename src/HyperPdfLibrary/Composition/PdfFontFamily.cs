// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Composition;

/// <summary>The faces of one typeface: regular, bold, italic and bold italic. Text asks for the face that matches its weight and slant.</summary>
/// <param name="Regular">The regular face, also used when another face is missing.</param>
[DebuggerDisplay("PdfFontFamily: {Regular}")]
public sealed record PdfFontFamily(PdfCanvasFont Regular)
{
    /// <summary>Gets the bold face, or <see langword="null"/> to use the regular face.</summary>
    public PdfCanvasFont? Bold { get; init; }

    /// <summary>Gets the italic face, or <see langword="null"/> to use the regular face.</summary>
    public PdfCanvasFont? Italic { get; init; }

    /// <summary>Gets the bold italic face, or <see langword="null"/> to use the bold face, then the italic face, then the regular face.</summary>
    public PdfCanvasFont? BoldItalic { get; init; }

    /// <summary>Embeds the four faces of a bundled typeface in a document.</summary>
    /// <param name="creator">The document.</param>
    /// <param name="family">The typeface.</param>
    /// <returns>The family.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="creator"/> is <see langword="null"/>.</exception>
    public static PdfFontFamily FromBundled(PdfDocumentCreator creator, PdfBundledFontFamily family)
    {
        ArgumentNullException.ThrowIfNull(creator);
        return new(Embed(creator, family, false, false)) { Bold = Embed(creator, family, true, false), Italic = Embed(creator, family, false, true), BoldItalic = Embed(creator, family, true, true) };
    }

    /// <summary>Picks the face for a weight and slant.</summary>
    /// <param name="bold">Whether the text is bold.</param>
    /// <param name="italic">Whether the text is italic.</param>
    /// <returns>The nearest face this family has.</returns>
    public PdfCanvasFont Select(bool bold, bool italic)
    {
        if (bold && italic)
        {
            return BoldItalic ?? Bold ?? Italic ?? Regular;
        }

        if (bold)
        {
            return Bold ?? Regular;
        }

        return italic ? Italic ?? Regular : Regular;
    }

    /// <summary>Embeds one face.</summary>
    /// <param name="creator">The document.</param>
    /// <param name="family">The typeface.</param>
    /// <param name="bold">Whether the face is bold.</param>
    /// <param name="italic">Whether the face is italic.</param>
    /// <returns>The font.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfCanvasFont Embed(PdfDocumentCreator creator, PdfBundledFontFamily family, bool bold, bool italic) =>
        creator.EmbedFont(PdfBundledFonts.OpenOrThrow(family, bold, italic));
}
