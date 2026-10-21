// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Presentation.Canvas;

/// <summary>What the page canvas needs from a toolkit to put pages on screen: paper, cached tiles and the marks drawn over a page.</summary>
public interface ITilePainter
{
    /// <summary>Starts drawing the pages, with page images sampled so neighbouring tiles meet without seams.</summary>
    void BeginPages();

    /// <summary>Finishes drawing the pages.</summary>
    void EndPages();

    /// <summary>Draws the shadow and the paper of a page.</summary>
    /// <param name="page">The page on the canvas.</param>
    /// <param name="tone">The tone the paper is drawn in.</param>
    void DrawPaper(in LayoutRect page, PageTone tone);

    /// <summary>Draws a cached tile or preview, or queues it with the other tiles of the page.</summary>
    /// <param name="surface">The cached surface.</param>
    /// <param name="destination">Where it goes on the canvas.</param>
    /// <param name="smooth">Whether it is scaled as a preview or stand-in, and so sampled smoothly.</param>
    void DrawSurface(IRenderSurface surface, in LayoutRect destination, bool smooth);

    /// <summary>Draws the surfaces queued so far.</summary>
    void FlushSurfaces();

    /// <summary>Draws what goes over a visible page once its tiles are down, such as the text selection.</summary>
    /// <param name="page">The page index.</param>
    /// <param name="bounds">The page on the canvas.</param>
    void DrawPageOverlay(int page, in LayoutRect bounds);
}
