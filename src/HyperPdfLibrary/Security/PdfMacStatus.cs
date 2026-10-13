// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>
/// Whether the integrity protection of an encrypted PDF 2.0 document (the PDF MAC of ISO/TS 32004:2024) holds. Only
/// <see cref="Valid"/> means the bytes were checked and match; every other value except <see cref="NotApplicable"/> means
/// the document must not be called intact.
/// </summary>
public enum PdfMacStatus
{
    /// <summary>The document is not encrypted, or its permissions do not require a PDF MAC and none is present.</summary>
    NotApplicable = 0,

    /// <summary>A PDF MAC is present, it was made with this document's file key, and it matches the document's bytes.</summary>
    Valid = 1,

    /// <summary>A PDF MAC is present but does not hold: the bytes changed, the token was made with another key, or it is damaged or does not follow the standard.</summary>
    Invalid = 2,

    /// <summary>The permissions require a PDF MAC, and this revision has none.</summary>
    Missing = 3,

    /// <summary>The PDF MAC uses a method this computer cannot check, so it is neither confirmed nor rejected.</summary>
    Unsupported = 4,
}
