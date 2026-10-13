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
}
