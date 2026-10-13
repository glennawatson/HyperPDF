// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.AssociatedFiles;

namespace HyperPdfLibrary.Document;

/// <summary>A file to embed in a document (ISO 32000-2 7.11.4).</summary>
/// <param name="Name">The file name shown to the user and used as the attachment's key in the <c>/EmbeddedFiles</c> name tree.</param>
/// <param name="Data">The file's bytes.</param>
[DebuggerDisplay("PdfAttachmentSpec: {Name}")]
public sealed record PdfAttachmentSpec(string Name, ReadOnlyMemory<byte> Data)
{
    /// <summary>Gets the MIME type, such as <c>application/pdf</c>, or <see langword="null"/> when unknown.</summary>
    public string? MimeType { get; init; }

    /// <summary>Gets a description of the file, or <see langword="null"/> for none.</summary>
    public string? Description { get; init; }

    /// <summary>Gets how the file relates to the document, or <see langword="null"/> to leave it unstated.</summary>
    public PdfFileRelationship? Relationship { get; init; }

    /// <summary>Gets the file's creation date, or <see langword="null"/> for none.</summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>Gets the file's modification date, or <see langword="null"/> for none.</summary>
    public DateTimeOffset? Modified { get; init; }

    /// <summary>Gets a value indicating whether the bytes are stored compressed with Flate; on by default.</summary>
    public bool Compress { get; init; } = true;
}
