// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A tab stop: a place along the line that a tab character jumps to.</summary>
/// <param name="Position">The distance in points from the left edge of the text area.</param>
/// <param name="Alignment">How the text after the tab lines up with the stop.</param>
/// <param name="Leader">What fills the space the tab skips.</param>
[DebuggerDisplay("PdfTabStop: {Position} pt, {Alignment}, {Leader}")]
public readonly record struct PdfTabStop(float Position, PdfTabAlignment Alignment, PdfTabLeader Leader)
{
    /// <summary>Makes a left tab stop with no leader.</summary>
    /// <param name="position">The distance in points from the left edge of the text area.</param>
    /// <returns>The tab stop.</returns>
    public static PdfTabStop At(float position) => new(position, PdfTabAlignment.Left, PdfTabLeader.None);
}
