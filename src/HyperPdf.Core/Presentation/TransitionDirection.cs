// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Presentation;

/// <summary>The four straight directions a page transition moves in.</summary>
public enum TransitionDirection
{
    /// <summary>From the left of the page to the right.</summary>
    LeftToRight = 0,

    /// <summary>From the bottom of the page upwards.</summary>
    BottomToTop = 1,

    /// <summary>From the right of the page leftwards.</summary>
    RightToLeft = 2,

    /// <summary>From the top of the page downwards.</summary>
    TopToBottom = 3,
}
