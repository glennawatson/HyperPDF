// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Tests;

/// <summary>A marks painter with no toolkit: it counts what it is told to draw.</summary>
internal sealed class FakeMarksPainter : IMarksPainter
{
    /// <summary>Gets or sets a value indicating whether the toolkit has a colour for the spoken sentence.</summary>
    public bool HasSpoken { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the toolkit has a colour for dimming.</summary>
    public bool HasDim { get; set; } = true;

    /// <summary>Gets how many hits were drawn.</summary>
    public int Hits { get; private set; }

    /// <summary>Gets how many current hits were drawn.</summary>
    public int CurrentHits { get; private set; }

    /// <summary>Gets how many spoken rectangles were filled.</summary>
    public int Spoken { get; private set; }

    /// <summary>Gets how many words were underlined.</summary>
    public int Underlines { get; private set; }

    /// <summary>Gets how many areas were dimmed.</summary>
    public int Dims { get; private set; }

    /// <summary>Gets how many marks were drawn in all.</summary>
    public int Marks => Hits + Spoken + Underlines + Dims;

    /// <inheritdoc/>
    public bool CanDraw(bool dim) => dim ? HasDim : HasSpoken;

    /// <inheritdoc/>
    public void DrawHit(in LayoutRect rect, bool current)
    {
        Hits++;
        CurrentHits += current ? 1 : 0;
    }

    /// <inheritdoc/>
    public void FillSpoken(in LayoutRect rect) => Spoken++;

    /// <inheritdoc/>
    public void UnderlineWord(CanvasPoint from, CanvasPoint to) => Underlines++;

    /// <inheritdoc/>
    public void FillDim(in LayoutRect rect) => Dims++;
}
