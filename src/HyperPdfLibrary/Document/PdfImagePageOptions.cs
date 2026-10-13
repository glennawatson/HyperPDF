// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>How an image becomes a page: at the size its resolution gives, or fitted on a sheet of paper.</summary>
[DebuggerDisplay("PdfImagePageOptions: {Dpi} dpi, paper {PaperWidth}x{PaperHeight}")]
public sealed record PdfImagePageOptions
{
    /// <summary>The points in an inch.</summary>
    private const float PointsPerInch = 72;

    /// <summary>Gets the options that give each image a page the size of the image at 72 pixels to the inch.</summary>
    public static PdfImagePageOptions Default { get; } = new();

    /// <summary>Gets the resolution of the image in pixels to the inch; the page is the image's size at this resolution. Used when there is no paper size.</summary>
    public float Dpi { get; init; } = PointsPerInch;

    /// <summary>Gets the paper width in points, or 0 to size the page from the image.</summary>
    public float PaperWidth { get; init; }

    /// <summary>Gets the paper height in points, or 0 to size the page from the image.</summary>
    public float PaperHeight { get; init; }

    /// <summary>Gets the blank border kept around an image fitted on paper, in points.</summary>
    public float Margin { get; init; }
}
