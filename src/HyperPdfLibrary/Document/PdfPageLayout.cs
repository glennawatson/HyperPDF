// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>How pages are laid out when the document opens (ISO 32000-2 7.7.2, <c>/PageLayout</c>).</summary>
public enum PdfPageLayout
{
    /// <summary>One page at a time.</summary>
    SinglePage = 0,

    /// <summary>Pages in one scrolling column.</summary>
    OneColumn = 1,

    /// <summary>Two columns, odd pages on the left.</summary>
    TwoColumnLeft = 2,

    /// <summary>Two columns, odd pages on the right.</summary>
    TwoColumnRight = 3,

    /// <summary>Two pages at a time, odd pages on the left.</summary>
    TwoPageLeft = 4,

    /// <summary>Two pages at a time, odd pages on the right.</summary>
    TwoPageRight = 5,
}
