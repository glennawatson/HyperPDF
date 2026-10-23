// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A block of a section's content that is laid out from top to bottom: a paragraph, list, table, frame, image, chart, barcode, rule or page break.</summary>
[DebuggerDisplay("PdfBlock")]
public abstract record PdfBlock
{
    /// <summary>Initializes a new instance of the <see cref="PdfBlock"/> class; only the blocks in this library derive from it.</summary>
    private protected PdfBlock()
    {
    }
}
