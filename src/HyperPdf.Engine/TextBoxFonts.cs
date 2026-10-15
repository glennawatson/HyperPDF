// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Text;
using HyperPdf.Core.Text.Fonts;

namespace HyperPdf.Engine;

/// <summary>
/// Chooses the font a text box is written in: the chosen installed family when it has
/// every character; else the closest built in family when that has every character; else the first installed font
/// that has them all; else whatever was found, showing missing characters as boxes.
/// </summary>
internal static class TextBoxFonts
{
    /// <summary>The largest font file tried as a fallback, so huge collections are not read just to check them.</summary>
    private const long MaxFileBytes = 64L * 1024 * 1024;

    /// <summary>The bits of a code point's block.</summary>
    private const int BlockShift = 7;

    /// <summary>The weight from which a face counts as bold.</summary>
    private const int BoldWeight = 600;

    /// <summary>The rank added for the wrong weight, which outweighs the wrong slant.</summary>
    private const int WeightMismatch = 2;

    /// <summary>Chooses the font to write text in.</summary>
    /// <param name="state">The viewer engine's reusable font state.</param>
    /// <param name="catalog">The installed fonts.</param>
    /// <param name="format">The format.</param>
    /// <param name="text">The text.</param>
    /// <returns>The font, or <see langword="null"/> when none can be used.</returns>
    internal static TextBoxFont? Resolve(TextBoxFontState state, FontCatalog catalog, TextFormat format, string text)
    {
        var match = StandardFontFamilies.IsStandard(format.FontFamily) ? null : catalog.Find(format.FontFamily, format.IsBold, format.IsItalic);
        var program = match is null ? null : state.Load(match.Face);
        if (program is not null && Covers(program, text))
        {
            return Installed(state, program, match!.SynthesizeBold, match.SynthesizeItalic);
        }

        var standard = ClosestStandard(state, catalog, format, match);
        if (StandardTextShaper.CoversText(text))
        {
            return new(standard, null, standard, false, false);
        }

        if (FindFallback(state, catalog, text, format) is { } fallback)
        {
            return fallback;
        }

        return program is not null ? Installed(state, program, match!.SynthesizeBold, match.SynthesizeItalic) : new(standard, null, standard, false, false);
    }

