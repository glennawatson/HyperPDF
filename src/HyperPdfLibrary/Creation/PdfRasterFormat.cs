// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>How the pixels of a <see cref="PdfRasterImage"/> are laid out.</summary>
public enum PdfRasterFormat
{
    /// <summary>No format; the pixels cannot be stored.</summary>
    None = 0,

    /// <summary>One byte a pixel, 0 black to 255 white.</summary>
    Gray8 = 1,

    /// <summary>Three bytes a pixel: red, green, blue.</summary>
    Rgb24 = 2,

    /// <summary>Four bytes a pixel: blue, green, red, then straight (not premultiplied) alpha.</summary>
    Bgra32 = 3,

    /// <summary>One bit a pixel, most significant bit first, each row padded to a whole byte; a set bit is white.</summary>
    Bilevel1 = 4,
}
