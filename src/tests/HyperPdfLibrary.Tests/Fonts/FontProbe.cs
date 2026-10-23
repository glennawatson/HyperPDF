// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Reads font answers in forms that are easy to assert.</summary>
internal static class FontProbe
{
    /// <summary>Gets a code's Unicode text.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <returns>The text.</returns>
    internal static string Text(PdfFont font, int code)
    {
        var buffer = new char[PdfFont.MaxUnicodeLength];
        return new(buffer, 0, font.GetUnicode(code, buffer));
    }

    /// <summary>Gets a code's outline bounds in glyph space.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <returns>The bounds, or an empty rectangle when the code has no outline.</returns>
    internal static PdfRect Bounds(PdfFont font, int code) => font.GetOutline(code)?.Bounds ?? PdfRect.Empty;

    /// <summary>Reads every code of a byte string.</summary>
    /// <param name="font">The font.</param>
    /// <param name="bytes">The string bytes.</param>
    /// <returns>The codes and their lengths.</returns>
    internal static List<CodeRead> ReadCodes(PdfFont font, byte[] bytes)
    {
        var codes = new List<CodeRead>();
        ReadOnlySpan<byte> rest = bytes;
        while (!rest.IsEmpty)
        {
            var used = font.ReadCode(rest, out var code);
            codes.Add(new(code, used));
            rest = rest[Math.Max(used, 1)..];
        }

        return codes;
    }
}
