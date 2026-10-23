// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.ImageFiles;

/// <summary>The picture file formats <see cref="PdfImageFiles"/> recognises.</summary>
public enum PdfImageFileFormat
{
    /// <summary>The bytes are not a picture format this library reads.</summary>
    Unknown = 0,

    /// <summary>Portable Network Graphics.</summary>
    Png = 1,

    /// <summary>JPEG; a PDF carries its bytes as they are.</summary>
    Jpeg = 2,

    /// <summary>GIF; the first frame is read.</summary>
    Gif = 3,

    /// <summary>Windows bitmap.</summary>
    Bmp = 4,

    /// <summary>TIFF; the first image is read.</summary>
    Tiff = 5,
}
