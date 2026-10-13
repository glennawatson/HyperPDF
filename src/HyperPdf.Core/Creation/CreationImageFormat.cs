// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Creation;

/// <summary>How the bytes of a <see cref="CreationImage"/> are coded.</summary>
public enum CreationImageFormat
{
    /// <summary>No format; the image cannot be stored.</summary>
    None = 0,

    /// <summary>A JPEG file, stored as it is without decoding.</summary>
    Jpeg = 1,

    /// <summary>Four bytes a pixel: blue, green, red, then straight alpha.</summary>
    Bgra32 = 2,

    /// <summary>Three bytes a pixel: red, green, blue.</summary>
    Rgb24 = 3,

    /// <summary>One byte a pixel, 0 black to 255 white.</summary>
    Gray8 = 4,

    /// <summary>One bit a pixel, most significant bit first, each row padded to a whole byte; a set bit is white.</summary>
    Bilevel1 = 5,
}
