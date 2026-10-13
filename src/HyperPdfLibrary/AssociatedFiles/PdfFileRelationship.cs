// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.AssociatedFiles;

/// <summary>How an attached file relates to the part of the document it is attached to (ISO 32000-2 14.13, Table 43 <c>/AFRelationship</c>).</summary>
public enum PdfFileRelationship
{
    /// <summary>The relationship is not stated.</summary>
    Unspecified = 0,

    /// <summary>The file is the original source of the content.</summary>
    Source = 1,

    /// <summary>The file holds data the content shows, such as a table behind a chart.</summary>
    Data = 2,

    /// <summary>The file is another form of the content, such as an XML version of an invoice.</summary>
    Alternative = 3,

    /// <summary>The file adds to the content.</summary>
    Supplement = 4,

    /// <summary>The file is an encrypted payload document (PDF 2.0).</summary>
    EncryptedPayload = 5,

    /// <summary>The file holds data for filling in a form.</summary>
    FormData = 6,

    /// <summary>The file is a schema for the content.</summary>
    Schema = 7,
}
