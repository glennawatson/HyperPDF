// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Text;

/// <summary>Page text folded for searching.</summary>
/// <param name="Text">The folded text.</param>
/// <param name="Map">The page text index of each folded character, or <see langword="null"/> when no character was dropped and the indexes are equal.</param>
[DebuggerDisplay("FoldedText: {Text.Length} chars")]
internal sealed record FoldedText(string Text, int[]? Map);
