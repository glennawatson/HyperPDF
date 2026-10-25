// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Exporting;

/// <summary>A file format a page can be exported as: a picture or a vector drawing.</summary>
public enum PageFileFormat
{
    /// <summary>PNG: lossless, with transparency.</summary>
    Png = 0,

    /// <summary>JPEG: lossy and small; transparent areas turn white.</summary>
    Jpeg = 1,

    /// <summary>TIFF: a print and scanning picture format, not compressed.</summary>
    Tiff = 2,

    /// <summary>BMP: a simple Windows picture.</summary>
    Bmp = 3,

    /// <summary>PBM, PGM or PPM: the simplest picture format many tools read; no transparency.</summary>
    Pnm = 4,

    /// <summary>PAM: like PPM, with transparency.</summary>
    Pam = 5,

    /// <summary>TGA: a picture format used in games and graphics software.</summary>
    Tga = 6,

    /// <summary>PSD: a Photoshop document with one layer.</summary>
    Psd = 7,

    /// <summary>SVG: a vector drawing that stays sharp at any size.</summary>
    Svg = 8,

    /// <summary>PostScript: a printer language file, one page for each page of the document.</summary>
    PostScript = 9,

    /// <summary>Encapsulated PostScript: one page that other documents can place.</summary>
    EncapsulatedPostScript = 10,
}
