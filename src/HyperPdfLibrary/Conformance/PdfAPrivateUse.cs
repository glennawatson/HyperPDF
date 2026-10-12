// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdfLibrary.Conformance;

/// <summary>Finds characters of the Unicode private use areas (U+E000 to U+F8FF and planes 15 and 16).</summary>
internal static class PdfAPrivateUse
{
    /// <summary>The first character of the basic private use area.</summary>
    private const int BasicStart = 0xE000;

    /// <summary>The last character of the basic private use area.</summary>
    private const int BasicEnd = 0xF8FF;

    /// <summary>The first character of supplementary private use area A.</summary>
    private const int PlaneFifteenStart = 0xF0000;

    /// <summary>The last character of supplementary private use area A.</summary>
    private const int PlaneFifteenEnd = 0xFFFFD;

    /// <summary>The first character of supplementary private use area B.</summary>
    private const int PlaneSixteenStart = 0x100000;

    /// <summary>The last character of supplementary private use area B.</summary>
    private const int PlaneSixteenEnd = 0x10FFFD;

    /// <summary>Determines whether text holds a private use character.</summary>
    /// <param name="text">The text, or null.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool Contains(string? text)
    {
        foreach (var rune in (text ?? string.Empty).EnumerateRunes())
        {
            if (IsPrivateUse(rune.Value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a code point is a private use character.</summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool IsPrivateUse(int codePoint) =>
        codePoint is (>= BasicStart and <= BasicEnd) or (>= PlaneFifteenStart and <= PlaneFifteenEnd) or (>= PlaneSixteenStart and <= PlaneSixteenEnd);
}
