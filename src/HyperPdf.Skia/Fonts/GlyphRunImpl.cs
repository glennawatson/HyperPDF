// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Buffers;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using SkiaSharp;

namespace HyperPdf.Skia.Fonts;

/// <summary>Platform glyph run implementation using modern SkiaSharp fonts and text blobs.</summary>
internal sealed class GlyphRunImpl : IGlyphRunImpl
{
    /// <summary>The typeface implementation.</summary>
    private readonly SkiaTypeface _typeface;

    /// <summary>The shaped glyph indices.</summary>
    private readonly ushort[] _glyphIndices;

    /// <summary>The computed 2D glyph positions.</summary>
    private readonly SKPoint[] _glyphPositions;

    /// <summary>Cached primary text blob.</summary>
    private SKTextBlob? _cachedBlob;

    /// <summary>The text options used for the cached blob.</summary>
    private TextOptions _cachedOptions;

    /// <summary>Initializes a new instance of the <see cref = "GlyphRunImpl"/> class.</summary>
    /// <param name = "glyphTypeface">The glyph typeface.</param>
    /// <param name = "fontRenderingEmSize">The font rendering size.</param>
    /// <param name = "glyphInfos">The shaped glyph infos.</param>
    /// <param name = "baselineOrigin">The baseline origin point.</param>
    public GlyphRunImpl(GlyphTypeface glyphTypeface, double fontRenderingEmSize, IReadOnlyList<GlyphInfo> glyphInfos, Point baselineOrigin)
    {
        ArgumentNullException.ThrowIfNull(glyphTypeface);
        ArgumentNullException.ThrowIfNull(glyphInfos);
        _typeface = (SkiaTypeface)glyphTypeface.PlatformTypeface;
        FontRenderingEmSize = fontRenderingEmSize;
        var count = glyphInfos.Count;
        _glyphIndices = new ushort[count];
        _glyphPositions = new SKPoint[count];
        if (glyphInfos is ShapedBuffer shaped)
        {
            for (var i = 0; i < count; i++)
            {
                _glyphIndices[i] = shaped[i].GlyphIndex;
            }
        }
        else
        {
            for (var i = 0; i < count; i++)
            {
                _glyphIndices[i] = glyphInfos[i].GlyphIndex;
            }
        }

        var defaultOptions = default(TextOptions) with
        {
            TextRenderingMode = TextRenderingMode.SubpixelAntialias,
            TextHintingMode = TextHintingMode.Strong,
            BaselinePixelAlignment = BaselinePixelAlignment.Unaligned,
        };
        using var font = CreateFont(defaultOptions);
        var glyphBounds = ArrayPool<SKRect>.Shared.Rent(count);
        try
        {
            font.GetGlyphWidths(_glyphIndices, null, glyphBounds.AsSpan(0, count));
            var currentX = 0.0;
            var runBounds = default(Rect);
            for (var i = 0; i < count; i++)
            {
                var glyphInfo = glyphInfos[i];
                var offset = glyphInfo.GlyphOffset;
                var glyphRectangle = glyphBounds[i];
                _glyphPositions[i] = new((float)(currentX + offset.X), (float)offset.Y);
                runBounds = runBounds.Union(new(currentX + glyphRectangle.Left, glyphRectangle.Top, glyphRectangle.Width, glyphRectangle.Height));
                currentX += glyphInfo.GlyphAdvance;
            }

            BaselineOrigin = baselineOrigin;
            Bounds = runBounds.Translate(new(baselineOrigin.X, baselineOrigin.Y));
        }
        finally
        {
            ArrayPool<SKRect>.Shared.Return(glyphBounds);
        }
    }

    /// <inheritdoc/>
    public double FontRenderingEmSize { get; }

    /// <inheritdoc/>
    public Point BaselineOrigin { get; }

    /// <inheritdoc/>
    public Rect Bounds { get; }

    /// <inheritdoc/>
    public IReadOnlyList<float> GetIntersections(float lowerLimit, float upperLimit)
    {
        var textBlob = GetTextBlob(default, default);
        return textBlob.GetIntercepts(lowerLimit, upperLimit);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cachedBlob?.Dispose();
        _cachedBlob = null;
    }

    /// <summary>Gets the Skia text blob for the specified rendering options.</summary>
    /// <param name = "textOptions">The text rendering options.</param>
    /// <param name = "renderOptions">The general render options.</param>
    /// <returns>The pre-built or newly built text blob.</returns>
    /// <exception cref = "InvalidOperationException">Thrown when <c>builder.Build()</c> is <see langword="null"/>.</exception>
    internal SKTextBlob GetTextBlob(TextOptions textOptions, RenderOptions renderOptions)
    {
        if (textOptions.TextRenderingMode == TextRenderingMode.Unspecified)
        {
            textOptions = textOptions with
            {
                TextRenderingMode = renderOptions.EdgeMode == EdgeMode.Aliased ? TextRenderingMode.Alias : TextRenderingMode.SubpixelAntialias,
            };
        }

        if (_cachedBlob is not null && _cachedOptions == textOptions)
        {
            return _cachedBlob;
        }

        _cachedBlob?.Dispose();
        _cachedOptions = textOptions;
        using var font = CreateFont(textOptions);
        using var builder = new SKTextBlobBuilder();
        var runBuffer = builder.AllocatePositionedRun(font, _glyphIndices.Length);
        runBuffer.SetPositions(_glyphPositions);
        runBuffer.SetGlyphs(_glyphIndices);
        _cachedBlob = builder.Build() ?? throw new InvalidOperationException("Failed to build SKTextBlob.");
        return _cachedBlob;
    }

    /// <summary>Creates a modern Skia font with hinting and edging applied.</summary>
    /// <param name = "textOptions">The text options.</param>
    /// <returns>The configured Skia font.</returns>
    private SKFont CreateFont(TextOptions textOptions)
    {
        var edging = textOptions.TextRenderingMode switch
        {
            TextRenderingMode.Alias => SKFontEdging.Alias,
            TextRenderingMode.Antialias => SKFontEdging.Antialias,
            _ => SKFontEdging.SubpixelAntialias,
        };
        var hinting = textOptions.TextHintingMode switch
        {
            TextHintingMode.None => SKFontHinting.None,
            TextHintingMode.Light => SKFontHinting.Slight,
            _ => SKFontHinting.Full,
        };
        var font = _typeface.CreateSKFont((float)FontRenderingEmSize);
        font.ForceAutoHinting = textOptions.TextHintingMode == TextHintingMode.Light;
        font.Hinting = hinting;
        font.Subpixel = edging != SKFontEdging.Alias;
        font.Edging = edging;
        font.BaselineSnap = textOptions.BaselinePixelAlignment != BaselinePixelAlignment.Unaligned;
        return font;
    }
}
