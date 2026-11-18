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

    /// <summary>
    /// Limits what is drawn from now on to a rectangle, until <see cref="ClearClip"/>, as a page transition does to show one page through
    /// the part of the window the other has not yet covered. A pass that cannot clip draws everything, which shows the wrong page there
    /// but never a broken frame.
    /// </summary>
    /// <param name="area">The rectangle in frame pixels.</param>
    void SetClip(in LayoutRect area)
    {
        // A pass that does not clip draws whole.
    }

    /// <summary>Lets drawing reach the whole frame again.</summary>
    void ClearClip()
    {
        // A pass that does not clip has nothing to clear.
    }

    /// <summary>
    /// Makes everything drawn from now on, until the opacity is set to 1 again, count for only that much over what is already in the frame,
    /// as one layer: where the layer is transparent the frame shows as it was. A page transition uses it to fade one page in over another.
    /// A pass that cannot fade draws the layer opaque, which cuts to the new page but never breaks the frame.
    /// </summary>
    /// <param name="opacity">How much the layer counts, from 0 to 1.</param>
    void SetOpacity(double opacity)
    {
        // A pass that does not fade draws opaque.
    }

    /// <summary>
    /// Starts a trial of a group of draws that is the same in most frames, such as the window's own controls. Until
    /// <see cref="EndKeptTrial"/> the pass draws nothing and only follows what the group would draw. A pass that keeps no pixels
    /// between frames, or has none kept yet, answers <see langword="false"/>, and the group is then drawn between
    /// <see cref="BeginKeptGroup"/> and <see cref="EndKeptGroup"/>.
    /// </summary>
    /// <returns><see langword="true"/> when the trial has started.</returns>
    bool BeginKeptTrial() => false;

    /// <summary>
    /// Ends the trial. When the group is draw for draw the one whose pixels the pass kept, the pixels are put back in the frame
    /// and the group is done. Otherwise the caller draws the group between <see cref="BeginKeptGroup"/> and <see cref="EndKeptGroup"/>.
    /// </summary>
    /// <returns><see langword="true"/> when the kept pixels were put back.</returns>
    bool EndKeptTrial() => false;

    /// <summary>Starts the real drawing of a group whose pixels the pass keeps for later frames.</summary>
    void BeginKeptGroup()
    {
        // A pass that keeps no pixels draws the group like anything else.
    }

    /// <summary>Ends the group and keeps the pixels it left in the frame.</summary>
    void EndKeptGroup()
    {
        // A pass that keeps no pixels has nothing to keep.
    }
}
