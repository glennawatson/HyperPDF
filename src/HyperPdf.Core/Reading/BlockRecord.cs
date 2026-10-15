// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Reading;

/// <summary>A block being worked out: the chain of lines that make it up and its box.</summary>
[DebuggerDisplay("BlockRecord: {LineCount} lines")]
internal record struct BlockRecord
{
    /// <summary>Gets or sets the first line.</summary>
    internal int First { get; set; }

    /// <summary>Gets or sets the last line.</summary>
    internal int Last { get; set; }

    /// <summary>Gets or sets the number of lines.</summary>
    internal int LineCount { get; set; }

    /// <summary>Gets or sets the visible characters of all the lines.</summary>
    internal int TextLength { get; set; }

    /// <summary>Gets or sets the block's box.</summary>
    internal PageRect Bounds { get; set; }

    /// <summary>Gets or sets what the block is, once classified.</summary>
    internal ReadingBlockKind Kind { get; set; }
}
