// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.ViewModels.Reading;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What focus mode shows: the column of text wrapped for the width it has, and where in it the reader is.</summary>
/// <param name="Lines">The lines of the column as they are wrapped now.</param>
/// <param name="Blocks">The paragraphs the lines belong to, by the number a line carries.</param>
/// <param name="Stamp">What the lines were wrapped from: the width, the text size and the text of each page.</param>
/// <param name="First">The first line shown.</param>
/// <param name="Visible">The lines that fit the column's height.</param>
/// <param name="Asked">The pages whose text was asked for.</param>
[DebuggerDisplay("FocusState: {Lines.Length} lines from {First}")]
internal sealed record FocusState(FocusLine[] Lines, FocusBlockViewModel[] Blocks, int Stamp, int First, int Visible, int[] Asked)
{
    /// <summary>Gets the state before anything was wrapped.</summary>
    internal static FocusState Start { get; } = new([], [], 0, 0, 1, []);
}
