// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Render.Skia.Fonts;

/// <summary>Owns an unhinted Skia font and exposes glyph metrics and managed outlines.</summary>
internal sealed class SkiaFontFace : IPdfFontFace
{
    /// <summary>The size that expresses metrics in PDF glyph units.</summary>
    private const float GlyphUnits = 1000F;

    /// <summary>The smallest weight treated as bold.</summary>
    private const int BoldThreshold = 600;

    /// <summary>The synthetic rightward italic skew in Skia's downward coordinate space.</summary>
    private const float SyntheticSkew = -0.2F;

    /// <summary>The letters in a table tag.</summary>
    private const int TagLength = 4;

    /// <summary>The bits in one tag letter.</summary>
    private const int TagLetterBits = 8;

    /// <summary>Serializes native font access and disposal.</summary>
    private readonly Lock _gate = new();

    /// <summary>The owned native typeface.</summary>
    private readonly SKTypeface _typeface;

    /// <summary>The owned native font.</summary>
    private readonly SKFont _font;

    /// <summary>Whether native resources have been released.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="SkiaFontFace"/> class.</summary>
    /// <param name="typeface">The owned native typeface.</param>
    /// <param name="weight">The requested weight.</param>
    /// <param name="italic">Whether the requested face slants.</param>
    internal SkiaFontFace(SKTypeface typeface, int weight, bool italic)
    {
        _typeface = typeface;
        FamilyName = typeface.FamilyName ?? string.Empty;
        Weight = typeface.FontWeight;
        Italic = typeface.FontSlant != SKFontStyleSlant.Upright;
        _font = new(typeface, GlyphUnits)
        {
            Hinting = SKFontHinting.None,
            LinearMetrics = true,
            Subpixel = true,
            Embolden = weight >= BoldThreshold && Weight < BoldThreshold,
            SkewX = italic && !Italic ? SyntheticSkew : 0,
        };
        _ = _font.GetFontMetrics(out var metrics);
        Ascent = -metrics.Ascent;
        Descent = -metrics.Descent;
        GlyphCount = typeface.GlyphCount;
    }

    /// <inheritdoc/>
    public string FamilyName { get; }

    /// <inheritdoc/>
    public int Weight { get; }

    /// <inheritdoc/>
    public bool Italic { get; }

    /// <inheritdoc/>
    public int GlyphCount { get; }

    /// <inheritdoc/>
    public float Ascent { get; }

    /// <inheritdoc/>
    public float Descent { get; }

    /// <inheritdoc/>
    public int GetGlyph(int unicode)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return unicode <= 0 ? 0 : _font.GetGlyph(unicode);
        }
    }

    /// <inheritdoc/>
    public float GetAdvance(int glyph)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if ((uint)glyph >= (uint)GlyphCount)
            {
                return 0;
            }

            ReadOnlySpan<ushort> glyphs = [(ushort)glyph];
            Span<float> widths = stackalloc float[1];
            _font.GetGlyphWidths(glyphs, widths, []);
            return widths[0];
        }
    }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> GetTable(string tag)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return tag.Length == TagLength && _typeface.TryGetTableData(TagValue(tag), out var data) ? data : default;
        }
    }

    /// <inheritdoc/>
    public PdfPath? BuildOutline(int glyph)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if ((uint)glyph >= (uint)GlyphCount)
            {
                return null;
            }

            using var path = _font.GetGlyphPath((ushort)glyph);
            return path is null ? null : SkiaFontOutline.Convert(path);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _font.Dispose();
            _typeface.Dispose();
        }
    }

    /// <summary>Packs the four letters of a table tag into the number Skia names the table by.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The number, first letter highest.</returns>
    private static uint TagValue(string tag)
    {
        var value = 0U;
        foreach (var letter in tag)
        {
            value = (value << TagLetterBits) | (byte)letter;
        }

        return value;
    }
}
