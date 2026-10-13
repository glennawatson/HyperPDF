// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>Where a revision keeps its PDF MAC token (ISO/TS 32004:2024 5.2.3, Table 6).</summary>
public enum PdfMacLocation
{
    /// <summary>The revision has no /AuthCode dictionary.</summary>
    None = 0,

    /// <summary>The token is the /MAC entry of the trailer's /AuthCode dictionary.</summary>
    Standalone = 1,

    /// <summary>The token is an unsigned attribute of a signature (/MACLocation /AttachedToSig).</summary>
    AttachedToSignature = 2,

    /// <summary>The /MACLocation is missing or names a value this reader does not know.</summary>
    Unknown = 3,
}
