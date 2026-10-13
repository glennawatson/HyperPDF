// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>The five page boundaries of ISO 32000-2 14.11.2.</summary>
public enum PdfPageBox
{
    /// <summary>The boundaries of the physical medium (<c>/MediaBox</c>).</summary>
    Media = 0,

    /// <summary>The region the page is clipped to when displayed or printed (<c>/CropBox</c>); defaults to the media box.</summary>
    Crop = 1,

    /// <summary>The region the page is clipped to in production (<c>/BleedBox</c>); defaults to the crop box.</summary>
    Bleed = 2,

    /// <summary>The intended size of the finished page (<c>/TrimBox</c>); defaults to the crop box.</summary>
    Trim = 3,

    /// <summary>The extent of the page's meaningful content (<c>/ArtBox</c>); defaults to the crop box.</summary>
    Art = 4,
}
