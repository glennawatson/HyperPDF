// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>How the pixels given to <see cref="PdfJpegEncoder"/> are laid out. Rows are packed with no padding.</summary>
public enum PdfJpegPixelFormat
{
    /// <summary>No format; the pixels cannot be encoded.</summary>
    None = 0,

    /// <summary>One byte a pixel, 0 black to 255 white. The JPEG has one component.</summary>
    Gray8 = 1,

    /// <summary>Three bytes a pixel: red, green, blue. The JPEG has three components (YCbCr).</summary>
    Rgb24 = 2,

    /// <summary>Four bytes a pixel: red, green, blue and a fourth byte that is ignored. The JPEG has three components (YCbCr).</summary>
    Rgbx32 = 3,

    /// <summary>Four bytes a pixel: blue, green, red, then straight (not premultiplied) alpha. JPEG has no alpha, so each pixel is composited over white.</summary>
    Bgra32 = 4,

    /// <summary>
    /// Four bytes a pixel: cyan, magenta, yellow, black, where 255 is full ink as in a PDF <c>/DeviceCMYK</c> image. The JPEG has
    /// four components and an Adobe marker, and the samples are stored inverted as Adobe applications do, so the
    /// <c>/DCTDecode</c> image needs <c>/Decode [1 0 1 0 1 0 1 0]</c>, which <see cref="PdfImageWriter.AddJpeg(HyperPdfLibrary.Writing.IPdfObjectSink, ReadOnlySpan{byte})"/> adds.
    /// </summary>
    Cmyk32 = 5,
}
