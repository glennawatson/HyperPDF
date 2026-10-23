// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A section as laid out: its content and setup, and how many pages it came to.</summary>
[DebuggerDisplay("SectionInfo: {Number}")]
internal sealed class SectionInfo
{
    /// <summary>Initializes a new instance of the <see cref="SectionInfo"/> class.</summary>
    /// <param name="number">The section's place in the document, counted from 1.</param>
    /// <param name="section">The section.</param>
    internal SectionInfo(int number, PdfSection section)
    {
        Number = number;
        Section = section;
    }

    /// <summary>Gets the section's place in the document, counted from 1.</summary>
    internal int Number { get; }

    /// <summary>Gets the section.</summary>
    internal PdfSection Section { get; }

    /// <summary>Gets the page setup.</summary>
    internal PdfPageSetup Setup => Section.Setup;

    /// <summary>Gets or sets the number of pages the section came to, once it is laid out.</summary>
    internal int PageCount { get; set; }
}
