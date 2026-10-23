// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Presentation.Tests;

/// <summary>A painter with no toolkit: it counts what it is told to draw.</summary>
internal sealed class FakeTilePainter : ITilePainter
{
    /// <summary>Gets how many times the pages were started.</summary>
    public int Begins { get; private set; }

    /// <summary>Gets how many times the pages were finished.</summary>
    public int Ends { get; private set; }

    /// <summary>Gets how many pages of paper were drawn.</summary>
    public int Papers { get; private set; }

    /// <summary>Gets how many surfaces were drawn.</summary>
    public int Surfaces { get; private set; }

    /// <summary>Gets how many surfaces were drawn smoothly.</summary>
    public int SmoothSurfaces { get; private set; }

    /// <summary>Gets how many times the queued surfaces were flushed.</summary>
    public int Flushes { get; private set; }

    /// <summary>Gets how many pages had their overlay drawn.</summary>
    public int Overlays { get; private set; }

    /// <inheritdoc/>
    public void BeginPages() => Begins++;

    /// <inheritdoc/>
    public void EndPages() => Ends++;

    /// <inheritdoc/>
    public void DrawPaper(in LayoutRect page, PageTone tone) => Papers++;

    /// <inheritdoc/>
    public void DrawSurface(IRenderSurface surface, in LayoutRect destination, bool smooth)
    {
        Surfaces++;
        SmoothSurfaces += smooth ? 1 : 0;
    }

    /// <inheritdoc/>
    public void FlushSurfaces() => Flushes++;

    /// <inheritdoc/>
    public void DrawPageOverlay(int page, in LayoutRect bounds) => Overlays++;
}
