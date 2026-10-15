// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>What a layout line stands for.</summary>
internal enum LayoutLineKind
{
    /// <summary>A line of text.</summary>
    Text = 0,

    /// <summary>A table, taking part in the reading order as one block.</summary>
    Table = 1,

    /// <summary>An image, taking part in the reading order as one block.</summary>
    Image = 2,
}
