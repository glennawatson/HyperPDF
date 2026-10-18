// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace HyperPdfLibrary.Text;

/// <summary>Strips Unicode formatting controls in place before bidirectional interpretation.</summary>
internal static class TextFormattingControls
{
    /// <summary>The first non-ASCII code point.</summary>
    private const char NonAsciiStart = (char)0x80;

    /// <summary>The size of a UTF-16 surrogate pair.</summary>
    private const int SurrogatePairLength = 2;

    /// <summary>The mask for periodic cancellation checks.</summary>
    private const int CancellationMask = 1023;

    /// <summary>Removes formatting controls, including supplementary tag characters, from a line without a temporary buffer.</summary>
    /// <param name="line">The characters before reordering.</param>
    /// <param name="cancellationToken">Cancels filtering.</param>
    internal static void Strip(List<TextBuildChar> line, CancellationToken cancellationToken)
    {
        var chars = CollectionsMarshal.AsSpan(line);
        var destination = 0;
        for (var source = 0; source < chars.Length;)
        {
            if ((source & CancellationMask) <= 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var count = ScalarLength(chars, source, out var control);
            if (!control)
            {
                chars.Slice(source, count).CopyTo(chars.Slice(destination, count));
                destination += count;
            }

            source += count;
        }

        if (destination < chars.Length)
        {
            line.RemoveRange(destination, chars.Length - destination);
        }
    }

    /// <summary>Classifies one Unicode scalar, preserving invalid surrogates rather than throwing.</summary>
    /// <param name="chars">The characters.</param>
    /// <param name="index">The scalar's start.</param>
    /// <param name="control">Whether it is a formatting control.</param>
    /// <returns>The number of UTF-16 code units.</returns>
    private static int ScalarLength(ReadOnlySpan<TextBuildChar> chars, int index, out bool control)
    {
        control = false;
        var value = chars[index].Unicode;
        if (value < NonAsciiStart)
        {
            return 1;
        }

        if (Rune.TryCreate(value, out var rune))
        {
            control = Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format;
            return 1;
        }

        if (index + 1 < chars.Length && Rune.TryCreate(value, chars[index + 1].Unicode, out rune))
        {
            control = Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format;
            return SurrogatePairLength;
        }

        return 1;
    }
}
