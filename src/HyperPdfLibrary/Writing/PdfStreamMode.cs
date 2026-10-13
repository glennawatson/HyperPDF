// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Writing;

/// <summary>What <see cref="PdfCompactWriter"/> does with the data of each stream (ISO 32000-2 §7.3.8, §7.4).</summary>
public enum PdfStreamMode
{
    /// <summary>
    /// Compresses a stream that has no filter when that makes it smaller, and copies a filtered stream as it is. A
    /// damaged Flate or LZW stream is written again from the part that decoded.
    /// </summary>
    Compress = 0,

    /// <summary>Copies every stream as it is. Only a damaged Flate or LZW stream is written again, so the file needs no repair.</summary>
    Preserve = 1,

    /// <summary>
    /// Decodes every stream whose filters are all byte filters (Flate, LZW, ASCII85, ASCIIHex, RunLength), including
    /// their predictors, and writes it again as Flate at <see cref="PdfCompactOptions.CompressionLevel"/>, when that is
    /// smaller. Image codecs (DCT, JPX, JBIG2, CCITT) are copied untouched.
    /// </summary>
    Recompress = 2,

    /// <summary>
    /// Decodes every stream whose filters are all byte filters and writes it with no filter, to read it in a text editor.
    /// Image codecs are copied untouched, and object streams are written unfiltered.
    /// </summary>
    Uncompressed = 3,
}
