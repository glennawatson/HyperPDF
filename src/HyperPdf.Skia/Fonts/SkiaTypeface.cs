// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics.CodeAnalysis;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using SkiaSharp;

namespace HyperPdf.Skia.Fonts;

/// <summary>Platform typeface implementation wrapping an <see cref = "SKTypeface"/>.</summary>
internal sealed class SkiaTypeface : IPlatformTypeface, ITextShaperTypeface
{
    /// <summary>The skew angle for synthesized oblique text.</summary>
    private const float ObliqueSkew = -0.3F;

    /// <summary>Initializes a new instance of the <see cref = "SkiaTypeface"/> class.</summary>
    /// <param name = "typeface">The Skia typeface.</param>
    /// <param name = "fontSimulations">The font simulations.</param>
    /// <exception cref = "ArgumentNullException">Thrown when <c>typeface</c> is <see langword="null"/>.</exception>
    public SkiaTypeface(SKTypeface typeface, FontSimulations fontSimulations)
    {
        Typeface = typeface ?? throw new ArgumentNullException(nameof(typeface));
        FontSimulations = fontSimulations;
        Weight = (FontWeight)typeface.FontWeight;
        Style = typeface.FontStyle.Slant.ToAvalonia();
        Stretch = (FontStretch)typeface.FontWidth;
    }

    /// <summary>Gets the underlying Skia typeface.</summary>
    public SKTypeface Typeface { get; }

    /// <summary>Gets a value indicating whether the font manager keeps this typeface for later requests, so a caller's <see cref="Dispose"/> must leave it alone.</summary>
    public bool IsShared { get; init; }

    /// <summary>Gets the font simulations.</summary>
    public FontSimulations FontSimulations { get; }

    /// <inheritdoc/>
    public string FamilyName => Typeface.FamilyName;

    /// <inheritdoc/>
    public FontWeight Weight { get; }

    /// <inheritdoc/>
    public FontStyle Style { get; }

    /// <inheritdoc/>
    public FontStretch Stretch { get; }

    /// <inheritdoc/>
    public bool TryGetTable(OpenTypeTag tag, out ReadOnlyMemory<byte> table)
    {
        table = default;
        if (Typeface.TryGetTableData(tag, out var data))
        {
            table = data;
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public bool TryGetStream([NotNullWhen(true)] out Stream? stream)
    {
        try
        {
            using var asset = Typeface.OpenStream();
            var size = asset.Length;
            var buffer = new byte[size];
            _ = asset.Read(buffer, size);
            stream = new MemoryStream(buffer);
            return true;
        }
        catch
        {
            stream = null;
            return false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!IsShared)
        {
            Typeface.Dispose();
        }
    }

    /// <summary>Creates a modern <see cref = "SKFont"/> configured for this typeface and size.</summary>
    /// <param name = "size">The font size in pixels.</param>
    /// <returns>The configured Skia font.</returns>
    internal SKFont CreateSKFont(float size) => new(Typeface, size, skewX: (FontSimulations & FontSimulations.Oblique) != 0 ? ObliqueSkew : 0.0F)
    {
        LinearMetrics = true,
        Embolden = (FontSimulations & FontSimulations.Bold) != 0,
    };
}
