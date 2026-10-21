// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Geometry;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.ViewModels.Markup;

/// <summary>The corners placed so far for a polygon or poly-line, before the shape is finished.</summary>
[DebuggerDisplay("PolygonDraft: page {Page}, {Corners.Count} corners")]
public sealed class PolygonDraft
{
    /// <summary>The corners placed, in page space.</summary>
    private readonly List<PagePoint> _corners = [];

    /// <summary>Gets the page the corners are on, or -1 when no corner is placed.</summary>
    public int Page { get; private set; } = -1;

    /// <summary>Gets where the next side's free end is, in page space.</summary>
    public PagePoint Hover { get; private set; }

    /// <summary>Gets a value indicating whether any corner has been placed.</summary>
    public bool IsActive => Page >= 0;

    /// <summary>Gets the corners placed, in page space.</summary>
    public IReadOnlyList<PagePoint> Corners => _corners;

    /// <summary>Places a corner, or finishes the polygon when the press was a double-click.</summary>
    /// <param name="annotations">The annotation state that receives a finished shape.</param>
    /// <param name="page">The page pressed, or -1.</param>
    /// <param name="point">The point pressed, in page space.</param>
    /// <param name="isDoubleClick">Whether the press was a double-click.</param>
    /// <returns><see langword="true"/> when the draft changed and the view should redraw.</returns>
    public bool Press(AnnotationsViewModel annotations, int page, PagePoint point, bool isDoubleClick)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        if (page < 0 || (IsActive && page != Page))
        {
            return false;
        }

        if (isDoubleClick)
        {
            return Finish(annotations);
        }

        Page = page;
        Hover = point;
        _corners.Add(point);
        return true;
    }

    /// <summary>Moves the free end of the next side.</summary>
    /// <param name="point">The point, in page space.</param>
    /// <returns><see langword="true"/> when a polygon is being drawn, so the view should redraw.</returns>
    public bool MoveHover(PagePoint point)
    {
        if (!IsActive)
        {
            return false;
        }

        Hover = point;
        return true;
    }

    /// <summary>Forgets the placed corners.</summary>
    /// <returns><see langword="true"/> when there were corners, so the view should redraw.</returns>
    public bool Cancel()
    {
        if (!IsActive)
        {
            return false;
        }

        _corners.Clear();
        Page = -1;
        return true;
    }

    /// <summary>Adds the polygon from the placed corners and starts again.</summary>
    /// <param name="annotations">The annotation state that receives the shape.</param>
    /// <returns><see langword="true"/> when there were corners to finish.</returns>
    public bool Finish(AnnotationsViewModel annotations)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        if (!IsActive)
        {
            return false;
        }

        if (AnnotationsViewModel.GetPolygonKind(annotations.Tool) is { } kind)
        {
            _ = annotations.AddPolygon(Page, kind, CollectionsMarshal.AsSpan(_corners));
        }

        return Cancel();
    }

    /// <summary>Handles Enter (finish), Backspace (take back a corner) and Escape (start again) while placing corners.</summary>
    /// <param name="annotations">The annotation state that receives a finished shape.</param>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the key was used and the view should redraw.</returns>
    public bool HandleKey(AnnotationsViewModel annotations, ViewerKey key)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (key)
        {
            case ViewerKey.Enter:
            {
                return Finish(annotations);
            }

            case ViewerKey.Escape:
            {
                return Cancel();
            }

            case ViewerKey.Back:
            {
                _corners.RemoveAt(_corners.Count - 1);
                if (_corners.Count == 0)
                {
                    Page = -1;
                }

                return true;
            }

            default:
            {
                return false;
            }
        }
    }
}
