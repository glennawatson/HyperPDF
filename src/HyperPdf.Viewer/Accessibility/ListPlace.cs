// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Accessibility;

/// <summary>Where a list item is in its list.</summary>
/// <param name="Position">The item's place from 1, or 0 when the block is not a list item.</param>
/// <param name="Size">The number of items in the list, or 0.</param>
[DebuggerDisplay("ListPlace {Position} of {Size}")]
internal readonly record struct ListPlace(int Position, int Size);
