// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>What a <see cref="PdfField"/> shows.</summary>
public enum PdfFieldKind
{
    /// <summary>The number of the page the field is on, written in the section's page number format.</summary>
    PageNumber = 0,

    /// <summary>The number of pages in the whole document.</summary>
    PageCount = 1,

    /// <summary>The number of the section the field is in, counted from 1.</summary>
    SectionNumber = 2,

    /// <summary>The number of pages in the section the field is in.</summary>
    SectionPageCount = 3,

    /// <summary>The date and time the document was composed, written with the field's date format.</summary>
    Date = 4,

    /// <summary>The document's title.</summary>
    Title = 5,

    /// <summary>The document's author.</summary>
    Author = 6,
}
