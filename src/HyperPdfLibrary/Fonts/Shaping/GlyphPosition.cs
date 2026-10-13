// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Where a glyph goes, in font design units: how far the pen moves after it and where it is drawn relative to the pen.</summary>
/// <param name="XAdvance">The horizontal advance.</param>
/// <param name="XOffset">The shift of the drawn glyph to the right.</param>
/// <param name="YOffset">The shift of the drawn glyph upward.</param>
[DebuggerDisplay("GlyphPosition: advance {XAdvance} offset {XOffset},{YOffset}")]
internal record struct GlyphPosition(int XAdvance, int XOffset, int YOffset)
{
    /// <summary>Gets or sets the glyph this one is attached to, or -1.</summary>
    internal int AttachTo { get; set; } = -1;

    /// <summary>Gets or sets how the glyph is attached.</summary>
    internal AttachKind Attach { get; set; }

    /// <summary>Gets or sets the horizontal distance from the attached glyph's anchor to this glyph's anchor.</summary>
    internal int AttachX { get; set; }

    /// <summary>Gets or sets the vertical distance from the attached glyph's anchor to this glyph's anchor.</summary>
    internal int AttachY { get; set; }
}
