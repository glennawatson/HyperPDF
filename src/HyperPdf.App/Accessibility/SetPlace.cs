// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.App.Accessibility;

/// <summary>Where an element sits among its siblings of the same kind, such as the third of seven list items.</summary>
/// <param name="Position">The place from 1, or 0 when the element is not part of a set.</param>
/// <param name="Size">How many elements the set holds, or 0 when the element is not part of a set.</param>
[DebuggerDisplay("SetPlace: {Position} of {Size}")]
internal readonly record struct SetPlace(int Position, int Size);
