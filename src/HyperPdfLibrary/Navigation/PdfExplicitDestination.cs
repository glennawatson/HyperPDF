// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>An explicit destination to write: a page, a fit and the numbers the fit uses (ISO 32000-2 12.3.2.2).</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Fit">How the page fits the window.</param>
/// <param name="Left">The left coordinate, in default user space, or <see langword="null"/> to keep the current one.</param>
/// <param name="Top">The top coordinate, or <see langword="null"/> to keep the current one.</param>
/// <param name="Right">The right coordinate; used by <see cref="PdfDestinationFit.FitRectangle"/> only.</param>
/// <param name="Bottom">The bottom coordinate; used by <see cref="PdfDestinationFit.FitRectangle"/> only.</param>
/// <param name="Zoom">The zoom factor (1 is 100 percent); used by <see cref="PdfDestinationFit.Xyz"/> only.</param>
[DebuggerDisplay("PdfExplicitDestination: page {PageIndex} {Fit}")]
public sealed record PdfExplicitDestination(int PageIndex, PdfDestinationFit Fit, float? Left, float? Top, float? Right, float? Bottom, float? Zoom)
{
    /// <summary>Makes a destination that fits the whole page.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The destination.</returns>
    public static PdfExplicitDestination FitPage(int pageIndex) => new(pageIndex, PdfDestinationFit.Fit, null, null, null, null, null);

    /// <summary>Makes a destination that shows the top of a page, keeping the current zoom.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The destination.</returns>
    public static PdfExplicitDestination TopOfPage(int pageIndex) => new(pageIndex, PdfDestinationFit.Xyz, null, null, null, null, null);

    /// <summary>Makes a destination at a position on a page.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="left">The left coordinate, or <see langword="null"/>.</param>
    /// <param name="top">The top coordinate, or <see langword="null"/>.</param>
    /// <param name="zoom">The zoom factor, or <see langword="null"/>.</param>
    /// <returns>The destination.</returns>
    public static PdfExplicitDestination At(int pageIndex, float? left, float? top, float? zoom) => new(pageIndex, PdfDestinationFit.Xyz, left, top, null, null, zoom);
}
