// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A tab: the text after it jumps to the next tab stop of the paragraph.</summary>
[DebuggerDisplay("PdfTab")]
public sealed record PdfTab : PdfInline
{
    /// <summary>Gets a tab.</summary>
    public static PdfTab Instance { get; } = new();
}
