// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Which pagination artifact a layout node is: a running header, a running footer or a page number (ISO 32000-2 14.8.2.2.2).</summary>
public enum PdfPaginationKind
{
    /// <summary>Not a pagination artifact.</summary>
    None = 0,

    /// <summary>A running header: text repeated at the top of pages.</summary>
    Header = 1,

    /// <summary>A running footer: text repeated at the foot of pages.</summary>
    Footer = 2,

    /// <summary>A page number, at the top or the foot of the page.</summary>
    PageNumber = 3,
}
