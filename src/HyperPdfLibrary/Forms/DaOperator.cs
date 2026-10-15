// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Forms;

/// <summary>An operator a default appearance string is read for.</summary>
internal enum DaOperator
{
    /// <summary>Any other token.</summary>
    None = 0,

    /// <summary>The font operator, <c>Tf</c>.</summary>
    Font = 1,

    /// <summary>The grey operator, <c>g</c>.</summary>
    Gray = 2,

    /// <summary>The RGB operator, <c>rg</c>.</summary>
    Rgb = 3,

    /// <summary>The CMYK operator, <c>k</c>.</summary>
    Cmyk = 4,
}
