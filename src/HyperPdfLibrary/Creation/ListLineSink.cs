// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Creation;

/// <summary>Sends wrapped lines to a caller's list.</summary>
/// <param name="Lines">The list that receives the lines.</param>
[DebuggerDisplay("ListLineSink: {Lines.Count} lines")]
internal readonly record struct ListLineSink(List<PdfTextLine> Lines) : ILineSink
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(PdfTextLine line) => Lines.Add(line);
}
