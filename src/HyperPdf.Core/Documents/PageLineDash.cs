// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>The pattern of dashes a line of a page object is drawn with.</summary>
public enum PageLineDash
{
    /// <summary>An unbroken line.</summary>
    Solid = 0,

    /// <summary>Long dashes with gaps of the same length.</summary>
    Dashed = 1,

    /// <summary>Round dots with gaps between them.</summary>
    Dotted = 2,

    /// <summary>Long dashes and dots, taking turns.</summary>
    DashDot = 3,
}
