// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Optimizing;

/// <summary>The codec <see cref="PdfOptimizer"/> uses to re-encode continuous-tone grey and RGB images.</summary>
public enum PdfOptimizeImageCodec
{
    /// <summary>JPEG, through the document's image codec. Lossless runs only recompress.</summary>
    Jpeg = 0,

    /// <summary>
    /// JPEG 2000 (<c>JPXDecode</c>), written by the library's own encoder: lossy at <see cref="PdfOptimizeOptions.JpegQuality"/>
    /// when lossy changes are allowed, otherwise lossless. A document that claims PDF/A-1 keeps JPEG, as that level forbids JPEG 2000.
    /// </summary>
    Jpeg2000 = 1,
}
