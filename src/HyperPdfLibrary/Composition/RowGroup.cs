// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>Rows of a grid that must stay together on a page because cells span between them; a row no cell spans out of is a group of its own.</summary>
/// <param name="Start">The first row of the group.</param>
/// <param name="End">The place after the last row of the group.</param>
/// <param name="Height">The height of all the rows of the group.</param>
[DebuggerDisplay("RowGroup: rows {Start}..{End}, {Height} pt")]
internal readonly record struct RowGroup(int Start, int End, float Height);
