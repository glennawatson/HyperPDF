// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using PdfViewerLite.Core.Text;
using PdfViewerLite.Core.Text.Layout;

namespace PdfViewerLite.HyperPdf;

/// <summary>
/// Lays text out in one of the built in fonts from their standard metrics. Each glyph is the character's WinAnsi code,
/// which is also the byte written; characters the fonts lack get code 0 and no width. The vertical metrics match the
/// PDFium engine's, so both lay text out alike.
/// </summary>
[DebuggerDisplay("StandardTextShaper: {Font}")]
internal sealed class StandardTextShaper : ITextShaper
{
    /// <summary>The number of built in fonts.</summary>
    internal const int FontCount = 12;

    /// <summary>Where the underline sits in all three families, as the PDFium engine places it.</summary>
    private const float Underline = -0.1F;

    /// <summary>The underline thickness in all three families.</summary>
    private const float UnderlineWidth = 0.05F;

    /// <summary>The index of the Times family.</summary>
    private const int TimesFamily = 1;

    /// <summary>The index of the Courier family.</summary>
    private const int CourierFamily = 2;

    /// <summary>Initializes a new instance of the <see cref="StandardTextShaper"/> class.</summary>
    /// <param name="font">The font.</param>
    private StandardTextShaper(AppearanceFont font) => Font = font;

    /// <inheritdoc/>
    public float Ascent => AppearanceFontMetrics.GetAscent(Font);

    /// <inheritdoc/>
    public float Descent => AppearanceFontMetrics.GetDescent(Font);

    /// <inheritdoc/>
    public float UnderlinePosition => Underline;

    /// <inheritdoc/>
    public float UnderlineThickness => UnderlineWidth;

    /// <summary>Gets the font.</summary>
    internal AppearanceFont Font { get; }

    /// <inheritdoc/>
    public bool Shape(ReadOnlySpan<char> text, List<ShapedGlyph> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        for (var i = 0; i < text.Length; i++)
        {
            var code = AppearanceFontMetrics.TryEncode(text[i], out var encoded) ? encoded : (byte)0;
            output.Add(new(code, i, AppearanceFontMetrics.GetAdvance(Font, code), 0, 0));
        }

        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Covers(int codePoint) => AppearanceFontMetrics.TryEncode(codePoint, out _);

    /// <summary>Gets the shaper of a built in family in a style.</summary>
    /// <param name="state">The engine's reusable font state.</param>
    /// <param name="family">The standard family; anything else is treated as Helvetica.</param>
    /// <param name="bold">Whether bold.</param>
    /// <param name="italic">Whether italic.</param>
    /// <returns>The shaper.</returns>
    internal static StandardTextShaper For(TextBoxFontState state, string family, bool bold, bool italic)
    {
        var font = AppearanceFontMetrics.FromStyle(FamilyIndex(family), bold, italic);
        ref var slot = ref state.StandardShapers[(int)font];
        return Volatile.Read(ref slot) ?? Interlocked.CompareExchange(ref slot, new(font), null) ?? Volatile.Read(ref slot)!;
    }

    /// <summary>Determines whether the built in fonts have every character of some text, line breaks aside.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> for Windows Latin text.</returns>
    internal static bool CoversText(string text)
    {
        foreach (var c in text)
        {
            if (c is not ('\r' or '\n') && !AppearanceFontMetrics.TryEncode(c, out _))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets a standard family's index: 0 for Helvetica, 1 for Times, 2 for Courier.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The index.</returns>
    private static int FamilyIndex(string family)
    {
        if (string.Equals(family, StandardFontFamilies.Serif, StringComparison.OrdinalIgnoreCase))
        {
            return TimesFamily;
        }

        return string.Equals(family, StandardFontFamilies.Mono, StringComparison.OrdinalIgnoreCase) ? CourierFamily : 0;
    }
}
