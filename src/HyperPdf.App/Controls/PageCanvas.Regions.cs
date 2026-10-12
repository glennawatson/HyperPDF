// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Forms.Detection;

namespace HyperPdf.App.Controls;

/// <summary>
/// Shows the places to write found on a printed form while the Text tool is on: a quiet dashed outline around each,
/// and a solid one around the place under the pointer, where a click will type.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>The width of a suggestion's outline.</summary>
    private const double SuggestionWidth = 1;

    /// <summary>The width of the outline of the suggestion under the pointer.</summary>
    private const double HoveredSuggestionWidth = 2;

    /// <summary>The dashes of a suggestion's outline.</summary>
    private static readonly ImmutableDashStyle SuggestionDashes = new([3, 3], 0);

    /// <summary>The dashed outline of a suggestion, kept until the outline colour changes.</summary>
    private Pen? _suggestionPen;

    /// <summary>The solid outline of the suggestion under the pointer, kept until the outline colour changes.</summary>
    private Pen? _hoveredSuggestionPen;

    /// <summary>The place under the pointer, with its page, or <see langword="null"/>.</summary>
    private (int Page, FormRegion Region)? _hoveredRegion;

    /// <summary>Follows the pointer over the places to write, redrawing when it moves onto or off one.</summary>
    /// <param name="position">The canvas point.</param>
    private void HoverRegion(Point position)
    {
        (int Page, FormRegion Region)? hovered = null;
        if (Tab is { Annotations: { Tool: AnnotationTool.Text, IsEditingText: false } annotations } tab)
        {
            var page = _layout.HitTest(position.X, position.Y);
            if (page >= 0 && annotations.RegionAt(page, ToPage(tab, page, position)) is { } region)
            {
                hovered = (page, region);
            }
        }

        if (hovered == _hoveredRegion)
        {
            return;
        }

        _hoveredRegion = hovered;
        InvalidateVisual();
    }

    /// <summary>Makes the suggestion outlines again when their colour changes, so drawing does not allocate.</summary>
    /// <param name="brush">The outline colour.</param>
    private void UpdateSuggestionPens(IBrush? brush)
    {
        if (_suggestionPen is not null && ReferenceEquals(_suggestionPen.Brush, brush))
        {
            return;
        }

        _suggestionPen = new(brush, SuggestionWidth, SuggestionDashes);
        _hoveredSuggestionPen = new(brush, HoveredSuggestionWidth);
    }

    /// <summary>Outlines the places to write on the pages in view while the Text tool is on.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="viewport">The viewport.</param>
    private void DrawRegions(DrawingContext context, DocumentTabViewModel tab, in Rect viewport)
    {
        var annotations = tab.Annotations;
        if (annotations.Tool != AnnotationTool.Text || annotations.IsEditingText)
        {
            return;
        }

        _layout.GetVisiblePages(viewport.Top, viewport.Bottom, out var first, out var last);
        UpdateSuggestionPens((_currentHitPen ?? StrokePen).Brush);
        for (var page = first; page <= last && page >= 0; page++)
        {
            foreach (var region in annotations.RegionsOn(page))
            {
                var hovered = _hoveredRegion is { } under && under.Page == page && under.Region == region;
                context.DrawRectangle(null, hovered ? _hoveredSuggestionPen : _suggestionPen, GetCanvasRect(page, region.Bounds));
            }
        }
    }
}
