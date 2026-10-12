// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Editing;

/// <summary>The character encoding of an XMP packet.</summary>
internal enum XmpEncoding
{
    /// <summary>UTF-8, the encoding new packets use.</summary>
    Utf8 = 0,

    /// <summary>UTF-16, big-endian.</summary>
    Utf16BigEndian = 1,

    /// <summary>UTF-16, little-endian.</summary>
    Utf16LittleEndian = 2,

    /// <summary>UTF-32, big-endian.</summary>
    Utf32BigEndian = 3,

    /// <summary>UTF-32, little-endian.</summary>
    Utf32LittleEndian = 4,
}
