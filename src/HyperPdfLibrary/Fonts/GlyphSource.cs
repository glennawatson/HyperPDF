// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// Where a font's glyphs come from: an embedded font program or a substituted system font. Glyph space has 1000 units
/// per em with y up, as PDF glyph widths do. Sources are safe to use from many threads.
/// </summary>
[DebuggerDisplay("{GetType().Name}: {GlyphCount} glyphs")]
internal abstract class GlyphSource
{
    /// <summary>Gets the number of glyphs.</summary>
    internal abstract int GlyphCount { get; }

    /// <summary>Gets the ascent in glyph space units, or zero when unknown.</summary>
    internal abstract float Ascent { get; }

    /// <summary>Gets the descent in glyph space units, usually negative, or zero when unknown.</summary>
    internal abstract float Descent { get; }

    /// <summary>Gets a value indicating whether the glyphs come from a system font rather than the PDF.</summary>
    internal abstract bool IsSubstitute { get; }

    /// <summary>Builds a glyph's outline in glyph space.</summary>
    /// <param name="glyph">The glyph id.</param>
    /// <returns>A new path the caller owns, or <see langword="null"/> when the glyph has no outline.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfPath? BuildOutline(int glyph) => BuildOutline(glyph, Matrix3x2.Identity);

    /// <summary>Builds a glyph's outline in glyph space and moves its points with a transform, without building the untransformed path first.</summary>
    /// <param name="glyph">The glyph id.</param>
    /// <param name="transform">The transform applied to the outline's points.</param>
    /// <returns>A new path the caller owns, or <see langword="null"/> when the glyph has no outline.</returns>
    internal abstract PdfPath? BuildOutline(int glyph, Matrix3x2 transform);

    /// <summary>Gets a glyph's advance width in glyph space units.</summary>
    /// <param name="glyph">The glyph id.</param>
    /// <returns>The advance, or zero for an unknown glyph.</returns>
    internal abstract float GetAdvance(int glyph);
}
