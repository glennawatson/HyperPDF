// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>The enumerated colour space a JPEG 2000 file declares in its colour specification box (ISO 15444-1 table I.10).</summary>
public enum PdfJpxColorSpace
{
    /// <summary>Greyscale for one colour channel, sRGB for three and CMYK for four.</summary>
    Automatic = 0,

    /// <summary>Greyscale (enumerated value 17), for one colour channel.</summary>
    Gray = 1,

    /// <summary>The sRGB colour space (enumerated value 16), for three colour channels.</summary>
    Srgb = 2,

    /// <summary>The CMYK colour space (enumerated value 12, a JPX feature that PDF requires readers to support), for four colour channels.</summary>
    Cmyk = 3,
}
