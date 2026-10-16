// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>An image on a line of its own in a <see cref="PdfTextFlow"/> or inside a <see cref="PdfTableCell"/>.</summary>
/// <param name="Image">The image resource, added to the <see cref="PdfDocumentCreator"/> first.</param>
/// <param name="AlternativeText">What the image shows, for accessibility. An image with blank text is treated as a decorative layout artifact and omitted from the structure tree.</param>
/// <param name="WidthPoints">The requested width in points, or zero/negative to use the image's pixel width at 96 DPI (<c>0.75</c> points per pixel).</param>
/// <remarks>
/// <para>
/// When <see cref="WidthPoints"/> is zero or negative, the desired width is computed from the image's pixel width at
/// 96 DPI (<c>Image.Width * 0.75f</c> points). The height is always derived from the image's aspect ratio
/// (<c>width * (Image.Height / (float)Image.Width)</c>) so the image is never distorted.
/// </para>
/// <para>
/// Use <see cref="Fit"/> to control how images wider than the available content width (<c>PageWidth - 2 * Margin</c>)
/// or taller than the remaining space / content height (<c>PageHeight - 2 * Margin</c>) are scaled and paginated:
/// <see cref="PdfFlowImageFit.ScaleToPage"/> (the default) scales the image down to fit a single page's content box,
/// <see cref="PdfFlowImageFit.ScaleToRemaining"/> scales it to fit the remaining vertical space on the current page,
/// <see cref="PdfFlowImageFit.ScaleToWidth"/> scales width to the content width and clips any vertical overflow, and
/// <see cref="PdfFlowImageFit.ClipOverflow"/> preserves the requested dimensions and clips horizontal and vertical overflow.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var image = creator.AddRaster(raster, PdfImageOptions.Default, cancellationToken);
/// var blocks = new PdfFlowBlock[]
/// {
///     new PdfFlowImage(image, "Full-page diagram"),
///     new PdfFlowImage(image, "Inline thumbnail", WidthPoints: 180f)
///     {
///         Fit = PdfFlowImageFit.ScaleToRemaining,
///         Alignment = PdfTableAlignment.Center,
///     },
/// };
/// </code>
/// </example>
[DebuggerDisplay("PdfFlowImage: {Image}, {AlternativeText}, Fit={Fit}")]
public sealed record PdfFlowImage(PdfCreatedImage Image, string AlternativeText, float WidthPoints) : PdfFlowBlock
{
    /// <summary>Initializes a new instance of the <see cref="PdfFlowImage"/> class sized from its pixel dimensions at 96 DPI.</summary>
    /// <param name="image">The image resource, added to the <see cref="PdfDocumentCreator"/> first.</param>
    /// <param name="alternativeText">What the image shows, or <see cref="string.Empty"/> for a decorative image.</param>
    public PdfFlowImage(PdfCreatedImage image, string alternativeText)
        : this(image, alternativeText, 0F)
    {
    }

    /// <summary>
    /// Gets how the image is sized and paginated when its requested dimensions exceed the remaining space or page
    /// content bounds; the default is <see cref="PdfFlowImageFit.ScaleToPage"/>.
    /// </summary>
    public PdfFlowImageFit Fit { get; init; } = PdfFlowImageFit.ScaleToPage;

    /// <summary>
    /// Gets how the image is aligned horizontally within the available content width when narrower than the content area;
    /// the default is <see cref="PdfTableAlignment.Left"/>.
    /// </summary>
    public PdfTableAlignment Alignment { get; init; } = PdfTableAlignment.Left;
}
