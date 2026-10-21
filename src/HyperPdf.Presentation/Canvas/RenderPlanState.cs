// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>What the page canvas remembers from frame to frame to decide what to draw and what to ask the renderer for.</summary>
[DebuggerDisplay("RenderPlanState: fallback scale {FallbackScale}")]
public sealed class RenderPlanState
{
    /// <summary>Gets or sets the tile scale of the last frame whose visible pages were all sharp, drawn while newer tiles render.</summary>
    public float FallbackScale { get; set; }

    /// <summary>Gets or sets a value indicating whether every visible page in the frame being drawn had its sharp tiles.</summary>
    public bool FrameComplete { get; set; }

    /// <summary>Gets or sets the tab the last tile request was made for.</summary>
    public DocumentTabViewModel? RenderTab { get; set; }

    /// <summary>Gets or sets the document used by the last tile request.</summary>
    public IDocument? RenderDocument { get; set; }

    /// <summary>Gets or sets the viewport used by the last tile request.</summary>
    public LayoutRect RenderViewport { get; set; }

    /// <summary>Gets or sets the display scale used by the last tile request.</summary>
    public double RenderScaling { get; set; }

    /// <summary>Gets or sets the layout used by the last tile request.</summary>
    public LayoutOptions RenderLayout { get; set; }

    /// <summary>Gets or sets the rotation used by the last tile request.</summary>
    public PageRotation RenderRotation { get; set; }

    /// <summary>Gets or sets the tone used by the last tile request.</summary>
    public int RenderToneId { get; set; }

    /// <summary>Gets or sets the top of the viewport at the last frame, to tell which way the reader is scrolling.</summary>
    public double PreviousViewportTop { get; set; }

    /// <summary>Gets or sets what the last frame's prefetch passes depended on.</summary>
    public PrefetchStamp LastPrefetch { get; set; }

    /// <summary>Gets or sets a value indicating whether the last frame found every tile and preview it looked for already cached, so it asked for nothing.</summary>
    public bool LastPrefetchSettled { get; set; }

    /// <summary>Gets or sets a value indicating whether this frame skips the prefetch passes because they would find what the last frame found.</summary>
    public bool SkipPrefetch { get; set; }

    /// <summary>Gets the render requests made so far in this frame.</summary>
    public int RequestsThisFrame { get; private set; }

    /// <summary>Gets or sets a value indicating whether the reader last scrolled towards the end of the document, so previews are made further ahead on that side.</summary>
    public bool ScrollingDown { get; set; } = true;

    /// <summary>Starts a frame: nothing has been requested yet, and every visible page is taken to be sharp until one is found that is not.</summary>
    public void StartFrame()
    {
        FrameComplete = true;
        RequestsThisFrame = 0;
    }

    /// <summary>Notes that a render request was made in this frame.</summary>
    public void NoteRequest() => RequestsThisFrame++;

    /// <summary>Forgets the tab and document the last request was made for, so the next frame starts a new generation.</summary>
    public void ForgetRequest()
    {
        RenderTab = null;
        RenderDocument = null;
    }
}
