// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// The bounds of what each marked content id paints other than text, as two arrays of the same length. The ids are in
/// ascending order so a lookup is a binary search, and a page that paints nothing shares <see cref="Empty"/>.
/// </summary>
/// <param name="Ids">The marked content ids, in ascending order, each once.</param>
/// <param name="Bounds">The bounds of each id, at the same index.</param>
[DebuggerDisplay("McidGraphics: {Ids.Length} ids")]
internal readonly record struct McidGraphics(int[] Ids, PdfViewerRect[] Bounds)
{
    /// <summary>Gets the value for a page that paints no path or image in marked content; pages only read it.</summary>
    internal static McidGraphics Empty { get; } = new([], []);

    /// <summary>Gets the bounds of an id's paths and images.</summary>
    /// <param name="mcid">The marked content id.</param>
    /// <returns>The bounds; empty when the id paints none.</returns>
    internal PdfViewerRect Find(int mcid)
    {
        var at = Ids.AsSpan().BinarySearch(mcid);
        return at >= 0 ? Bounds[at] : default;
    }

    /// <summary>Determines whether an id paints a path or image.</summary>
    /// <param name="mcid">The marked content id.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal bool Contains(int mcid) => Ids.AsSpan().BinarySearch(mcid) >= 0;
}
