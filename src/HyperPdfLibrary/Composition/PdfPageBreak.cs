// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A page break: the next block starts on a new page.</summary>
[DebuggerDisplay("PdfPageBreak")]
public sealed record PdfPageBreak : PdfBlock
{
    /// <summary>Gets a page break.</summary>
    public static PdfPageBreak Instance { get; } = new();
}
