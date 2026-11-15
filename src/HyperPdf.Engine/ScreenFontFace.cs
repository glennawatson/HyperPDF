// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdf.Engine;

/// <summary>
/// One face of a font file opened for drawing text on screen: its outlines and metrics, and the source its layout tables are
/// shaped from. A face of a font collection is opened by its index.
/// </summary>
[DebuggerDisplay("ScreenFontFace: {Source.FamilyName}")]
public sealed class ScreenFontFace
{
    /// <summary>Initializes a new instance of the <see cref="ScreenFontFace"/> class.</summary>
    /// <param name="source">The source the face is shaped from.</param>
    /// <param name="program">The face's outlines and metrics.</param>
    private ScreenFontFace(PdfFontSource source, TrueTypeProgram program)
    {
        Source = source;
        Program = program;
    }

    /// <summary>Gets the source the face is shaped from.</summary>
    public PdfFontSource Source { get; }

    /// <summary>Gets the face's outlines and metrics.</summary>
    public TrueTypeProgram Program { get; }

    /// <summary>Opens one face of a font file.</summary>
    /// <param name="fontFile">The font file's bytes, which the face keeps.</param>
    /// <param name="faceIndex">The face's index in a font collection; zero for a single font.</param>
    /// <returns>The face, or <see langword="null"/> when the file is not a font the library reads or has no such face.</returns>
    public static ScreenFontFace? Open(byte[] fontFile, int faceIndex)
    {
        ArgumentNullException.ThrowIfNull(fontFile);
        if (PdfFontSource.Open(fontFile, faceIndex).Source is not { } source)
        {
            return null;
        }

        // A face of a collection is copied out once, so its outlines read as a font of their own.
        return TrueTypeProgram.TryParse(source.Font.ToStandalone(), out var program) ? new(source, program) : null;
    }

    /// <summary>Opens a face of the screen font that ships inside the application.</summary>
    /// <param name="family">The family: Arimo, Tinos or Cousine.</param>
    /// <param name="bold">Whether the face is bold.</param>
    /// <returns>The face, or <see langword="null"/> when the bundled font is missing or damaged.</returns>
    public static ScreenFontFace? OpenBundled(string family, bool bold) =>
        BundledScreenFonts.Read(family, bold, false) is { } file ? Open(file, 0) : null;
}
