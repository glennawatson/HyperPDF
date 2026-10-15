// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Forms;

/// <summary>The colour operator of a default appearance string.</summary>
internal enum DaColorKind
{
    /// <summary>A grey level, <c>g</c>; the default is black.</summary>
    Gray = 0,

    /// <summary>Red, green and blue, <c>rg</c>.</summary>
    Rgb = 1,

    /// <summary>Cyan, magenta, yellow and black, <c>k</c>.</summary>
    Cmyk = 2,
}
