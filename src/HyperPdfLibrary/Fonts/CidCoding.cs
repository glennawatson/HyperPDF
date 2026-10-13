// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts;

/// <summary>What a composite font's character codes mean, taken from the CMap name (ISO 32000-2, 9.7.5.2); it decides how a code without /ToUnicode becomes text.</summary>
internal enum CidCoding
{
    /// <summary>The codes mean nothing on their own: embedded CMap streams and predefined CMap names the library does not know.</summary>
    Unknown = 0,

    /// <summary>The codes are CIDs (Identity-H and Identity-V).</summary>
    Cid = 1,

    /// <summary>The codes are UCS-2 Unicode values (the Uni*-UCS2 CMaps).</summary>
    Ucs2 = 2,

    /// <summary>The codes are UTF-16 code units, with surrogate pairs as four-byte codes (the Uni*-UTF16 CMaps).</summary>
    Utf16 = 3,

    /// <summary>The codes are bytes of a national encoding, such as Shift-JIS or GBK, whose text comes from their CIDs.</summary>
    Native = 4,

    /// <summary>The codes are UTF-32 code points (the Uni*-UTF32 CMaps).</summary>
    Utf32 = 5,
}
