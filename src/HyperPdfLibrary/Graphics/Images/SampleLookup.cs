// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>Builds the per-component tables that fold an image's /Decode array into 8-bit samples, and shares the table of images that /Decode leaves unchanged.</summary>
internal static class SampleLookup
{
    /// <summary>The components the shared identity table covers.</summary>
    internal const int SharedComponents = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Gets the table that maps every byte of each of the first <see cref="SharedComponents"/> components to itself; callers must not change it.</summary>
    internal static byte[] Identity { get; } = CreateIdentity();

    /// <summary>Determines whether 8-bit samples map to themselves, without building a table.</summary>
    /// <param name="decode">The image's decode ranges.</param>
    /// <param name="target">The colour space's 8-bit decode ranges.</param>
    /// <param name="components">The components per pixel.</param>
    /// <returns><see langword="true"/> when every sample of every component maps to itself.</returns>
    internal static bool IsIdentity(float[] decode, float[] target, int components)
    {
        for (var c = 0; c < components; c++)
        {
            for (var raw = 0; raw < PixelConverter.LookupSize; raw++)
            {
                if (MapSample(decode, target, c, raw, PixelConverter.LookupSize) != raw)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Builds the per-component tables.</summary>
    /// <param name="lookup">Receives the tables, 256 entries per component.</param>
    /// <param name="bits">The bits per sample.</param>
    /// <param name="components">The components per pixel.</param>
    /// <param name="decode">The image's decode ranges.</param>
    /// <param name="target">The colour space's 8-bit decode ranges.</param>
    /// <returns><see langword="true"/> when every table maps each byte to itself.</returns>
    internal static bool Build(byte[] lookup, int bits, int components, float[] decode, float[] target)
    {
        var levels = bits >= ByteBits ? PixelConverter.LookupSize : 1 << bits;
        var identity = bits == ByteBits;
        for (var c = 0; c < components; c++)
        {
            for (var raw = 0; raw < levels; raw++)
            {
                var mapped = MapSample(decode, target, c, raw, levels);
                lookup[(c * PixelConverter.LookupSize) + raw] = mapped;
                identity &= mapped == raw;
            }
        }

        return identity;
    }

    /// <summary>Makes the table that maps every byte to itself, once for each shared component.</summary>
    /// <returns>The table.</returns>
    private static byte[] CreateIdentity()
    {
        var table = new byte[SharedComponents * PixelConverter.LookupSize];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (byte)(i % PixelConverter.LookupSize);
        }

        return table;
    }

    /// <summary>Maps one raw sample through the image's /Decode range into the colour space's 8-bit range.</summary>
    /// <param name="decode">The image's decode ranges.</param>
    /// <param name="target">The colour space's 8-bit decode ranges.</param>
    /// <param name="component">The component.</param>
    /// <param name="raw">The raw sample value.</param>
    /// <param name="levels">The number of raw values.</param>
    /// <returns>The 8-bit sample.</returns>
    private static byte MapSample(float[] decode, float[] target, int component, int raw, int levels)
    {
        var low = decode[PdfColorSpace.PairSize * component];
        var high = decode[(PdfColorSpace.PairSize * component) + 1];
        var targetLow = target[PdfColorSpace.PairSize * component];
        var targetSpan = target[(PdfColorSpace.PairSize * component) + 1] - targetLow;
        var value = low + (raw * (high - low) / (levels - 1));
        return targetSpan is 0 ? (byte)0 : PixelConverter.ToByte((value - targetLow) / targetSpan);
    }
}
