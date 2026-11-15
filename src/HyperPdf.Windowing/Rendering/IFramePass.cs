// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Windowing.Rendering;

/// <summary>One frame being drawn. Disposing it presents the frame.</summary>
public interface IFramePass : IDisposable
{
    /// <summary>Gets the size of the frame in device pixels.</summary>
    SurfaceSize Size { get; }

    /// <summary>Fills the frame with a solid colour.</summary>
    /// <param name="argb">The colour as 0xAARRGGBB.</param>
    void Clear(uint argb);

    /// <summary>Draws a colour over a rectangle. A colour that is not opaque is blended over what is there.</summary>
    /// <param name="area">The rectangle in canvas units.</param>
    /// <param name="argb">The colour as 0xAARRGGBB.</param>
    void FillRect(in LayoutRect area, uint argb);

    /// <summary>Draws a tile surface made by this backend's own factory.</summary>
    /// <param name="surface">The surface.</param>
    /// <param name="destination">Where it goes, in canvas units.</param>
    /// <param name="smooth">Whether it is sampled smoothly because it is scaled.</param>
    void DrawSurface(IRenderSurface surface, in LayoutRect destination, bool smooth);
}
