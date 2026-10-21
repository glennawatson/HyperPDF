// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Viewing;

/// <summary>The pointer shapes the document view asks for, which each render head draws in its own way.</summary>
public enum ViewerCursor
{
    /// <summary>No shape is asked for, so the pointer follows what is under it.</summary>
    Inherit = 0,

    /// <summary>The ordinary arrow.</summary>
    Arrow = 1,

    /// <summary>The hand, over something that can be clicked or dragged.</summary>
    Hand = 2,

    /// <summary>The cross, for choosing an area or point.</summary>
    Cross = 3,

    /// <summary>The text beam, over text that can be selected.</summary>
    Text = 4,
}
