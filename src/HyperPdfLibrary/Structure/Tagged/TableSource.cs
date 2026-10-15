// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>What a table search reads: the items' text, the items and the page's lines.</summary>
/// <param name="Text">The items' text.</param>
/// <param name="Items">The items, grouped as the lines refer to them.</param>
/// <param name="Lines">The page's lines.</param>
[DebuggerDisplay("TableSource: {Lines.Length} lines")]
internal readonly record struct TableSource(string Text, LayoutItem[] Items, LayoutLine[] Lines);
