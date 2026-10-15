// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>The lines of one list item in a region, and its label.</summary>
/// <param name="Start">The position of the item's first line in the region.</param>
/// <param name="End">The position after the item's last line.</param>
/// <param name="Label">The label that starts the first line.</param>
[DebuggerDisplay("ListItemSpan: lines {Start} to {End}")]
internal readonly record struct ListItemSpan(int Start, int End, ListLabel Label);
