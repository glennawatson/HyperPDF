// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>The text of some lines joined, with the items it came from.</summary>
/// <param name="Indices">The items' indices, in reading order.</param>
/// <param name="Bounds">The box around the lines.</param>
/// <param name="Height">The sum of the lines' heights.</param>
/// <param name="Length">The characters of text written to the buffer.</param>
[DebuggerDisplay("ComposedText: {Indices.Length} items, {Length} characters")]
internal readonly record struct ComposedText(int[] Indices, PdfViewerRect Bounds, float Height, int Length);
