// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Where on a page text that is stamped on it sits.</summary>
public enum PageStampAnchor
{
    /// <summary>In the middle of the page.</summary>
    Center = 0,

    /// <summary>In the top left corner, inside the margin.</summary>
    TopLeft = 1,

    /// <summary>Centred along the top edge, inside the margin: where a header goes.</summary>
    TopCenter = 2,

    /// <summary>In the top right corner, inside the margin.</summary>
    TopRight = 3,

    /// <summary>In the bottom left corner, inside the margin.</summary>
    BottomLeft = 4,

    /// <summary>Centred along the bottom edge, inside the margin: where a footer goes.</summary>
    BottomCenter = 5,

    /// <summary>In the bottom right corner, inside the margin.</summary>
    BottomRight = 6,

    /// <summary>Centred down the left edge, inside the margin.</summary>
    MiddleLeft = 7,

    /// <summary>Centred down the right edge, inside the margin.</summary>
    MiddleRight = 8,
}
