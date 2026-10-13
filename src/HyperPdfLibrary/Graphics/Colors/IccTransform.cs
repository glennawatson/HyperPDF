// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// Converts the colours of an ICC profile to sRGB in managed code. Instances are immutable and safe to use from many
/// threads; conversion does not allocate.
/// </summary>
internal abstract class IccTransform
{
    /// <summary>Initializes a new instance of the <see cref="IccTransform"/> class.</summary>
    private protected IccTransform()
    {
    }

    /// <summary>Gets the number of colour components the profile takes.</summary>
    internal abstract int Components { get; }

    /// <summary>Gets a value indicating whether the profile behaves as sRGB, so device RGB can stand in for it.</summary>
    internal virtual bool IsSrgb => false;

    /// <summary>Gets the component ranges as min/max pairs when the profile's colour space needs other than 0 to 1, or <see langword="null"/>.</summary>
    internal virtual float[]? DefaultRange => null;

    /// <summary>Gets the transform for a rendering intent and black point choice.</summary>
    /// <param name="rendering">The rendering.</param>
    /// <returns>The transform; this one when the profile converts the same way for that rendering.</returns>
    internal virtual IccTransform ForRendering(IccRendering rendering) => this;

    /// <summary>Converts a colour to sRGB.</summary>
    /// <param name="components">The components, each from 0 to 1.</param>
    /// <param name="rgb">Receives red, green and blue from 0 to 1.</param>
    internal abstract void ToRgb(ReadOnlySpan<float> components, Span<float> rgb);

    /// <summary>Converts a row of 8-bit samples, each the component scaled to 0..255, to opaque BGRA.</summary>
    /// <param name="samples">The samples.</param>
    /// <param name="bgra">Receives four bytes per pixel.</param>
    /// <param name="pixelCount">The number of pixels.</param>
    internal abstract void ConvertRow(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount);
}
