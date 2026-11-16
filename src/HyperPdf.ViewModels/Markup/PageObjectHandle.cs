// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Markup;

/// <summary>A handle on the box of the picked object that resizes it when dragged.</summary>
public enum PageObjectHandle
{
    /// <summary>No handle.</summary>
    None = 0,

    /// <summary>The top left corner.</summary>
    TopLeft = 1,

    /// <summary>The middle of the top edge.</summary>
    Top = 2,

    /// <summary>The top right corner.</summary>
    TopRight = 3,

    /// <summary>The middle of the right edge.</summary>
    Right = 4,

    /// <summary>The bottom right corner.</summary>
    BottomRight = 5,

    /// <summary>The middle of the bottom edge.</summary>
    Bottom = 6,

    /// <summary>The bottom left corner.</summary>
    BottomLeft = 7,

    /// <summary>The middle of the left edge.</summary>
    Left = 8,
}
