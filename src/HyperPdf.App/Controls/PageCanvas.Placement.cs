// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Annotations;

namespace HyperPdf.App.Controls;

/// <summary>
/// Placing a signature or initials: the mark is previewed on the page inside an outline. Arrow keys move it (Shift for
/// small steps) in the direction they point on screen, + and - resize it, Page Up and Page Down change page, Enter
/// places it and Escape cancels. A click moves it to the pointer.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>The width of a drawn signature's ink on the page, in points.</summary>
    private const double MarkInkWidth = 1.5;

    /// <summary>How far the placement outline sits outside the mark.</summary>
    private const double PlacementInset = 4;

    /// <summary>The smallest zoom change that makes the ink pen again.</summary>
    private const double PenTolerance = 0.001;

    /// <summary>The ink brush for typed marks.</summary>
    private static readonly IImmutableSolidColorBrush MarkInk = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF000000U | AnnotationColors.Ink));

    /// <summary>Draws the mark being placed, while the canvas is shown.</summary>
    private SignatureMarkPainter? _markPainter;

    /// <summary>The pen for a drawn mark's ink at the current zoom.</summary>
    private ImmutablePen? _markPen;

    /// <summary>Places the mark; Enter is used even when placing fails, as the failure is shown as a notice.</summary>
    /// <param name="placing">The tab's Fill &amp; Sign state.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool Commit(FillAndSignViewModel placing)
    {
        _ = placing.Commit();
        return true;
    }

    /// <summary>Handles the placing keys while a mark is being placed.</summary>
    /// <param name="e">The key press.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    private bool HandlePlacementKey(KeyEventArgs e)
    {
        if (Tab is not { FillAndSign: { Placement: not null } placing } tab)
        {
            return false;
        }

        // Ctrl shortcuts, such as zooming, still reach the window.
        if ((e.KeyModifiers & ~KeyModifiers.Shift) != 0)
        {
            return false;
        }

        var step = (e.KeyModifiers & KeyModifiers.Shift) != 0 ? SignatureMarkLayout.FineMoveStep : SignatureMarkLayout.MoveStep;
        var used = e.Key switch
        {
            Key.Left => MovePlacement(tab, -step, 0),
            Key.Right => MovePlacement(tab, step, 0),
            Key.Up => MovePlacement(tab, 0, -step),
            Key.Down => MovePlacement(tab, 0, step),
            Key.OemPlus or Key.Add => placing.Resize(SignatureMarkLayout.ResizeStep),
            Key.OemMinus or Key.Subtract => placing.Resize(1 / SignatureMarkLayout.ResizeStep),
            Key.PageDown => placing.MoveToPage(1),
            Key.PageUp => placing.MoveToPage(-1),
            Key.Enter => Commit(placing),
            Key.Escape => placing.Cancel(),
            _ => false,
        };
        if (used)
        {
            InvalidateVisual();
        }

        return used;
    }

    /// <summary>Moves the mark being placed by a distance on screen, so arrows follow the screen on a rotated page.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="dx">The move right on screen, in points.</param>
    /// <param name="dy">The move down on screen, in points.</param>
    /// <returns><see langword="true"/> when a mark is being placed.</returns>
    private bool MovePlacement(DocumentTabViewModel tab, float dx, float dy)
    {
        if (tab.FillAndSign.Placement is not { } placement || (uint)placement.Page >= (uint)_sizes.Length)
        {
            return false;
        }

        var transform = new PageTransform(_layout.GetPageBounds(placement.Page), _sizes[placement.Page], tab.Rotation, _layout.Options.Scale);
        var origin = transform.ToCanvas(new Core.Geometry.PagePoint(placement.Bounds.Left, placement.Bounds.Top));
        var start = transform.ToPage(origin);
        var end = transform.ToPage(origin + new Vector(dx * transform.Scale, dy * transform.Scale));
        return tab.FillAndSign.Move(end.X - start.X, end.Y - start.Y);
    }

    /// <summary>Moves the mark being placed to where the page was pressed.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page pressed, or -1.</param>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when a mark is being placed.</returns>
    private bool PressPlacement(DocumentTabViewModel tab, int page, Point position)
    {
        if (tab.FillAndSign.Placement is null)
        {
            return false;
        }

        if (page >= 0 && tab.FillAndSign.MoveTo(page, ToPage(tab, page, position)))
        {
            InvalidateVisual();
        }

        return true;
    }

    /// <summary>Draws the mark being placed and its outline.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    private void DrawPlacement(DrawingContext context, DocumentTabViewModel tab)
    {
        if (tab.FillAndSign.Placement is not { } placement || (uint)placement.Page >= (uint)_sizes.Length)
        {
            return;
        }

        var transform = new PageTransform(_layout.GetPageBounds(placement.Page), _sizes[placement.Page], tab.Rotation, _layout.Options.Scale);
        var area = transform.ToCanvas(placement.Bounds);
        var inkWidth = MarkInkWidth * transform.Scale;
        if (_markPen is null || Math.Abs(_markPen.Thickness - inkWidth) > PenTolerance)
        {
            // Made again only when the zoom changes, so redraws while placing do not allocate.
            _markPen = new(MarkInk, inkWidth, lineCap: PenLineCap.Round);
        }

        (_markPainter ??= new()).Draw(context, placement.Mark, area, MarkInk, _markPen);
        if (_currentHitPen is { } outline)
        {
            context.DrawRectangle(null, outline, area.Inflate(PlacementInset));
        }
    }
}
