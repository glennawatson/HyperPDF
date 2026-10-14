// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Writing;

/// <summary>The filters <see cref="PdfStreamMode.Recompress"/> writes a stream with (ISO 32000-2 7.4).</summary>
public enum PdfStreamEncoding
{
    /// <summary>Flate (<c>/FlateDecode</c>), the default. It is usually the smallest of the compression filters.</summary>
    Flate = 0,

    /// <summary>LZW (<c>/LZWDecode</c>). PDF/A parts 1 to 3 forbid it, so a file that claims PDF/A or PDF/UA is written with Flate instead.</summary>
    Lzw = 1,

    /// <summary>Run length (<c>/RunLengthDecode</c>), which suits data with long runs of one byte. It is written only when it is smaller than the data.</summary>
    RunLength = 2,

    /// <summary>Flate, then ASCII base-85 text (<c>/Filter [/ASCII85Decode /FlateDecode]</c>), for a stream that must survive 7-bit channels. The data grows by a quarter.</summary>
    FlateAscii85 = 3,

    /// <summary>Flate, then ASCII hexadecimal text (<c>/Filter [/ASCIIHexDecode /FlateDecode]</c>). The data grows to more than double.</summary>
    FlateAsciiHex = 4,

    /// <summary>
    /// LZW, then ASCII base-85 text (<c>/Filter [/ASCII85Decode /LZWDecode]</c>), the chain ISO 32000-2 7.4.1 gives as its
    /// example. A file that claims PDF/A or PDF/UA is written with Flate and base-85 text instead.
    /// </summary>
    LzwAscii85 = 5,
}
