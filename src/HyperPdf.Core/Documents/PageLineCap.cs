// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>How the ends of a line of a page object are drawn.</summary>
public enum PageLineCap
{
    /// <summary>Square, stopping at the end point.</summary>
    Butt = 0,

    /// <summary>Round, ending in a half circle.</summary>
    Round = 1,

    /// <summary>Square, reaching half the line's width past the end point.</summary>
    Square = 2,
}
