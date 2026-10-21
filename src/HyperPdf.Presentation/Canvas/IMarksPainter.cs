// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>What the layer over the pages needs from a toolkit to mark search hits, the sentence being read aloud and the focus band.</summary>
public interface IMarksPainter
{
    /// <summary>Tells whether the toolkit has a colour for the dimming of the focus band and the fill of the spoken sentence.</summary>
    /// <param name="dim"><see langword="true"/> to ask about the dimming, and <see langword="false"/> to ask about the spoken sentence.</param>
    /// <returns><see langword="true"/> when marks of that kind can be drawn.</returns>
    bool CanDraw(bool dim);

    /// <summary>Marks a search hit, outlining the current one.</summary>
    /// <param name="rect">The hit on the canvas.</param>
    /// <param name="current">Whether it is the current hit.</param>
    void DrawHit(in LayoutRect rect, bool current);

    /// <summary>Softly fills the sentence being read aloud.</summary>
    /// <param name="rect">The part of the sentence on the canvas.</param>
    void FillSpoken(in LayoutRect rect);

    /// <summary>Underlines the word being read aloud.</summary>
    /// <param name="from">The left end of the line.</param>
    /// <param name="to">The right end of the line.</param>
    void UnderlineWord(CanvasPoint from, CanvasPoint to);

    /// <summary>Dims an area with the translucent paper colour.</summary>
    /// <param name="rect">The area on the canvas.</param>
    void FillDim(in LayoutRect rect);
}
