// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>Works out the font, size, shift and measurements of an atom from its formatting.</summary>
internal static class AtomMetrics
{
    /// <summary>The size of raised and lowered text as a fraction of its normal size.</summary>
    private const float ScriptScale = 0.65F;

    /// <summary>The size of a small capital as a fraction of a full capital.</summary>
    private const float SmallCapScale = 0.8F;

    /// <summary>How far superscript text is raised, as a fraction of the text's normal size.</summary>
    private const float SuperscriptRise = 0.36F;

    /// <summary>How far subscript text is lowered, as a fraction of the text's normal size.</summary>
    private const float SubscriptDrop = 0.14F;

    /// <summary>Fills in an atom's font, size, shift, width, ascent and descent.</summary>
    /// <param name="atom">The atom, whose format and text are set.</param>
    /// <exception cref="InvalidOperationException">The atom's format has no typeface.</exception>
    internal static void Measure(Atom atom)
    {
        var format = atom.Format;
        var family = format.Family ?? throw new InvalidOperationException("Text has no typeface.");
        var font = family.Select(format.Bold, format.Italic);
        var size = format.Size;
        var shift = 0F;
        if (format.Script != PdfScript.Normal)
        {
            shift = format.Script == PdfScript.Superscript ? size * SuperscriptRise : -(size * SubscriptDrop);
            size *= ScriptScale;
        }

        if (atom.IsSmallCapital)
        {
            size *= SmallCapScale;
        }

        atom.Font = font;
        atom.Size = size;
        atom.Shift = shift;
        atom.Width = atom.Kind is AtomKind.Word or AtomKind.Space or AtomKind.Note ? font.Measure(atom.Text, size) : 0F;
        atom.Ascent = MathF.Max(0F, font.GetAscent(size) + shift);
        atom.Descent = MathF.Max(0F, MathF.Abs(font.GetDescent(size)) - shift);
    }
}
