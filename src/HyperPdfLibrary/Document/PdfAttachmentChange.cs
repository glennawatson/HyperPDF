// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.AssociatedFiles;

namespace HyperPdfLibrary.Document;

/// <summary>Changes to an embedded file; a property left <see langword="null"/> is not changed.</summary>
[DebuggerDisplay("PdfAttachmentChange")]
public sealed record PdfAttachmentChange
{
    /// <summary>Gets the new description; an empty string removes it.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the new MIME type; an empty string removes it.</summary>
    public string? MimeType { get; init; }

    /// <summary>Gets the new relationship to the document.</summary>
    public PdfFileRelationship? Relationship { get; init; }

    /// <summary>Gets the new creation date.</summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>Gets the new modification date.</summary>
    public DateTimeOffset? Modified { get; init; }

    /// <summary>Gets the new bytes. The size and MD5 checksum are worked out again.</summary>
    public ReadOnlyMemory<byte>? Data { get; init; }

    /// <summary>Gets the new file name; the attachment is listed under it afterwards.</summary>
    public string? Name { get; init; }
}
