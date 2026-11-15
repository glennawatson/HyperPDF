// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Markup;

/// <summary>Which edge or middle of the picked objects they line up on.</summary>
public enum PageObjectAlignment
{
    /// <summary>The left edges line up.</summary>
    Left = 0,

    /// <summary>The middles, left to right, line up.</summary>
    Centre = 1,

    /// <summary>The right edges line up.</summary>
    Right = 2,

    /// <summary>The top edges line up.</summary>
    Top = 3,

    /// <summary>The middles, top to bottom, line up.</summary>
    Middle = 4,

    /// <summary>The bottom edges line up.</summary>
    Bottom = 5,
}
