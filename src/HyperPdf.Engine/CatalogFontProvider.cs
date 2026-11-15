// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Text.Fonts;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdf.Engine;

/// <summary>
/// Finds installed fonts for text whose font is not embedded, from the catalog of font files the app already scans. Font files are
/// read by the library's own parser, so no native font library is needed.
/// </summary>
/// <param name="catalog">The installed fonts, scanned on first use.</param>
[DebuggerDisplay("CatalogFontProvider: {_programs.Count} fonts read")]
internal sealed class CatalogFontProvider(Lazy<FontCatalog> catalog) : IPdfFontProvider
{
    /// <summary>The weight from which a face counts as bold.</summary>
    private const int BoldWeight = 600;

    /// <summary>The weight of a regular face.</summary>
    private const int RegularWeight = 400;

    /// <summary>The weight of a bold face.</summary>
    private const int HeavyWeight = 700;

    /// <summary>The most font files opened to find one that has a character, so a rare character cannot stall a page.</summary>
    private const int MaxProbes = 48;

    /// <summary>The bits of a code point below its block of 256 characters.</summary>
    private const int BlockBits = 8;

    /// <summary>The bundled family used when no installed font matches.</summary>
    private const string DefaultFamily = "Arimo";

    /// <summary>The fonts read so far, by file and face index. A file that could not be read is kept as <see langword="null"/>.</summary>
    private readonly Dictionary<FontFace, TrueTypeProgram?> _programs = [];

    /// <summary>The face found for each block of characters asked for, or <see langword="null"/> when no probed font had it.</summary>
    private readonly Dictionary<int, FontFace?> _blocks = [];

    /// <summary>Guards the two tables.</summary>
    private readonly Lock _gate = new();

    /// <inheritdoc/>
    public IPdfFontFace DefaultFace =>
        ScreenFontFace.OpenBundled(DefaultFamily, false) is { } bundled
            ? new CatalogFontFace(DefaultFamily, RegularWeight, false, bundled.Program)
            : throw new InvalidOperationException("The bundled default font is missing.");

    /// <inheritdoc/>
    public IPdfFontFace? Match(string family, int weight, bool italic)
    {
        ArgumentNullException.ThrowIfNull(family);
        return catalog.Value.Find(family, weight >= BoldWeight, italic) is { } match ? Open(match.Face) : null;
    }

    /// <inheritdoc/>
    public IPdfFontFace? MatchCharacter(int unicode)
    {
        var block = unicode >> BlockBits;
        FontFace? known;
        bool seen;
        lock (_gate)
        {
            seen = _blocks.TryGetValue(block, out known);
        }

        if (seen)
        {
            return known is not null && Open(known) is { } face && face.GetGlyph(unicode) != 0 ? face : null;
        }

        var found = Probe(unicode);
        lock (_gate)
        {
            _blocks[block] = found;
        }

        return found is null ? null : Open(found);
    }

    /// <summary>Tells whether a style name names a bold face.</summary>
    /// <param name="style">The style name, such as "Bold Oblique".</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsBold(string style) => style.Contains("Bold", StringComparison.OrdinalIgnoreCase) || style.Contains("Black", StringComparison.OrdinalIgnoreCase);

    /// <summary>Tells whether a style name names a slanted face.</summary>
    /// <param name="style">The style name.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsItalic(string style) => style.Contains("Italic", StringComparison.OrdinalIgnoreCase) || style.Contains("Oblique", StringComparison.OrdinalIgnoreCase);

    /// <summary>Looks through the installed fonts for one that has a character, upright regular faces first.</summary>
    /// <param name="unicode">The character.</param>
    /// <returns>The face, or <see langword="null"/> when none of the fonts probed has it.</returns>
    private FontFace? Probe(int unicode)
    {
        var probes = 0;
        foreach (var face in catalog.Value.Faces)
        {
            if (IsBold(face.Style) || IsItalic(face.Style))
            {
                continue;
            }

            if (probes >= MaxProbes)
            {
                break;
            }

            probes++;
            if (Read(face) is { } program && program.GetGlyphByUnicode(unicode) > 0)
            {
                return face;
            }
        }

        return null;
    }

    /// <summary>Makes a face of an installed font.</summary>
    /// <param name="face">The font file and face index.</param>
    /// <returns>The face, or <see langword="null"/> when the file cannot be read as a font.</returns>
    private CatalogFontFace? Open(FontFace face) =>
        Read(face) is { } program ? new(face.Family, IsBold(face.Style) ? HeavyWeight : RegularWeight, IsItalic(face.Style), program) : null;

    /// <summary>Reads and parses a font file once.</summary>
    /// <param name="face">The font file and face index.</param>
    /// <returns>The parsed font, or <see langword="null"/> when the file cannot be read as a font.</returns>
    private TrueTypeProgram? Read(FontFace face)
    {
        lock (_gate)
        {
            if (_programs.TryGetValue(face, out var cached))
            {
                return cached;
            }
        }

        TrueTypeProgram? program = null;
        try
        {
            program = ScreenFontFace.Open(File.ReadAllBytes(face.Path), face.FaceIndex)?.Program;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A font file that went away or cannot be read is treated as not installed.
        }

        lock (_gate)
        {
            _programs[face] = program;
        }

        return program;
    }
}
