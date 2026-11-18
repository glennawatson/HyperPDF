// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Windowing.Windows;

/// <summary>The edges of a window that a resize drags. A corner is two edges together.</summary>
[Flags]
public enum WindowEdges
{
    /// <summary>No edge.</summary>
    None = 0,

    /// <summary>The top edge.</summary>
    Top = 1 << 0,

    /// <summary>The bottom edge.</summary>
    Bottom = 1 << 1,

    /// <summary>The left edge.</summary>
    Left = 1 << 2,

    /// <summary>The right edge.</summary>
    Right = 1 << 3,
}
