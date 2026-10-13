// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>The result of looking for a PDF MAC token in a signature container.</summary>
internal enum PdfMacSignatureLookup
{
    /// <summary>The signer has the PDF MAC attribute with one value.</summary>
    Found = 0,

    /// <summary>The container is a signature but has no PDF MAC attribute.</summary>
    NotFound = 1,

    /// <summary>The container, or the attribute, is damaged.</summary>
    Malformed = 2,
}
