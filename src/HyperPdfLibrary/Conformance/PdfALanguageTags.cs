// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks language identifiers (ISO 19005-1, 6.8.4; ISO 19005-2, 6.7.4): empty, or subtags of one to eight characters
/// joined by hyphens, letters only in PDF/A-1 (RFC 1766) and letters or digits after the first in PDF/A-2 and later
/// (RFC 3066).
/// </summary>
internal static class PdfALanguageTags
{
    /// <summary>The longest subtag.</summary>
    private const int MaxSubtag = 8;

    /// <summary>Records the rules a /Lang value breaks.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="value">The string's bytes.</param>
    /// <param name="where">Where the value is.</param>
    internal static void Check(PdfAValidationContext context, ReadOnlySpan<byte> value, string where)
    {
        var tag = PdfText.Decode(value);
        context.Require(IsValid(tag, false), PdfARule.LanguageTagPdfA1, where);
        context.Require(IsValid(tag, true), PdfARule.LanguageTagPdfA2, where);
    }

    /// <summary>Determines whether a language identifier is well formed.</summary>
    /// <param name="tag">The identifier.</param>
    /// <param name="digits">Whether subtags after the first may hold digits.</param>
    /// <returns><see langword="true"/> when it is empty or well formed.</returns>
    internal static bool IsValid(ReadOnlySpan<char> tag, bool digits)
    {
        if (tag.IsEmpty)
        {
            return true;
        }

        var first = true;
        foreach (var range in tag.Split('-'))
        {
            var subtag = tag[range];
            if (subtag.IsEmpty || subtag.Length > MaxSubtag || !IsSubtag(subtag, digits && !first))
            {
                return false;
            }

            first = false;
        }

        return true;
    }

    /// <summary>Determines whether a subtag holds only ASCII letters, and digits when allowed.</summary>
    /// <param name="subtag">The subtag.</param>
    /// <param name="digits">Whether digits are allowed.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsSubtag(ReadOnlySpan<char> subtag, bool digits)
    {
        foreach (var c in subtag)
        {
            if (!char.IsAsciiLetter(c) && !(digits && char.IsAsciiDigit(c)))
            {
                return false;
            }
        }

        return true;
    }
}
