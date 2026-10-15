// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>An item and the square of a table grid it sits in.</summary>
/// <param name="Item">The item.</param>
/// <param name="Square">The square's position, row by row.</param>
[DebuggerDisplay("PlacedItem: {Item.Index} in square {Square}")]
internal readonly record struct PlacedItem(LayoutItem Item, int Square);
