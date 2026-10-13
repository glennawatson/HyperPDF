// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Four-character OpenType tags (OpenType, "Data types": Tag) as numbers.</summary>
internal static class OtTag
{
    /// <summary>The characters in a tag.</summary>
    private const int TagLength = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The byte that pads a short tag.</summary>
    private const char Pad = ' ';

    /// <summary>The highest character a tag may hold.</summary>
    private const char LastTagCharacter = '~';

    /// <summary>Makes a tag from up to four ASCII characters; a short tag is padded with spaces.</summary>
    /// <param name="text">The characters.</param>
    /// <returns>The tag.</returns>
    internal static uint Of(ReadOnlySpan<char> text)
    {
        uint tag = 0;
        for (var i = 0; i < TagLength; i++)
        {
            var c = i < text.Length ? text[i] : Pad;
            tag = (tag << ByteBits) | (c <= LastTagCharacter ? c : Pad);
        }

        return tag;
    }
}
