// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Annotations;

/// <summary>
/// The divisions <see cref="SignaturePixels"/> makes, worked out once for every alpha and channel value, so each pixel is
/// a lookup rather than a division. Each table is indexed by <c>alpha &lt;&lt; 8 | value</c>. The tables are made the first
/// time a signature picture is prepared.
/// </summary>
internal static class SignaturePixelTables
{
    /// <summary>The values a channel takes.</summary>
    private const int Values = 256;

    /// <summary>The bits the alpha is shifted by in an index.</summary>
    private const int AlphaShift = 8;

    /// <summary>The largest channel value.</summary>
    private const int Opaque = 255;

    /// <summary>Gets the straight value of a premultiplied channel, rounded to nearest so a round trip keeps colours. Made once and never changed.</summary>
    internal static byte[] Unpremultiplied { get; } = Build(true);

    /// <summary>Gets a channel's distance above the paper, scaled by the ink's alpha. Made once and never changed.</summary>
    internal static byte[] PaperRemoved { get; } = Build(false);

    /// <summary>Gets the index of an alpha and a channel value.</summary>
    /// <param name="alpha">The alpha.</param>
    /// <param name="value">The channel value.</param>
    /// <returns>The index.</returns>
    internal static int IndexOf(int alpha, int value) => (alpha << AlphaShift) | value;

    /// <summary>Builds a table; alpha zero is never looked up.</summary>
    /// <param name="rounded">Whether to round to nearest, for unpremultiplying, rather than down, for removing paper.</param>
    /// <returns>The table.</returns>
    private static byte[] Build(bool rounded)
    {
        var table = new byte[Values * Values];
        for (var alpha = 1; alpha < Values; alpha++)
        {
            var half = rounded ? alpha >> 1 : 0;
            for (var value = 0; value < Values; value++)
            {
                table[IndexOf(alpha, value)] = (byte)Math.Min(Opaque, ((value * Opaque) + half) / alpha);
            }
        }

        return table;
    }
}
