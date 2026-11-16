// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>
/// Text stamped on pages: a watermark, a header or footer, page numbers or Bates numbers. The words may hold <c>{page}</c> for the page's own
/// number, <c>{pages}</c> for the number of pages, <c>{number}</c> for a running number that counts the stamped pages from
/// <see cref="FirstNumber"/>, and <c>{date}</c> for today's date.
/// </summary>
/// <param name="Text">The words, with any of the placeholders.</param>
/// <param name="Family">The look of the letters.</param>
/// <param name="Bold">Whether the letters are bold.</param>
/// <param name="Italic">Whether the letters are slanted.</param>
/// <param name="FontSize">The size of the letters in points.</param>
/// <param name="Color">The colour of the letters as an opaque ARGB value.</param>
/// <param name="Opacity">How solid the text is, from just above 0 to 1.</param>
/// <param name="RotationDegrees">How far the text is turned, anticlockwise.</param>
/// <param name="Anchor">Where on the page the text sits.</param>
/// <param name="Margin">How far the text stays from the edge, in points.</param>
/// <param name="Behind">Whether the text is drawn under the page's own content.</param>
[DebuggerDisplay("PageTextStamp: {Text} at {Anchor}")]
public sealed record PageTextStamp(
    string Text,
    PageTextFamily Family,
    bool Bold,
    bool Italic,
    float FontSize,
    uint Color,
    float Opacity,
    float RotationDegrees,
    PageStampAnchor Anchor,
    float Margin,
    bool Behind)
{
    /// <summary>Gets the number <c>{number}</c> counts from, on the first stamped page.</summary>
    public int FirstNumber { get; init; } = 1;

    /// <summary>Gets how many digits <c>{number}</c> is written with, padded with zeros on the left; 0 writes it as it is.</summary>
    public int Digits { get; init; }
}
