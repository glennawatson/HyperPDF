// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>The device colour spaces a canvas colour can be in (ISO 32000-2 8.6.4).</summary>
public enum PdfColorKind
{
    /// <summary>One level of grey, from 0 black to 1 white.</summary>
    Gray = 0,

    /// <summary>Red, green and blue levels, each from 0 to 1.</summary>
    Rgb = 1,

    /// <summary>Cyan, magenta, yellow and black amounts, each from 0 to 1.</summary>
    Cmyk = 2,
}
