// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Reads what a font's licence flags (OS/2 <c>fsType</c>) allow when the font goes into a PDF.</summary>
internal static class FontLicence
{
    /// <summary>The embedding level bits of <c>fsType</c>.</summary>
    private const int LevelMask = 0x000F;

    /// <summary>The restricted licence level: the font must not be embedded.</summary>
    private const int Restricted = 0x0002;

    /// <summary>The <c>fsType</c> bit forbidding subsetting.</summary>
    private const int NoSubsetting = 0x0100;

    /// <summary>The <c>fsType</c> bit allowing only bitmaps to be embedded.</summary>
    private const int BitmapOnly = 0x0200;

    /// <summary>Gets why a licence forbids embedding.</summary>
    /// <param name="flags">The <c>fsType</c> flags.</param>
    /// <returns>A plain reason, or <see langword="null"/> when embedding is allowed.</returns>
    internal static string? Refusal(int flags)
    {
        if ((flags & LevelMask) == Restricted)
        {
            return "The font's licence does not allow it to be embedded in documents (restricted licence embedding).";
        }

        return (flags & BitmapOnly) != 0 ? "The font's licence allows only bitmaps to be embedded, and a PDF needs the outlines." : null;
    }

    /// <summary>Determines whether the licence lets an embedded font keep only the glyphs used.</summary>
    /// <param name="flags">The <c>fsType</c> flags.</param>
    /// <returns><see langword="false"/> when the whole font has to be embedded.</returns>
    internal static bool CanSubset(int flags) => (flags & NoSubsetting) == 0;
}
