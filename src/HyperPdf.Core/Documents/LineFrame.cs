// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>A line seen as it reads, whichever way the page turns it.</summary>
/// <param name="Start">Where the line starts along the way it reads.</param>
/// <param name="End">Where the line ends along the way it reads.</param>
/// <param name="Top">Where the line's first side is, across the way it reads; the next line of the paragraph is further on.</param>
/// <param name="Bottom">Where the line's last side is.</param>
internal readonly record struct LineFrame(float Start, float End, float Top, float Bottom)
{
    /// <summary>Gets how tall the line is, across the way it reads.</summary>
    internal float Height => Bottom - Top;
}
