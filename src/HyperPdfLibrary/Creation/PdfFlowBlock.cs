// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>A block of a document that <see cref="PdfTextFlow"/> lays out from top to bottom and across pages.</summary>
[DebuggerDisplay("PdfFlowBlock")]
public abstract record PdfFlowBlock
{
    /// <summary>Initializes a new instance of the <see cref="PdfFlowBlock"/> class; only the blocks in this library derive from it.</summary>
    private protected PdfFlowBlock()
    {
    }

    /// <summary>
    /// Gets a value indicating whether this block must stay on the same page as the start of the following block.
    /// When enabled and the block together with the opening content of the next block (or chain of keep-with-next
    /// blocks) does not fit in the remaining space on the current page, a new page is started before this block.
    /// <see cref="PdfFlowHeading"/> enables this by default.
    /// </summary>
    public bool KeepWithNext { get; init; }
}
