// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>What an inline image's filters break.</summary>
/// <param name="Lzw">Whether a filter is LZW, which PDF/A-1 forbids.</param>
/// <param name="Allowed">Whether every filter is one PDF/A-2 and later allow in inline images.</param>
internal readonly record struct PdfAInlineFilters(bool Lzw, bool Allowed)
{
    /// <summary>The filters PDF/A-2 and later allow in inline images, by full and abbreviated name.</summary>
    private static readonly byte[][] AllowedNames =
    [
        "ASCIIHexDecode"u8.ToArray(), "ASCII85Decode"u8.ToArray(), "FlateDecode"u8.ToArray(), "RunLengthDecode"u8.ToArray(),
        "CCITTFaxDecode"u8.ToArray(), "DCTDecode"u8.ToArray(), "AHx"u8.ToArray(), "A85"u8.ToArray(), "Fl"u8.ToArray(),
        "RL"u8.ToArray(), "CCF"u8.ToArray(), "DCT"u8.ToArray(),
    ];

    /// <summary>Gets the result for no filters.</summary>
    internal static PdfAInlineFilters None => new(false, true);

    /// <summary>Classifies one filter name.</summary>
    /// <param name="name">The name, full or abbreviated.</param>
    /// <returns>What it breaks.</returns>
    internal static PdfAInlineFilters Of(ReadOnlySpan<byte> name) =>
        new(name.SequenceEqual("LZWDecode"u8) || name.SequenceEqual("LZW"u8), IsAllowed(name));

    /// <summary>Combines this result with another filter's.</summary>
    /// <param name="other">The other filter's result.</param>
    /// <returns>The combined result.</returns>
    internal PdfAInlineFilters And(PdfAInlineFilters other) => new(Lzw || other.Lzw, Allowed && other.Allowed);

    /// <summary>Determines whether a filter is allowed in inline images.</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsAllowed(ReadOnlySpan<byte> name)
    {
        foreach (var allowed in AllowedNames)
        {
            if (name.SequenceEqual(allowed))
            {
                return true;
            }
        }

        return false;
    }
}
