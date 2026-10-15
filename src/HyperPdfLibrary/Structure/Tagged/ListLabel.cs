// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>The label that starts a line of a list: a bullet, a number or a letter, and the items it takes.</summary>
/// <param name="ItemCount">The items the label takes at the start of the line; zero when the line has no label.</param>
/// <param name="Kind">The label's kind; for a bullet, the bullet's shape.</param>
/// <param name="IsBullet">Whether the label is a bullet or dash rather than a count.</param>
/// <param name="Number">For a number, its value; for a letter, its place in the alphabet; otherwise zero.</param>
/// <param name="Roman">When the label may be a Roman numeral, its value; otherwise zero.</param>
/// <param name="BodyLeft">The left edge of the text after the label.</param>
[DebuggerDisplay("ListLabel: {Kind} {Number} ({ItemCount} items)")]
internal readonly record struct ListLabel(int ItemCount, PdfListNumbering Kind, bool IsBullet, int Number, int Roman, float BodyLeft)
{
    /// <summary>Gets a value indicating whether the line starts with a label.</summary>
    internal bool Exists => ItemCount > 0;
}
