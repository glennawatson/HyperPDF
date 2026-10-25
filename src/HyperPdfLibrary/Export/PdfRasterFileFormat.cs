// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Export;

/// <summary>The picture file formats <see cref="PdfRasterFiles"/> writes.</summary>
public enum PdfRasterFileFormat
{
    /// <summary>PNG: lossless, with alpha.</summary>
    Png = 0,

    /// <summary>JPEG: lossy, with no alpha; transparent pixels are placed over white.</summary>
    Jpeg = 1,

    /// <summary>Baseline TIFF with no compression and with alpha.</summary>
    Tiff = 2,

    /// <summary>Windows bitmap: 8-bit grey, 24-bit colour, or 32-bit colour with alpha.</summary>
    Bmp = 3,

    /// <summary>Portable anymap (PBM, PGM or PPM): no alpha; transparent pixels are placed over white.</summary>
    Pnm = 4,

    /// <summary>Portable arbitrary map (PAM): grey or colour, with alpha.</summary>
    Pam = 5,

    /// <summary>Truevision TGA, uncompressed, with alpha.</summary>
    Tga = 6,

    /// <summary>Photoshop document with one flattened layer and, for transparent images, an extra alpha channel.</summary>
    Psd = 7,
}
