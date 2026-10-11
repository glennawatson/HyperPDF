// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Layers;

/// <summary>The layers read from a document and their state.</summary>
/// <param name="Layers">The layers in document order.</param>
/// <param name="Hidden">The object numbers hidden by default.</param>
/// <param name="NonView">The object numbers of groups that are not for viewing, and so are always shown.</param>
/// <param name="RadioGroups">The sets of groups of which only one is shown.</param>
/// <param name="PrintHidden">The object numbers hidden when printing.</param>
[DebuggerDisplay("OptionalContentSet: {Layers.Length} layers")]
internal sealed record OptionalContentSet(PdfLayer[] Layers, HashSet<int> Hidden, HashSet<int> NonView, int[][] RadioGroups, HashSet<int> PrintHidden);
