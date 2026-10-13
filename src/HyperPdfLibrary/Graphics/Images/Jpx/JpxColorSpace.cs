// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The colour space a JPEG 2000 file declares in its colour specification box (T.800 I.5.3.3).</summary>
internal enum JpxColorSpace
{
    /// <summary>A raw codestream, which declares none.</summary>
    Unspecified = 0,

    /// <summary>A colour box the decoder does not know, or an ICC profile.</summary>
    Unknown = 1,

    /// <summary>Standard RGB (sRGB), enumerated colour space 16.</summary>
    Srgb = 2,

    /// <summary>Greyscale, enumerated colour space 17.</summary>
    Gray = 3,

    /// <summary>Luma and chroma from sRGB (sYCC), enumerated colour space 18.</summary>
    Sycc = 4,

    /// <summary>Extended sYCC (e-sYCC), enumerated colour space 24.</summary>
    Eycc = 5,

    /// <summary>CMYK, enumerated colour space 12.</summary>
    Cmyk = 6,
}
