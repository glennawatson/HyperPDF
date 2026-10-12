// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Annotations;

/// <summary>
/// Sizes and moves a signature mark on a page while it is being placed. Every result keeps the mark's shape and stays
/// on the page, so keyboard steps never push it out of sight.
/// </summary>
public static class SignatureMarkLayout
{
    /// <summary>How far one arrow key press moves a mark, in points.</summary>
    public static readonly float MoveStep = 10;

    /// <summary>How far one fine arrow key press (with Shift) moves a mark, in points.</summary>
    public static readonly float FineMoveStep = 1;

    /// <summary>How much one resize step grows or shrinks a mark.</summary>
    public static readonly float ResizeStep = 1.25F;

    /// <summary>The height a signature starts at, in points: half an inch.</summary>
    internal const float SignatureHeight = 36;

    /// <summary>The height initials start at, in points: a third of an inch.</summary>
    internal const float InitialsHeight = 24;

    /// <summary>The smallest height a mark can shrink to, in points.</summary>
    internal const float MinHeight = 8;

    /// <summary>The largest share of the page width a new mark takes.</summary>
    private const float MaxStartWidthShare = 0.6F;

    /// <summary>Half, to centre a mark.</summary>
    private const float Half = 0.5F;

    /// <summary>The coordinates in each stored point.</summary>
    private const int Coordinates = 2;

    /// <summary>Gets where a mark starts: centred on the page at its kind's usual height.</summary>
    /// <param name="mark">The mark.</param>
    /// <param name="page">The page size in points.</param>
    /// <returns>The bounds in page space.</returns>
    public static PageRect Start(SignatureMark mark, PageSize page)
    {
        ArgumentNullException.ThrowIfNull(mark);
        var height = mark.Kind == SignatureMarkKind.Initials ? InitialsHeight : SignatureHeight;
        var width = height * mark.AspectRatio;
        var maxWidth = page.Width * MaxStartWidthShare;
        if (width > maxWidth)
        {
            width = maxWidth;
            height = width / mark.AspectRatio;
        }

        return Clamp(new((page.Width - width) * Half, (page.Height - height) * Half, width, height), page);
    }

    /// <summary>Moves a mark, keeping it on the page.</summary>
    /// <param name="bounds">The mark's bounds.</param>
    /// <param name="dx">The move to the right, in points.</param>
    /// <param name="dy">The move down, in points.</param>
    /// <param name="page">The page size in points.</param>
    /// <returns>The moved bounds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PageRect Move(PageRect bounds, float dx, float dy, PageSize page) =>
        Clamp(bounds with { Left = bounds.Left + dx, Top = bounds.Top + dy }, page);

    /// <summary>Centres a mark on a point, keeping it on the page.</summary>
    /// <param name="bounds">The mark's bounds.</param>
    /// <param name="centre">The new centre.</param>
    /// <param name="page">The page size in points.</param>
    /// <returns>The moved bounds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PageRect CentreOn(PageRect bounds, PagePoint centre, PageSize page) =>
        Clamp(bounds with { Left = centre.X - (bounds.Width * Half), Top = centre.Y - (bounds.Height * Half) }, page);

    /// <summary>Grows or shrinks a mark about its centre, keeping its shape and keeping it on the page.</summary>
    /// <param name="bounds">The mark's bounds.</param>
    /// <param name="factor">The scale: more than 1 grows, less than 1 shrinks.</param>
    /// <param name="page">The page size in points.</param>
    /// <returns>The resized bounds.</returns>
    public static PageRect Resize(PageRect bounds, float factor, PageSize page)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(factor);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return bounds;
        }

        var aspect = bounds.Width / bounds.Height;
        var maxHeight = Math.Min(page.Height, page.Width / aspect);
        var height = Math.Clamp(bounds.Height * factor, Math.Min(MinHeight, maxHeight), maxHeight);
        var width = height * aspect;
        var centre = new PagePoint(bounds.Left + (bounds.Width * Half), bounds.Top + (bounds.Height * Half));
        return CentreOn(new(0, 0, width, height), centre, page);
    }

    /// <summary>Keeps a mark on the page, moving it in from any edge it crosses.</summary>
    /// <param name="bounds">The mark's bounds.</param>
    /// <param name="page">The page size in points.</param>
    /// <returns>The bounds inside the page.</returns>
    public static PageRect Clamp(PageRect bounds, PageSize page) => bounds with
    {
        Left = Math.Clamp(bounds.Left, 0, Math.Max(0, page.Width - bounds.Width)),
        Top = Math.Clamp(bounds.Top, 0, Math.Max(0, page.Height - bounds.Height)),
    };

    /// <summary>Gets the text size that fills a typed mark's height.</summary>
    /// <param name="height">The mark's height, in points or pixels.</param>
    /// <returns>The text size, in the same units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double TypedFontSize(double height) => height / SignatureMark.TypedLineHeight;

    /// <summary>Scales a drawn mark's points into its bounds on the page.</summary>
    /// <param name="mark">The drawn mark.</param>
    /// <param name="bounds">Where the mark goes, in page space.</param>
    /// <param name="destination">Receives one point per stored point.</param>
    /// <exception cref="ArgumentException">The destination is too small.</exception>
    public static void MapPoints(SignatureMark mark, PageRect bounds, Span<PagePoint> destination)
    {
        ArgumentNullException.ThrowIfNull(mark);
        var coordinates = mark.Points.Span;
        var count = coordinates.Length / Coordinates;
        if (destination.Length < count)
        {
            throw new ArgumentException("The destination is too small for the mark's points.", nameof(destination));
        }

        var scaleX = bounds.Width / mark.Width;
        var scaleY = bounds.Height / mark.Height;
        for (var i = 0; i < count; i++)
        {
            destination[i] = new(bounds.Left + (coordinates[i * Coordinates] * scaleX), bounds.Top + (coordinates[(i * Coordinates) + 1] * scaleY));
        }
    }
}
