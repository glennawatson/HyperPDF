// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// A font as a content stream uses it: reads character codes from string bytes and gives each code's width, outline
/// and Unicode text. Fonts are parsed once per document and are safe to use from many threads; glyph outlines are cached.
/// </summary>
[DebuggerDisplay("PdfFont: {GetType().Name}")]
public abstract class PdfFont
{
    /// <summary>The longest Unicode text one code maps to.</summary>
    private const int UnicodeLimit = 8;

    /// <summary>The glyph-space units per text-space unit.</summary>
    private const float GlyphUnitCount = 1000;

    /// <summary>Half, used to centre a vertical glyph.</summary>
    private const float Half = 0.5F;

    /// <summary>The ascent used when a font gives none.</summary>
    private const float DefaultAscentValue = 0.8F;

    /// <summary>The descent used when a font gives none.</summary>
    private const float DefaultDescentValue = -0.2F;

    /// <summary>The vertical origin used when a vertical font gives none, in text space units.</summary>
    private const float DefaultVerticalOrigin = 0.88F;

    /// <summary>Initializes a new instance of the <see cref="PdfFont"/> class.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    protected PdfFont(PdfDictionary dictionary) => Dictionary = dictionary;

    /// <summary>Gets the longest Unicode text one code maps to, such as a ligature.</summary>
    public static int MaxUnicodeLength => UnicodeLimit;

    /// <summary>Gets the font dictionary.</summary>
    public PdfDictionary Dictionary { get; }

    /// <summary>Gets a value indicating whether the font writes top to bottom.</summary>
    public virtual bool IsVertical => false;

    /// <summary>Gets a value indicating whether the font's glyphs are content streams (Type 3).</summary>
    public virtual bool IsType3 => false;

    /// <summary>Gets a value indicating whether the font is bold, from its name, weight or flags.</summary>
    public virtual bool IsBold => false;

    /// <summary>Gets the ascent in text space units (font size 1).</summary>
    public virtual float Ascent => DefaultAscentValue;

    /// <summary>Gets the descent in text space units (font size 1), usually negative.</summary>
    public virtual float Descent => DefaultDescentValue;

    /// <summary>Gets the matrix from glyph space to text space; 1/1000 scale for every font type but Type 3.</summary>
    public virtual Matrix3x2 FontMatrix => Matrix3x2.CreateScale(1 / GlyphUnitCount);

    /// <summary>Reads the next character code from string bytes.</summary>
    /// <param name="bytes">The remaining string bytes.</param>
    /// <param name="code">The character code.</param>
    /// <returns>The bytes the code used; at least 1 while bytes remain.</returns>
    public abstract int ReadCode(ReadOnlySpan<byte> bytes, out int code);

    /// <summary>
    /// Gets a code's horizontal advance in text space units (font size 1). Type 3 fonts return the glyph width already
    /// multiplied by the font matrix.
    /// </summary>
    /// <param name="code">The character code.</param>
    /// <returns>The width.</returns>
    public abstract float GetWidth(int code);

    /// <summary>Gets a code's outline in glyph space (y up), cached; <see langword="null"/> for blank glyphs and Type 3 fonts.</summary>
    /// <param name="code">The character code.</param>
    /// <returns>The outline, owned by the font.</returns>
    public abstract PdfPath? GetOutline(int code);

    /// <summary>Writes a code's Unicode text.</summary>
    /// <param name="code">The character code.</param>
    /// <param name="destination">The buffer, at least <see cref="MaxUnicodeLength"/> characters.</param>
    /// <returns>The characters written; 0 when the code has no known text.</returns>
    public abstract int GetUnicode(int code, Span<char> destination);

    /// <summary>Determines whether word spacing applies to a code: the single-byte code 32.</summary>
    /// <param name="code">The character code.</param>
    /// <param name="length">The bytes the code used.</param>
    /// <returns><see langword="true"/> when word spacing applies.</returns>
    public virtual bool IsWordSpace(int code, int length) => code == ' ' && length == 1;

    /// <summary>
    /// Gets a code's vertical writing metrics in text space units (font size 1). Only vertical fonts are asked. The default
    /// advances down by one unit and centres the glyph horizontally.
    /// </summary>
    /// <param name="code">The character code.</param>
    /// <param name="advance">The vertical advance, usually negative.</param>
    /// <param name="originX">The x of the vertical origin relative to the horizontal origin.</param>
    /// <param name="originY">The y of the vertical origin relative to the horizontal origin.</param>
    public virtual void GetVerticalMetrics(int code, out float advance, out float originX, out float originY)
    {
        advance = -1;
        originX = GetWidth(code) * Half;
        originY = DefaultVerticalOrigin;
    }
}
