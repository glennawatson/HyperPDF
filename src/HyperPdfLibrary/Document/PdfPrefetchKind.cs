// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>Which objects loading ahead follows from a starting dictionary.</summary>
internal enum PdfPrefetchKind
{
    /// <summary>A page: its content, resources, fonts, images and annotations, but not the page tree or other pages.</summary>
    Page = 0,

    /// <summary>The page tree: the /Kids of each node.</summary>
    PageTree = 1,

    /// <summary>The outline: /First and /Next of each entry.</summary>
    Outline = 2,

    /// <summary>A page's separation dictionary: its <c>/Pages</c> array, colourant and colour space.</summary>
    Separation = 3,
}
