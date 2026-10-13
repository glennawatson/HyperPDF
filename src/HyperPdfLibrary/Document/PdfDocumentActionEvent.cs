// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>A document-level event a script can run on (ISO 32000-2 12.6.3, Table 200, the catalog's <c>/AA</c>).</summary>
public enum PdfDocumentActionEvent
{
    /// <summary>Before the document closes (<c>WC</c>).</summary>
    WillClose = 0,

    /// <summary>Before saving (<c>WS</c>).</summary>
    WillSave = 1,

    /// <summary>After saving (<c>DS</c>).</summary>
    DidSave = 2,

    /// <summary>Before printing (<c>WP</c>).</summary>
    WillPrint = 3,

    /// <summary>After printing (<c>DP</c>).</summary>
    DidPrint = 4,
}