    /// <summary>Determines whether a font has every character of some text, white space aside.</summary>
    /// <param name="program">The font.</param>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it has them all.</returns>
    internal static bool Covers(FontProgram program, string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (!Rune.IsWhiteSpace(rune) && !Rune.IsControl(rune) && program.GlyphFor(rune.Value) == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the built in font closest to a format's family, judged by the installed face when there is one.</summary>
    /// <param name="state">The viewer engine's reusable font state.</param>
    /// <param name="catalog">The installed fonts.</param>
    /// <param name="format">The format.</param>
    /// <param name="match">The installed face found, or <see langword="null"/>.</param>
    /// <returns>The built in font's shaper.</returns>
    private static StandardTextShaper ClosestStandard(TextBoxFontState state, FontCatalog catalog, TextFormat format, FontMatch? match)
    {
        var face = match?.Face ?? catalog.PreviewFace(format.FontFamily);
        var family = face is null ? StandardFontFamilies.Closest(format.FontFamily, false, false) : StandardFontFamilies.Closest(format.FontFamily, face.IsSerif, face.IsMonospace);
        return StandardTextShaper.For(state, family, format.IsBold, format.IsItalic);
    }

    /// <summary>Writes text in an installed font, embedded.</summary>
    /// <param name="state">The viewer engine's reusable font state.</param>
    /// <param name="program">The font.</param>
    /// <param name="bold">Whether bold is drawn in.</param>
    /// <param name="italic">Whether italic is drawn in.</param>
    /// <returns>The font to write with.</returns>
    private static TextBoxFont Installed(TextBoxFontState state, FontProgram program, bool bold, bool italic) =>
        new(state.Shapers.GetOrAdd(program, static font => new(font)), program, null, bold, italic);

    /// <summary>Finds an installed font with every character, remembering what was found for the text's script.</summary>
    /// <param name="state">The viewer engine's reusable font state.</param>
    /// <param name="catalog">The installed fonts.</param>
    /// <param name="text">The text.</param>
    /// <param name="format">The format, for the weight and slant wanted.</param>
    /// <returns>The font, or <see langword="null"/> when no installed font has every character.</returns>
    private static TextBoxFont? FindFallback(TextBoxFontState state, FontCatalog catalog, string text, TextFormat format)
    {
        var found = state.Found.GetValue(catalog, static _ => new());
        var key = new TextBoxFontState.FallbackKey(FirstUncommonBlock(text), format.IsBold, format.IsItalic);
        if (found.TryGetValue(key, out var cached) && FromFace(state, cached, text, format) is { } reused)
        {
            return reused;
        }

        if (Search(state, catalog, text, format) is not { } candidate)
        {
            return null;
        }

        var face = candidate.Face;
        var program = state.Remember(candidate);
        found[key] = face;
        return Installed(state, program, format.IsBold && face.Weight < BoldWeight, format.IsItalic && !face.IsItalic);
    }

    /// <summary>Uses an installed face when it covers all the requested text.</summary>
    /// <param name="state">The viewer engine's reusable font state.</param>
    /// <param name="face">The candidate face.</param>
    /// <param name="text">The text to show.</param>
    /// <param name="format">The requested style.</param>
    /// <returns>The font, or <see langword="null"/> when the face cannot show the text.</returns>
    private static TextBoxFont? FromFace(TextBoxFontState state, FontFace face, string text, TextFormat format) =>
        state.Load(face) is { } program && Covers(program, text)
            ? Installed(state, program, format.IsBold && face.Weight < BoldWeight, format.IsItalic && !face.IsItalic)
            : null;

    /// <summary>Gets the block of the first character outside Latin-1, which decides which fonts can show the text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The block number.</returns>
    private static int FirstUncommonBlock(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value > byte.MaxValue)
            {
                return rune.Value >> BlockShift;
            }
        }

        return 0;
    }

    /// <summary>Tries the installed faces, those in the wanted style first.</summary>
    /// <param name="state">The engine's retained font programs.</param>
    /// <param name="catalog">The installed fonts.</param>
    /// <param name="text">The text.</param>
    /// <param name="format">The format.</param>
    /// <returns>The first face with every character, or <see langword="null"/>.</returns>
    private static FontProgram? Search(TextBoxFontState state, FontCatalog catalog, string text, TextFormat format)
    {
        var faces = new List<FontFace>(catalog.Faces);
        faces.Sort((a, b) => Rank(a, format).CompareTo(Rank(b, format)));
        foreach (var face in faces)
        {
            if (ReadCovering(state, face, text) is { } program)
            {
                return program;
            }
        }

        return null;
    }

    /// <summary>Ranks a face for a format; lower is tried first.</summary>
    /// <param name="face">The face.</param>
    /// <param name="format">The format.</param>
    /// <returns>The rank.</returns>
    private static int Rank(FontFace face, TextFormat format) =>
        (face.IsBold == format.IsBold ? 0 : WeightMismatch) + (face.IsItalic == format.IsItalic ? 0 : 1);

    /// <summary>Reads a face, without retaining an unselected one, to see whether it has every character.</summary>
    /// <param name="state">The engine's retained font programs.</param>
    /// <param name="face">The face.</param>
    /// <param name="text">The text.</param>
    /// <returns>The program when it has them all, or null.</returns>
    private static FontProgram? ReadCovering(TextBoxFontState state, FontFace face, string text)
    {
        if (state.TryGetLoaded(face, out var loaded) && loaded is { } retained)
        {
            return Covers(retained, text) ? retained : null;
        }

        try
        {
            var file = new FileInfo(face.Path);
            if (!file.Exists || file.Length > MaxFileBytes)
            {
                return null;
            }

            var program = FontProgram.FromBytes(face, File.ReadAllBytes(face.Path));
            return program is not null && Covers(program, text) ? program : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
