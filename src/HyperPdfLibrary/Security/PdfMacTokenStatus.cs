// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>The outcome of checking a PDF MAC token's own CMS structure and MAC (ISO/TS 32004:2024 6.3 and Annex B.3).</summary>
internal enum PdfMacTokenStatus
{
    /// <summary>The token follows the standard and its MAC matches.</summary>
    Valid = 0,

    /// <summary>The token is not the CMS structure the standard requires.</summary>
    Malformed = 1,

    /// <summary>The token names a key derivation, key wrap or MAC algorithm that this reader does not check.</summary>
    UnsupportedAlgorithm = 2,

    /// <summary>The token names a digest that the standard does not allow in a PDF MAC.</summary>
    DigestNotAllowed = 3,

    /// <summary>The token names an allowed digest that this computer's cryptography library does not provide.</summary>
    DigestUnavailable = 4,

    /// <summary>The wrapped MAC key did not unwrap, so the token was not made with this document's file key.</summary>
    KeyUnwrapFailed = 5,

    /// <summary>The message digest attribute does not match the token's content.</summary>
    ContentDigestMismatch = 6,

    /// <summary>The MAC does not match the authenticated attributes.</summary>
    MacMismatch = 7,
}
