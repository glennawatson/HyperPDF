// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>A column of a table found from alignment: the span of the page its pieces of text cover.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Right">The right edge.</param>
[DebuggerDisplay("ColumnBand: {Left} to {Right}")]
internal readonly record struct ColumnBand(float Left, float Right);
