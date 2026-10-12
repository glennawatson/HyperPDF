// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Replaces one non-embedded simple font with an embedded subset of the open font that has its metrics. The new font is a
/// symbolic TrueType font whose one cmap subtable maps each code straight to its glyph, so the document's encoding and
/// content stay as they are (ISO 19005-2 §6.2.11.6), with widths from the original that the program matches within one
/// unit (§6.2.11.5) and a ToUnicode map so the text can be read (§6.2.11.7.2).
/// </summary>
internal static class PdfAConversionSubstitute
{
    /// <summary>Embeds a substitute for a font.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="id">The font object.</param>
    /// <param name="font">The font dictionary.</param>
    /// <param name="used">The codes the font shows visibly, or <see langword="null"/> when it is not seen in any content.</param>
    /// <param name="usageUnknown">Whether the content could not be read, so every code may be used.</param>
    /// <returns><see langword="null"/> when embedded, else the end of a sentence saying why not.</returns>
    internal static string? Embed(PdfAConversionSession session, PdfObjectId id, PdfDictionary font, HashSet<int>? used, bool usageUnknown)
    {
        var store = session.Store;
        var standard = PdfAFontEmbedding.Substitutable(store, font);
        if (standard == StandardFont.None)
        {
            return "no matching font is available to embed.";
        }

        var face = PdfAFontEmbedding.FaceOf(standard);
        var faceName = BundledFaces.ResourceName(face.Family, face.Bold, face.Italic);
        if (ReadFace(faceName) is not { } bytes || !TrueTypeProgram.TryParse(bytes, out var program))
        {
            return "the matching open font could not be downloaded.";
        }

        var plan = PdfAConversionGlyphPlan.Build(font, standard, program, used, usageUnknown);
        if (plan.Problem is { } problem)
        {
            return problem;
        }

        if (plan.Glyphs.Count == 0)
        {
            return null;
        }

        var glyphs = new HashSet<int>(plan.Glyphs.Values) { 0 };
        if (TrueTypeSubsetter.SubsetWithCmap(bytes, glyphs, PdfAConversionCmap.Write(plan.Glyphs)) is not { } subset)
        {
            return "the matching open font could not be prepared.";
        }

        var name = PdfAConversionFontWriter.SubsetName(faceName, plan.Glyphs);
        PdfAConversionFontWriter.Write(store, id, new(name, face, standard, program, subset, plan));
        return null;
    }

    /// <summary>Reads a bundled face's bytes from the font cache.</summary>
    /// <param name="name">The face's resource name.</param>
    /// <returns>The bytes, or <see langword="null"/> when the face is not on this computer.</returns>
    private static byte[]? ReadFace(string name)
    {
        using var stream = FontDataResources.Open("Faces", $"{name}.ttf");
        if (stream is null)
        {
            return null;
        }

        var data = new byte[(int)stream.Length];
        stream.ReadExactly(data);
        return data;
    }
}
