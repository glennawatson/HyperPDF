// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Filters;

/// <summary>An image compression filter that decodes to pixels rather than bytes, left for the image decoder.</summary>
public enum PdfImageCodec
{
    /// <summary>No image codec; the data is fully decoded.</summary>
    None = 0,

    /// <summary>JPEG (DCTDecode).</summary>
    Jpeg = 1,

    /// <summary>JPEG 2000 (JPXDecode).</summary>
    Jpeg2000 = 2,

    /// <summary>JBIG2 (JBIG2Decode).</summary>
    Jbig2 = 3,

    /// <summary>CCITT fax group 3 or 4 (CCITTFaxDecode).</summary>
    Ccitt = 4,

    /// <summary>A filter this library does not know, so the stream's bytes cannot be turned into pixels.</summary>
    Unknown = 5,
}
