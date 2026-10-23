// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>The paper, margins and numbering of the pages of a section.</summary>
[DebuggerDisplay("PdfPageSetup: {Size} {Orientation}")]
public sealed record PdfPageSetup
{
    /// <summary>The default margin in points, one inch.</summary>
    private const float DefaultMargin = 72F;

    /// <summary>The default distance of a header or footer from the edge of the paper in points.</summary>
    private const float DefaultRunningDistance = 36F;

    /// <summary>Gets the paper size, upright.</summary>
    public PdfPageSize Size { get; init; } = PdfPageSize.A4;

    /// <summary>Gets which way up the paper is used.</summary>
    public PdfOrientation Orientation { get; init; }

    /// <summary>Gets the top margin in points.</summary>
    public float MarginTop { get; init; } = DefaultMargin;

    /// <summary>Gets the bottom margin in points.</summary>
    public float MarginBottom { get; init; } = DefaultMargin;

    /// <summary>Gets the left margin in points; on a mirrored odd page, the inside margin.</summary>
    public float MarginLeft { get; init; } = DefaultMargin;

    /// <summary>Gets the right margin in points; on a mirrored odd page, the outside margin.</summary>
    public float MarginRight { get; init; } = DefaultMargin;

    /// <summary>Gets the extra space in points added to the inside margin for binding.</summary>
    public float Gutter { get; init; }

    /// <summary>Gets a value indicating whether the left and right margins swap on even pages, so facing pages have matching inside and outside margins.</summary>
    public bool MirrorMargins { get; init; }

    /// <summary>Gets how far a header is from the top edge of the paper in points.</summary>
    public float HeaderDistance { get; init; } = DefaultRunningDistance;

    /// <summary>Gets how far a footer is from the bottom edge of the paper in points.</summary>
    public float FooterDistance { get; init; } = DefaultRunningDistance;

    /// <summary>Gets a value indicating whether the first page of the section has its own header and footer.</summary>
    public bool DifferentFirstPage { get; init; }

    /// <summary>Gets a value indicating whether even pages have their own header and footer.</summary>
    public bool DifferentOddAndEven { get; init; }

    /// <summary>Gets the number of the section's first page, or <see langword="null"/> to carry on from the section before.</summary>
    public int? StartPageNumber { get; init; }

    /// <summary>Gets how page numbers are written.</summary>
    public PdfNumberFormat PageNumberFormat { get; init; }

    /// <summary>Gets the width of the paper as it is used, in points.</summary>
    public float PageWidth => Orientation == PdfOrientation.Landscape ? Size.Height : Size.Width;

    /// <summary>Gets the height of the paper as it is used, in points.</summary>
    public float PageHeight => Orientation == PdfOrientation.Landscape ? Size.Width : Size.Height;
}
