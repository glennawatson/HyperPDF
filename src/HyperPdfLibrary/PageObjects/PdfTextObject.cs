// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>
/// One text-showing operator (<c>Tj</c>, <c>TJ</c>, <c>'</c> or <c>"</c>) with the text state it runs in. Glyphs can be
/// removed one by one; the regenerated content then moves the text that follows by the removed glyphs' widths, so
/// nothing else shifts.
/// </summary>
[DebuggerDisplay("PdfTextObject: {GlyphCount} glyphs, {RemovedCount} removed")]
public sealed class PdfTextObject : PdfPageObject
{
    /// <summary>The glyphs, in the order they are shown.</summary>
    private PdfTextGlyph[] _glyphs = [];

    /// <summary>Which glyphs are removed, or null while none is.</summary>
    private bool[]? _removed;

    /// <summary>Initializes a new instance of the <see cref="PdfTextObject"/> class.</summary>
    internal PdfTextObject()
    {
    }

    /// <inheritdoc/>
    public override PdfPageObjectKind Kind => PdfPageObjectKind.Text;

    /// <summary>Gets the font, or <see langword="null"/> when the resource is missing or cannot be loaded.</summary>
    public PdfFont? Font { get; internal init; }

    /// <summary>Gets the font's name in the resources' /Font.</summary>
    public PdfName FontName { get; internal init; }

    /// <summary>Gets the font size.</summary>
    public float FontSize { get; internal init; }

    /// <summary>Gets the text render mode, 0 to 7; 3 is invisible text such as an OCR layer.</summary>
    public int RenderMode { get; internal init; }

    /// <summary>Gets the character spacing (<c>Tc</c>).</summary>
    public float CharacterSpacing { get; internal init; }

    /// <summary>Gets the word spacing (<c>Tw</c>).</summary>
    public float WordSpacing { get; internal init; }

    /// <summary>Gets the horizontal scaling (<c>Tz</c>) as a ratio, 1 for 100 percent.</summary>
    public float HorizontalScaling { get; internal init; } = 1;

    /// <summary>Gets the text rise (<c>Ts</c>).</summary>
    public float Rise { get; internal init; }

    /// <summary>Gets the text matrix in force when the first glyph is shown.</summary>
    public Matrix3x2 TextMatrix { get; internal init; } = Matrix3x2.Identity;

    /// <summary>Gets the Unicode text of the glyphs as the font maps their codes, removed glyphs included.</summary>
    public string Text { get; internal init; } = string.Empty;

    /// <summary>Gets the glyphs, in the order they are shown.</summary>
    public ReadOnlySpan<PdfTextGlyph> Glyphs => _glyphs;

    /// <summary>Gets the number of glyphs.</summary>
    public int GlyphCount => _glyphs.Length;

    /// <summary>Gets the number of glyphs removed.</summary>
    public int RemovedCount { get; private set; }

    /// <summary>Gets a value indicating whether the font writes top to bottom.</summary>
    public bool IsVertical => Font?.IsVertical == true;

    /// <inheritdoc/>
    public override bool IsModified => base.IsModified || RemovedCount > 0;

    /// <inheritdoc/>
    internal override bool CanCrop => false;

    /// <summary>Gets a value indicating whether the text could not be read (no font), so only its original bytes can be written back.</summary>
    internal bool IsOpaque { get; init; }

    /// <summary>Gets the operator that showed the text.</summary>
    internal ContentOperator ShowOperator { get; init; }

    /// <summary>Gets the word spacing operand of the <c>"</c> operator.</summary>
    internal float SpacingOperandWord { get; init; }

    /// <summary>Gets the character spacing operand of the <c>"</c> operator.</summary>
    internal float SpacingOperandCharacter { get; init; }

    /// <summary>Gets the string bytes of every code, joined.</summary>
    internal byte[] CodeBytes { get; init; } = [];

    /// <summary>Gets the sum of the <c>TJ</c> numbers after the last glyph.</summary>
    internal float TrailingKerning { get; init; }

    /// <summary>Gets how far the whole show moved the text position along the writing direction, in text space units.</summary>
    internal float TotalAdvance { get; init; }

    /// <summary>Gets the text line matrix after the show.</summary>
    internal Matrix3x2 LineMatrixAfter { get; init; } = Matrix3x2.Identity;

    /// <summary>Gets how far the text position stands from the start of the text line after the show.</summary>
    internal Vector2 PositionAfter { get; init; }

    /// <summary>Gets the divisor that turns a text space distance into thousandths for a <c>TJ</c> number; 0 when the text cannot move.</summary>
    internal float AdjustmentScale => IsVertical ? FontSize : FontSize * HorizontalScaling;

    /// <summary>Gets the bytes of one glyph's character code.</summary>
    /// <param name="index">The glyph index.</param>
    /// <returns>The code's bytes as written in the string.</returns>
    public ReadOnlySpan<byte> GetCodeBytes(int index)
    {
        var glyph = _glyphs[index];
        return CodeBytes.AsSpan(glyph.ByteOffset, glyph.ByteLength);
    }

    /// <summary>Gets the Unicode text of one glyph.</summary>
    /// <param name="index">The glyph index.</param>
    /// <returns>The text, empty when the font gives none.</returns>
    public ReadOnlySpan<char> GetGlyphText(int index)
    {
        var glyph = _glyphs[index];
        return Text.AsSpan(glyph.TextStart, glyph.TextLength);
    }

    /// <summary>Determines whether a glyph is removed.</summary>
    /// <param name="index">The glyph index.</param>
    /// <returns><see langword="true"/> when the regenerated content leaves the glyph out.</returns>
    public bool IsGlyphRemoved(int index) => _removed is not null && _removed[index];

    /// <summary>Removes a glyph from the regenerated content.</summary>
    /// <param name="index">The glyph index.</param>
    /// <returns><see langword="true"/> when the glyph was still there.</returns>
    public bool RemoveGlyph(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)_glyphs.Length, nameof(index));
        _removed ??= new bool[_glyphs.Length];
        if (_removed[index])
        {
            return false;
        }

        _removed[index] = true;
        RemovedCount++;
        return true;
    }

    /// <summary>Removes every glyph whose box the area covers: its centre inside the area, or at least half of it.</summary>
    /// <param name="area">The area in user space.</param>
    /// <returns>The number of glyphs removed.</returns>
    public int RemoveGlyphsIn(PdfRectangle area)
    {
        var removed = 0;
        for (var i = 0; i < _glyphs.Length; i++)
        {
            if (!IsGlyphRemoved(i) && PageGeometry.Covers(area, _glyphs[i].Box) && RemoveGlyph(i))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Gets the text of the glyphs that are not removed.</summary>
    /// <returns>The text.</returns>
    public string GetRemainingText()
    {
        if (RemovedCount == 0)
        {
            return Text;
        }

        var builder = new System.Text.StringBuilder(Text.Length);
        for (var i = 0; i < _glyphs.Length; i++)
        {
            if (!IsGlyphRemoved(i))
            {
                _ = builder.Append(GetGlyphText(i));
            }
        }

        return builder.ToString();
    }

    /// <summary>Sets the glyphs while the object is built.</summary>
    /// <param name="glyphs">The glyphs.</param>
    internal void SetGlyphs(PdfTextGlyph[] glyphs) => _glyphs = glyphs;
}
