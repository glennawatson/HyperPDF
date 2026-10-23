// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>Where the text area of a page is: its sides, top and bottom, after margins, gutter, mirroring and the room headers and footers take.</summary>
/// <param name="PageWidth">The width of the paper.</param>
/// <param name="PageHeight">The height of the paper.</param>
/// <param name="Left">The x of the left edge of the text area.</param>
/// <param name="Width">The width of the text area.</param>
/// <param name="Top">The y of the top of the text area.</param>
/// <param name="Bottom">The y of the bottom of the text area.</param>
[DebuggerDisplay("PageGeometry: {PageWidth} x {PageHeight}, text {Width} x {Height}")]
internal readonly record struct PageGeometry(float PageWidth, float PageHeight, float Left, float Width, float Top, float Bottom)
{
    /// <summary>The gap in points between a header or footer and the text.</summary>
    private const float RunningGap = 6F;

    /// <summary>Gets the height of the text area.</summary>
    internal float Height => Top - Bottom;

    /// <summary>Works out the geometry of a page.</summary>
    /// <param name="setup">The section's page setup.</param>
    /// <param name="pageNumber">The page's number in the document, counted from 1, whose oddness decides which side the inside margin is on.</param>
    /// <param name="headerHeight">The height of the page's header, or zero for none.</param>
    /// <param name="footerHeight">The height of the page's footer, or zero for none.</param>
    /// <returns>The geometry.</returns>
    internal static PageGeometry For(PdfPageSetup setup, int pageNumber, float headerHeight, float footerHeight)
    {
        var inside = setup.MarginLeft + setup.Gutter;
        var outside = setup.MarginRight;
        var mirrored = setup.MirrorMargins && (pageNumber & 1) == 0;
        var left = mirrored ? outside : inside;
        var right = mirrored ? inside : outside;
        var top = MathF.Max(setup.MarginTop, headerHeight > 0F ? setup.HeaderDistance + headerHeight + RunningGap : 0F);
        var bottom = MathF.Max(setup.MarginBottom, footerHeight > 0F ? setup.FooterDistance + footerHeight + RunningGap : 0F);
        return new(setup.PageWidth, setup.PageHeight, left, setup.PageWidth - left - right, setup.PageHeight - top, bottom);
    }
}
