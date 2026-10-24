// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A line break: the text after it starts a new line of the same paragraph.</summary>
[DebuggerDisplay("PdfLineBreak")]
public sealed record PdfLineBreak : PdfInline
{
    /// <summary>Gets a line break.</summary>
    public static PdfLineBreak Instance { get; } = new();
}
