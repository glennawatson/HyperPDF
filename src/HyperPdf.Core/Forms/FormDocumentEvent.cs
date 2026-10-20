// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms;

/// <summary>A point in a document's life that its additional actions can run at (ISO 32000-2 12.6.3, Table 200).</summary>
public enum FormDocumentEvent
{
    /// <summary>Before the document closes.</summary>
    WillClose = 0,

    /// <summary>Before the document is saved.</summary>
    WillSave = 1,

    /// <summary>After the document is saved.</summary>
    DidSave = 2,

    /// <summary>Before the document is printed.</summary>
    WillPrint = 3,

    /// <summary>After the document is printed.</summary>
    DidPrint = 4,
}
