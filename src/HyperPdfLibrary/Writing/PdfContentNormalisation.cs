// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Writing;

/// <summary>The page content changes a save can make. Pages draw exactly as before.</summary>
[Flags]
public enum PdfContentNormalisation
{
    /// <summary>Leaves page content as it is.</summary>
    None = 0,

    /// <summary>
    /// Joins a page's <c>/Contents</c> array into one stream (ISO 32000-2 §7.8.2: the streams are one sequence with a
    /// token break between them).
    /// </summary>
    CoalesceContents = 1 << 0,

    /// <summary>
    /// Moves inline images larger than <see cref="PdfCompactOptions.InlineImageThreshold"/> out of page content into image
    /// XObjects named in the page resources (ISO 32000-2 §8.9.7), drawn with <c>Do</c>. A page that changes gets its
    /// content as one stream. An image that names a colour space the page resources lack stays inline, and inline images in
    /// form XObjects are not moved.
    /// </summary>
    ExternaliseInlineImages = 1 << 1,

    /// <summary>Both changes.</summary>
    All = CoalesceContents | ExternaliseInlineImages,
}
