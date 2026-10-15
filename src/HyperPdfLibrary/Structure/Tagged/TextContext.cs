// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>What the nodes of one run of text lines are made from.</summary>
/// <param name="Nodes">Makes the nodes.</param>
/// <param name="Column">The box around the region the lines are in.</param>
/// <param name="Items">The array of items the lines are runs of.</param>
/// <param name="Buffer">Room for a node's text.</param>
[DebuggerDisplay("TextContext: column {Column}")]
internal readonly record struct TextContext(LayoutNodes Nodes, PdfViewerRect Column, LayoutItem[] Items, char[] Buffer);
