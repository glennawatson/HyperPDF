// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>Why a document cannot be flattened at all.</summary>
public enum PdfFlattenRefusal
{
    /// <summary>The document can be flattened.</summary>
    None = 0,

    /// <summary>The document is signed or certified; flattening changes the signed pages and would invalidate the signature.</summary>
    Signed = 1,
}
