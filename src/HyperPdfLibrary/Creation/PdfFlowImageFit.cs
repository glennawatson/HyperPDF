// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>Specifies how <see cref="PdfTextFlow"/> sizes and paginates an oversized <see cref="PdfFlowImage"/>.</summary>
public enum PdfFlowImageFit
{
    /// <summary>
    /// Scales the image down uniformly (preserving aspect ratio) so its width does not exceed the page's content width
    /// (<c>PageWidth - 2 * Margin</c>) and its height does not exceed the page's content height
    /// (<c>PageHeight - 2 * Margin</c>). Starts a new page first when the scaled image does not fit in the remaining
    /// vertical space on the current page.
    /// </summary>
    ScaleToPage = 0,

    /// <summary>
    /// Scales the image down uniformly (preserving aspect ratio) to fit within the remaining vertical space on the
    /// currently open page when at least one body line of space remains, avoiding a page break. When the open page has
    /// less than one body line of space left, starts a new page and scales to the full page content area.
    /// </summary>
    ScaleToRemaining = 1,

    /// <summary>
    /// Scales the image width to the available content width (preserving aspect ratio) without shrinking its height to
    /// the page height. Starts on a fresh page when the current page already has content and cannot hold the image, and
    /// clips any vertical overflow to the page's content bounds so margins stay clean.
    /// </summary>
    ScaleToWidth = 2,

    /// <summary>
    /// Preserves the image's requested width (or 96 DPI pixel width) and aspect-ratio height without shrinking width or
    /// height to the page bounds. Starts on a fresh page when the current page already has content and cannot hold the
    /// image, and clips any horizontal or vertical overflow to the page's content bounds.
    /// </summary>
    ClipOverflow = 3,
}
